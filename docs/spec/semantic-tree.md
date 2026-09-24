# Semantic tree

The tree a parse produces, the visitors over it, and the two guarantees that make the
tree worth having: it round-trips to the source text, and every token knows its role.

Types live in `Commands.Parser.SemanticTree` (project `Parser.Tree`). Every node below
is a record and implements `IVisitable`, except the abstract bases, which declare
`Accept` abstract, and `ClosingTag`, noted below.

## Node catalogue

### Roots

| Node | Fields | Meaning |
| --- | --- | --- |
| `RootNode` | abstract | What a parse returns. |
| `EmptyCommand` | none | Empty or whitespace-only input. |
| `PipedCommandList` | `OrderedCommands: List<CommandExpression>` | One or more commands, left to right. |
| `RecoveryLine` | `Pipelines: List<PipedCommandList>` | Two or more pipelines joined by `else`. A line without `else` is a `PipedCommandList`, never a line of one. |

### Command expressions

| Node | Fields | Meaning |
| --- | --- | --- |
| `CommandExpression` | `Expression: OneOf<FunctionExpression, CommandExpressionCli, InstanceTag, NestedPipeline, VariableReference>`, `Try: bool`, `Default: Value?` | One stage of a pipeline: what it runs, whether it was written with `try`, and the operand after `??`, if any. |
| `FunctionExpression` | `Id: Identifier`, `Arguments: CommandArguments` | `name(a, b: c)`. |
| `CommandExpressionCli` | `Name: CommandName`, `Arguments: CommandArguments` | `name a b`. |
| `CommandName` | `Name: string` | A command name in command line form, hyphens included: `save-view`. |

An `InstanceTag` in `CommandExpression` is the tag form: the stage produces a value
without calling a command. A `NestedPipeline` there is a pipeline in parentheses
standing as a stage. A `VariableReference` there is a value stage
([decision 0032](../decisions/0032-a-stage-may-be-a-value.md)): `$files` and
`$problem.kind` standing where a command would. It is the same node an argument holds,
because the two are read the same way; only where it stands differs, so a consumer that
walks stages **must** handle the fifth case rather than assume that every stage names a
command or builds a tag.

### Arguments

| Node | Fields | Meaning |
| --- | --- | --- |
| `CommandArguments` | `Arguments: List<CommandArgument>` | Ordered arguments as written. |
| `CommandArgument` | abstract | Base. |
| `CommandArgumentValue` | `Value: Value` | A value in command line form. |
| `RequiredCommandArgument` | `Value: Value` | A positional value in function form. |
| `OptionalCommandArgument` | `Name: OneOf<CommandArgumentFlag, Identifier>`, `Value: Value` | `name: value`, in function form. |
| `CommandArgumentFlag` | `Name: string` | `-flag`. The setter strips leading dashes, so the name is stored without them. |
| `AssignmentArgument` | `Name: Identifier`, `Value: Value` | `name=value`, in command line form: data for the command, not a parameter binding ([decision 0017](../decisions/0017-assignment-arguments.md)). |

The grammar only ever builds an `OptionalCommandArgument` whose name is an
`Identifier`; the flag alternative of the `OneOf` is kept for the tree's other
producers and is not reachable from text.

### Values

| Node | Fields | Meaning |
| --- | --- | --- |
| `Value` | abstract | Base. |
| `SimpleValue` | abstract | A value that is not a tag. |
| `Identifier` | `Name: string` | A bare word or identifier, as written. Numbers are words too; what a word means is decided at [binding](execution-model.md#evaluating-a-written-value). A word may begin with `@`, so `select @tag` has the `Identifier` `@tag` ([decision 0050](../decisions/0050-at-names-are-words-and-columns.md)). |
| `VariableReference` | `Name: VariableName`, `Members: List<MemberName>` | `$name`, or `$name.member`. |
| `MemberName` | `Name: string` | The `.size` of `$row.size`. It carries its own stop, and its name is never empty: a stop with no name after it does not parse ([Tokens](lexical-grammar.md#tokens)). A member written with `@` keeps it in its name: `.@tag` is the `MemberName` `@tag` ([decision 0048](../decisions/0048-a-tags-own-parts-are-read-with-at.md)). |
| `Constant` | abstract | Base for literals. |
| `StringConstant` | `Value: string`, `QuoteCount: int`, `QuoteString: string` | A string literal. |
| `TagValue` | `Tag: InstanceTag` | A tag used where a value is expected. |
| `NestedPipeline` | `Pipeline: PipedCommandList` | A pipeline in parentheses, where a value is expected, or standing as a stage. |

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

A `ComparisonExpression` always has both sides. An operator with nothing after it to
compare with does not parse, and produces a syntax error that names the operator
([Expressions](lexical-grammar.md#expressions)) rather than a node with a null `Right`.

A `StringConstant` carries its body as `Value`, the number of quotes in its delimiter as
`QuoteCount`, and the delimiter itself as `QuoteString`. The combinator grammar builds
one with `StringConstant.Delimited(quoteCount, body)`, from the delimiter it matched, so
`""""` is the empty string with a count of two and `"""ab"""` is `ab` with a count of
three. The count is what lets `""x""` round-trip as `""x""` rather than as `"x"`.

The `Value` setter is kept for the retained GOLD interpreter, which hands the node the
literal with its quotes: it strips one quote from each end, repeatedly, while the rest
starts and ends with one and is longer than twice the pairs already stripped. That
guess stops early on a short body, which is why the combinator grammar does not use it.

`VariableName`'s setter strips a leading `$` in the same spirit, so the name never
carries its sigil.

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

An object or component tag's `Children` is `null` when it has no children, whether it
was written self-closing or with an empty body: `<a/>`, `<a></a>` and `<a></>` produce
the same tree, and `HasChildren` is how a consumer asks. The empty body is a spelling,
not a distinct value, and it serialises in the self-closing form.

A `PropertyAssignment` sets `Value` for `[name=value]` and `Children` for the tag
forms. `[name]=<tag>` and `[name]<tag>[/name]` produce the same tree, a one-element
`Children`. `[name][/name]` produces an empty, non-null `Children` and a null `Value`.

`ClosingTag` (`TagObjectType: ObjectType?`) is in the assembly but is not a node of
this tree: it is not visitable and the grammar never builds one. It is what the
retained GOLD interpreter reduces a closing tag to; the combinator grammar compares the
closing name while parsing instead.

`ProperyName` is spelled that way in the source. It is a typo preserved for
compatibility; an implementation **may** correct it, and **must** then treat the two
spellings as the same node type.

### Names

`VariableName`, `ObjectType`, `ComponentType`, `TagAttributeName` and `ProperyName`
each wrap a single string. They are distinct types so a visitor can colour and treat
them differently, which is the whole point of the tree.

## Visitors

`ISemanticTreeVisitor` has a `Visit` method per concrete node type — the abstract bases
have none, and dispatch goes to the concrete type through `Accept`. `VisitorBase`
(`Parser.Tree/Serialisation`) implements the traversal and writes the punctuation,
through four abstract members — `Append(string)`, `Append(char)`, `AppendNewLine()` and
`GetResult()` — so a subclass supplies an output and overrides only the nodes it cares
about. Two options, `UseIdentation` and `UseNewLineOnPipe`, lay a tree out over several
lines for display; both are off by default, and neither is used for a round trip.

Two visitors are normative.

### Serialisation

`SerialisationVisitor` flattens a tree back to text.

An implementation **must** satisfy, for any input `s` that parses to tree `t`:

- serialising `t` yields text that parses to a tree equal to `t`;
- serialising is idempotent: serialising the tree of the serialised text yields the
  same text again;
- when `s` is already in the normal form below, serialising `t` yields `s` exactly.

The normal form is what the serialiser writes:

- one space between command line arguments, on both sides of `|`, `else` and `??`,
  after `try`, and on both sides of a word operator;
- `, ` between function arguments and `: ` after an argument's name, with the
  parenthesis tight against the function's name and nothing inside the parentheses of a
  call or a nested pipeline;
- `name=value` with no space, for both an assignment and a tag attribute;
- inside a tag, one space between the type and the first attribute and between
  attributes, and nothing else: children follow one another with no separator;
- a tag with no children in the self-closing form; a closing tag with its type named,
  so `</>` becomes `</type>` and `[/]` becomes `[/name]`; `[name]=<tag>` as
  `[name]<tag>[/name]`; and `<$ name>` as `<$name>`;
- a string with the delimiter it was written with.

So `f( a , b )` serialises as `f(a, b)`, `a|b` as `a | b` and `echo"hi"` as `echo "hi"`.

The web host runs this on every parse and returns the result alongside the tokens, so a
lossy parse is detectable from outside. `Parser.Tests/SerialisationTests.cs`, which
also pins the normal forms, and the round-trip assertions elsewhere enforce it. A property with an empty tag body,
`<t>[p][/p]</t>`, is the list form with an empty list, and `HasChildren` is true for it
because `Children` is not null.

### Tokenisation

`TokenStreamVisitor` (`Web.Core/TokenStreamVisitor.cs`, namespace
`CommandLineReimagined.Web.Tokenisation`) produces an ordered list of `(Text, Kind)`
pairs. Concatenating every `Text` **must** reproduce the serialised form.

| Kind | Applied to |
| --- | --- |
| `command` | A `CommandName`, and the `Id` of a `FunctionExpression`. |
| `flag` | A `CommandArgumentFlag`, including its dash. |
| `string` | A `StringConstant`, including its delimiters. |
| `variable` | A `VariableReference` including its `$`, a `VariableName`, and a `VariableTag` including its `<$` and `>`. |
| `member` | A `MemberName`, including its leading stop. |
| `operator` | An `OperatorWord`, and the `??` of a default. |
| `keyword` | The `try` in front of a stage and the `else` between pipelines. |
| `identifier` | An `Identifier` that is not a command name, including the name of a `name: value` argument. |
| `type` | An `ObjectType` or a `ComponentType`, in an opening tag and in a closing one. |
| `attribute` | A `TagAttributeName`, a `ProperyName`, and the name of an `AssignmentArgument`. |
| `punctuation` | Everything the traversal emits itself: brackets, slashes, commas, colons, pipes, equals. |
| `whitespace` | Text that is only whitespace. |
| `newline` | A line break the visitor writes when laying a tree out over several lines. |

`try`, `else` and `??` are not nodes: nothing about them varies. `VisitorBase` writes
them through two hooks, `AppendKeyword` and `AppendOperator`, which a tokenising
visitor overrides to give them their kinds.

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

A value stage tokenises as the variable reference it is, whether it stands as a stage
or as an argument: `$problem.kind` is `$` and `problem` as `variable`, then `.` and
`kind` as `member`.

A member written with `@` **must** tokenise exactly as a plain member does, so a host
colours `$v.@tag` as it colours `$v.a`: `$` and `v` as `variable`, then `.` and `@tag` as
`member`. The `@` is in the member's token, never a token of its own. A word that begins
with `@`, as in `select @tag`, is an `identifier` like any other word.

A token is what one write of the traversal produced, not a whole lexeme, and adjacent
tokens are not merged. A host **must not** assume one token per lexeme: `$row.size` is
four tokens (`$`, `row` as `variable`; `.`, `size` as `member`), `-l` is two `flag`
tokens, a string is its opening delimiter, its body and its closing delimiter, and the
pipe is the single `punctuation` token `| `, with the space after it.

Two consequences of that rule are visible in the reference implementation. A line
break inside a string is part of the string's body token, not a `newline` token; the
web host never lays a tree out over several lines, so it never emits `newline`. And a
write that is only whitespace is tagged `whitespace` by its text, except inside a
string, so the body of `" "` is `string`.
