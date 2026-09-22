# Semantic tree

The tree a parse produces, the visitors over it, and the two guarantees that make the
tree worth having: it round-trips to the source text, and every token knows its role.

Types live in `Commands.Parser.SemanticTree` (project `Parser.Tree`). All nodes are
records and implement `IVisitable`.

## Node catalogue

### Roots

| Node | Fields | Meaning |
| --- | --- | --- |
| `RootNode` | abstract | What a parse returns. |
| `EmptyCommand` | none | Empty or whitespace-only input. |
| `PipedCommandList` | `OrderedCommands: List<CommandExpression>` | One or more commands, left to right. |

### Command expressions

| Node | Fields | Meaning |
| --- | --- | --- |
| `CommandExpression` | `Expression: OneOf<FunctionExpression, CommandExpressionCli, InstanceTag>` | One stage of a pipeline. |
| `FunctionExpression` | `Id: Identifier`, `Arguments: CommandArguments` | `name(a, b: c)`. |
| `CommandExpressionCli` | `Name: CommandName`, `Arguments: CommandArguments` | `name a b`. |
| `CommandName` | `Name: string` | A command name in command line form. |

An `InstanceTag` in `CommandExpression` is the tag form: the stage produces a value
without calling a command.

### Arguments

| Node | Fields | Meaning |
| --- | --- | --- |
| `CommandArguments` | `Arguments: List<CommandArgument>` | Ordered arguments as written. |
| `CommandArgument` | abstract | Base. |
| `CommandArgumentValue` | `Value: Value` | A value in command line form. |
| `RequiredCommandArgument` | `Value: Value` | A positional value in function form. |
| `OptionalCommandArgument` | `Name: OneOf<CommandArgumentFlag, Identifier>`, `Value: Value` | `name: value`. |
| `CommandArgumentFlag` | `Name: string` | `-flag`, stored without the dash. |

### Values

| Node | Fields | Meaning |
| --- | --- | --- |
| `Value` | abstract | Base. |
| `SimpleValue` | abstract | A value that is not a tag. |
| `Identifier` | `Name: string` | A bare word or identifier, as written. |
| `VariableReference` | `Name: VariableName`, `Members: List<MemberName>` | `$name`, or `$name.member`. |
| `MemberName` | `Name: string` | The `.size` of `$row.size`. It carries its own stop. |
| `Constant` | abstract | Base for literals. |
| `StringConstant` | `Value: string`, `QuoteCount: int`, `QuoteString: string` | A string literal. |
| `TagValue` | `Tag: InstanceTag` | A tag used where a value is expected. |
| `NestedPipeline` | `Pipeline: PipedCommandList` | A pipeline in parentheses, where a value is expected. |

### Expressions

| Node | Fields | Meaning |
| --- | --- | --- |
| `ExpressionNode` | abstract, descends from `Value` | An expression written in argument position. |
| `ComparisonExpression` | `Left: Value`, `Operator: OperatorWord`, `Right: Value` | `$row.size gt 100`. |
| `BooleanExpression` | `Left: Value`, `Operator: OperatorWord`, `Right: Value` | `a and b`, `a or b`. |
| `NotExpression` | `Operator: OperatorWord`, `Operand: Value` | `not a`. |
| `OperatorWord` | `Name: string` | One of the word operators. |

`ExpressionNode` descends from `Value` because the grammar admits an expression exactly
where it admits a value, and which of the two was written is decided by what is on the
page rather than by where it is. A comparison with no operator in it **must** produce
the operand's own node rather than a wrapper around it, so a line written before
expressions existed parses to the tree it always did.

`StringConstant.Value` is assigned the literal **as written, with its quotes**. The
setter strips matching pairs of leading and trailing double quotes, records how many it
removed in `QuoteCount`, and keeps the inner text as the value. This is what lets
`""x""` round-trip as `""x""` rather than as `"x"`.

`TagValue` exists because `InstanceTag` already descends from `Tag`, and a record
cannot descend from `Value` as well. It is a wrapper, not a distinct concept.

### Tags

| Node | Fields | Meaning |
| --- | --- | --- |
| `Tag` | abstract | Anything that can appear in a tag body. |
| `InstanceTag` | abstract, descends from `Tag` | A tag that denotes a value. |
| `ObjectInstance` | `VariableName?`, `ObjectType`, `Attributes: TagAttributeList`, `Children: TagList?` | `<name\|type a=1>...</type>`. |
| `ComponentInstance` | `VariableName?`, `ComponentType`, `Attributes: TagAttributeList`, `Children: TagList?` | `{name\|type a=1/}`. |
| `VariableTag` | `Name: VariableName` | `<$name>`. |
| `PropertyAssignment` | `Name: ProperyName`, `Value: SimpleValue?`, `Children: TagList?` | `[name=value]` and its three other forms. |
| `TagAttribute` | `Name: TagAttributeName`, `Value: SimpleValue` | `a=1`. |
| `TagAttributeList` | `Attributes: List<TagAttribute>` | Attributes in order. |
| `TagList` | `Tags: List<Tag>` | Children in order. |
| `ClosingTag` | `TagObjectType: ObjectType?` | A closing tag; null type means the empty form. |

`Children` is `null` for the self-closing form and non-null for a body, including an
empty one. An implementation **must** keep that distinction: it is how `<a/>` and
`<a></a>` stay distinguishable, although they evaluate alike.

`ProperyName` is spelled that way in the source. It is a typo preserved for
compatibility; an implementation **may** correct it, and **must** then treat the two
spellings as the same node type.

### Names

`VariableName`, `ObjectType`, `ComponentType`, `TagAttributeName` and `ProperyName`
each wrap a single string. They are distinct types so a visitor can colour and treat
them differently, which is the whole point of the tree.

## Visitors

`ISemanticTreeVisitor` has a method per node type. `VisitorBase` implements the
traversal and the punctuation, so a subclass overrides only the nodes it cares about.

Two visitors are normative.

### Serialisation

`SerialisationVisitor` flattens a tree back to text.

An implementation **must** satisfy: for any input `s` that parses to tree `t`,
serialising `t` yields `s`, up to the normalisation of whitespace between tokens to a
single space where the grammar allows any amount.

The web host runs this on every parse and returns the result alongside the tokens, so a
lossy parse is detectable from outside. `Parser.Tests/SerialisationTests.cs` and the
round-trip assertions elsewhere enforce it.

### Tokenisation

`TokenStreamVisitor` produces an ordered list of `(Text, Kind)` pairs. Concatenating
every `Text` **must** reproduce the serialised form.

| Kind | Applied to |
| --- | --- |
| `command` | A `CommandName`, and the `Id` of a `FunctionExpression`. |
| `flag` | A `CommandArgumentFlag`, including its dash. |
| `string` | A `StringConstant`, including its delimiters. |
| `variable` | A `VariableReference`, a `VariableName`, a `VariableTag`. |
| `member` | A `MemberName`, including its leading stop. |
| `operator` | An `OperatorWord`. |
| `identifier` | An `Identifier` that is not a command name. |
| `type` | An `ObjectType` or a `ComponentType`. |
| `attribute` | A `TagAttributeName` or a `ProperyName`. |
| `punctuation` | Everything the traversal emits itself: brackets, slashes, commas, pipes, equals. |
| `whitespace` | A run of spaces or tabs. |
| `newline` | A line break, inside a string. |

The function form's name is an `Identifier` in the tree rather than a `CommandName`,
because the grammar reuses the identifier production there. It **must** still be
tokenised as `command`: `write(a, b)` and `write a b` name the same command, and a host
colours by kind alone.

Nesting **must** be respected: a kind applies for the span of its subtree, and
punctuation emitted by a parent stays punctuation. `<size|measurement unit=metres/>`
tokenises as:

```
<          punctuation
size       variable
|          punctuation
measurement type
(space)    whitespace
unit       attribute
=          punctuation
metres     identifier
/>         punctuation
```
