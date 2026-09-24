# Lexical structure and grammar

Normative definition of the concrete syntax. The reference implementation is
`Parser.FParsec/Grammar.fs`; the retained GOLD grammar
`CommandLine/Commands/Parser/Grammar/CommandLineGrammar.grm` is historical and is not
normative.

## Notation

Productions use `::=`, alternation `|`, grouping `( )`, optional `?`, zero or more `*`
and one or more `+`. Terminals are in double quotes.

Alternatives are **ordered**: a parser **must** try them left to right and take the
first that succeeds. This matters in several places, each noted below.

Two lookahead operators appear in the productions below, and neither consumes input:
`&X` requires that `X` follows, and `!X` requires that it does not. They carry the
delimiter rules from [decision 0007](../decisions/0007-notation-conflicts.md), which
resolves the collisions between paths and `/>`, and between flags and negative
numbers, by looking ahead rather than by making a position mean something.

## Character classes

```
IdentifierChar ::= "a".."z" | "A".."Z" | "0".."9" | "_" | Accented
Accented       ::= one of  éèàäëïöüùçâêîôûÇÄÅÉæÆÖÜøØƒáíóúñÑÁÂÀãÃðÐÊËÈiÍÎÏÌÓßÔÒõÕµþÞÚÛÙýÝ
WordStart      ::= IdentifierChar | "." | "\" | "/" | "~" | "*" | "@"
WordChar       ::= IdentifierChar | "." | "\" | "/" | ":" | "~" | "+" | "@" | "%" | "-" | "*"
Space          ::= " " | HT
```

An implementation **must** treat the accented set as exactly the characters listed. It
is the set the original grammar declared, reproduced verbatim; it is idiosyncratic, and
widening it is a language change.

`*` is a word character so that `like` takes a glob without quotes:
`where $row.name like *.txt`. Nothing else in the grammar uses it.

`@` starts a word as well as continuing one
([decision 0050](../decisions/0050-at-names-are-words-and-columns.md)), so a column that
[`pick`](command-catalogue.md#picking-elements) answers is written as it is shown:
`select @tag`, `sort @children`. `echo @` writes `@`, `echo user@host` is one word, and
a tag attribute's value may start with one: `<t a=@x/>`. Nothing else in the grammar
begins with `@`. After a variable's stop, `@` belongs to the member
([Tokens](#tokens)), which is read before any word could be.

Every other printable character — `=`, `,`, `?`, `!`, `#`, `;`, `'`, the brackets and
the quote — is outside both sets, so a word ends in front of it.

`-` is a `WordChar` but not a `WordStart`, with one exception: a `-` **must** start a
word when a digit follows it, so `echo -5` writes minus five. `-flag` is still a flag
and `--flag` is still a syntax error, because neither has a digit after the dash.

`/` is a `WordChar`, but it joins the word it is in only when the next character is
neither `>` nor `}`. A `/` immediately before one of those two brackets ends the word
instead, which is what makes `/>` and `/}` reachable from inside a word and lets
`<file path=documents/notes.txt/>` parse. This rule is stated once here and applies
everywhere a `Word` is recognised.

## Whitespace and lines

Only space and horizontal tab are whitespace. A newline is **not** whitespace: a
program is one line, and a newline reaches the end-of-input rule and fails the parse.

Whitespace is insignificant between tokens and **must not** appear inside an
identifier, a word, a flag or a multi-character delimiter such as `/>`. It **must not**
appear inside an `Assignment` either, on either side of the `=`, and it **must not**
appear between `<` and what follows it, or between a `FunctionExpression`'s name and
its parenthesis.

Whitespace is not *required* between tokens either, wherever the tokens cannot run
together: `echo"hi"` is `echo` and a string, and `a|b` is a pipeline of two. Where two
tokens could run together, such as a command name and a word argument, only a space
separates them.

## Tokens

```
Identifier        ::= IdentifierChar+
CommandName       ::= Identifier ( "-" Identifier )*
Word              ::= ( WordStart | "-" &Digit ) ( WordChar | "/" !( ">" | "}" ) )*
                      -- a leading WordStart of "/" is subject to the same lookahead
Flag              ::= "-" !Digit IdentifierChar+
Digit             ::= "0".."9"
VariableReference ::= "$" Identifier MemberName*
MemberName        ::= "." "@"? Identifier
ReservedWord      ::= "and" | "or" | "not" | "eq" | "ne" | "gt" | "ge" | "lt" | "le"
                    | "like" | "has" | "else" | "try"
StringLiteral     ::= '"""' StringBody '"""'
                    | '""'  StringBody '""'
                    | '"'   StringBody '"'
StringBody        ::= ( any character other than '"' )*
Default           ::= "??"
```

The productions below write `Space*` explicitly wherever whitespace may appear, and it
may appear nowhere else.

String delimiters **must** be tried longest first, so `""""` is one empty
double-delimited string and not two single-delimited ones. This is the first ordered
choice that matters. A literal closes only with a delimiter of its own length, and
when the longer form cannot close the parser falls back to the shorter ones: `"""a""`
is a syntax error, while `"""x"` is the empty string `""` followed by the string `"x"`,
two arguments with no space between them.

A string body cannot contain a double quote and there is no escape character. A string
may contain spaces, tabs, carriage returns and line feeds; it is the only place a line
break may appear.

The three delimiters carry the same value, the body, so `""""` is the empty string and
`"""ab"""` is `ab`. The delimiter count **must** be retained in the tree so that
re-serialisation reproduces the input exactly.

A `Word` **must not** be a `ReservedWord`: the thirteen words above — eight comparison
words, `and`, `or`, `not`, and the recovery keywords `else` and `try` — are reserved in
every position
([decision 0019](../decisions/0019-reserved-words-in-expression-positions.md)). The
match is exact and case-sensitive, so `equals`, `eq.txt` and `Eq` are ordinary words;
only the bare word itself is taken. An implementation **must** report a `Word` that is a
reserved word as a syntax error rather than silently accepting it, at the column the
word starts in, and the message **should** say how to write it as text. The reference
implementation says `'eq' is an operator; write "eq" to pass it as text`, and uses the
same sentence for `try`.

Two of the words never reach that check in argument position, because the grammar reads
them as what they are first: `not` begins a negation, and `else` ends the argument list
(see [Lines, stages and recovery](#lines-stages-and-recovery)). `echo not` and
`echo else` are therefore syntax errors at the end of the line, where the missing
operand or pipeline should have been.
A tag attribute's value is a `Word` position too, so `<t a=eq/>` **must** be refused
as well, at the word's column and with the same message.

A `CommandName` **must not** be a `ReservedWord` either, and an implementation **must**
report one as a syntax error at the column the name starts in; the message **should**
say that the word is reserved and cannot name a command. Before recovery existed this
was harmless — `eq x` reached execution as an unknown command — but `else x` would read
as a command called `else` rather than a line whose first pipeline is missing.

`try` and `else`, like the operators, are matched as whole words: the next character
**must not** be a `WordChar`, so `trying` and `elsewhere` are ordinary names and words.
`??` is the only symbol operator; `?` is not a `WordChar`, so an argument list always
ends in front of it.

Reserved words are the only keywords, and with `??` they are the only things matched
literally at the lexical level. Case is otherwise preserved. Command name resolution is
case-insensitive and happens later, in
[execution](execution-model.md#resolving-a-command).

A `MemberName` carries its own full stop, so `$row.size` is three tokens — a sigil, a
variable name and one member — and re-serialising writes the stop back with the member
it belongs to.

A stop after a variable **must** be followed by an `Identifier`
([decision 0032](../decisions/0032-a-stage-may-be-a-value.md)). Once a `VariableReference`
has read a `.`, the stop belongs to it: an implementation **must not** end the reference
in front of the stop and read the stop as the start of a `Word`. A stop with no name
after it **must** be a syntax error, reported where the name should have been, and its
explanation **should** say what belongs there. The reference implementation says
`a column name belongs after the stop, as in $row.kind`, so `ls | where $row.` fails at
column 16 rather than reaching `where` with a path called `.`. A member may be all
digits, like any identifier: `$x.1` reads a member named `1`.

A member's name **may** begin with `@`, which marks one of a tag's own parts rather than
an attribute: `$v.@tag` and `$v.@children`
([decision 0048](../decisions/0048-a-tags-own-parts-are-read-with-at.md)). The `@` is part
of the member's name, so the tree, the tokens and the evaluator all see `@tag`, and
re-serialising writes `.@tag` back. Any identifier may follow it: `$v.@other` parses, and
what it reads is [execution](execution-model.md#evaluating-a-written-value). Members of
both forms chain, `$d.@children.@tag.x`, and an `@` member may stand in a comparison,
`where $row.@tag eq book`, or begin a value stage, `$v.@children | count`. An attribute
name is an `Identifier`, in the tag notation as in XML, so no attribute can be written
with a name that begins with `@`.

The `@` commits as the stop does. Once a `MemberName` has read `.@`, an `Identifier`
**must** follow, or the parse **must** fail where the name should have been; the
explanation **should** say what belongs there. The reference implementation says
`a name belongs after the @, as in $v.@tag`, so `echo $v.@` and `echo $v.@ x` both fail
at column 9. A stop with nothing after it keeps its own explanation and its one expected
symbol, `column name`. The form is the combinator parser's only; the retained GOLD
grammar has no member access at all.

A `CommandName` is one token, and appears in exactly two places: the name of a CLI
expression and the name of a function expression. The hyphen **must** be adjacent to an
identifier on both sides, so `save-view` is one name while `ls -l` is a name and a
flag and `echo -5` is a name and a negative number
([decision 0022](../decisions/0022-hyphenated-command-names.md)). The hyphen is not an
`IdentifierChar`, so nothing else in the grammar — a variable, an attribute, a
parameter name — admits one.

## Grammar

```
Program              ::= Space* ( EOF | Line Space* EOF )

Line                 ::= PipedCommandList ( "else" Space* PipedCommandList )*

PipedCommandList     ::= Stage ( "|" Space* Stage )*

Stage                ::= ( "try" Space* )? CommandExpression ( Default Space* Operand )?

CommandExpression    ::= ( FunctionExpression
                         | CliExpression
                         | InstanceTag
                         | "(" Space* PipedCommandList ")"
                         | VariableReference ) Space*

FunctionExpression   ::= CommandName "(" Space* FunctionArgumentList Space* ")" Space*
FunctionArgumentList ::= ( FunctionArgument ( "," Space* FunctionArgument )* )?
FunctionArgument     ::= ( Identifier Space* ":" Space* Expression
                         | Expression ) Space*

CliExpression        ::= CommandName Space* ( !"else" CommandArgument )*
CommandArgument      ::= ( Flag
                         | Assignment
                         | Expression ) Space*

Assignment           ::= Identifier "=" ArgumentValue

Expression           ::= OrExpr
OrExpr               ::= AndExpr ( "or" Space* AndExpr )*
AndExpr              ::= NotExpr ( "and" Space* NotExpr )*
NotExpr              ::= "not" Space* NotExpr | Comparison
Comparison           ::= Operand ( CompareOp Space* RightOperand )?
RightOperand         ::= Operand
CompareOp            ::= "eq" | "ne" | "gt" | "ge" | "lt" | "le" | "like" | "has"
Operand              ::= ArgumentValue Space*
                       | "(" Space* PipedCommandList ")" Space*

ArgumentValue        ::= InstanceTag
                       | ArgumentSimpleValue
ArgumentSimpleValue  ::= StringLiteral | VariableReference | Word
Value                ::= InstanceTag | SimpleValue
SimpleValue          ::= StringLiteral | VariableReference | Identifier

InstanceTag          ::= ( VariableTag | ObjectInstance | ComponentInstance ) Space*

ObjectInstance       ::= "<" &TagOpener ( Identifier Space* "|" Space* )? Identifier Space* TagAttribute*
                         ( "/>" | ">" Space* Tag* ClosingObjectTag )
TagOpener            ::= IdentifierChar | "$" | "/"
ClosingObjectTag     ::= "</" Space* ( ">" | Identifier Space* ">" )

ComponentInstance    ::= "{" Space* ( Identifier Space* "|" Space* )? Identifier Space* TagAttribute*
                         ( "/}" | "}" Space* Tag* ClosingComponentTag )
ClosingComponentTag  ::= "{/" Space* ( "}" | Identifier Space* "}" )

VariableTag          ::= "<$" Space* Identifier Space* ">"

TagAttribute         ::= Identifier Space* "=" Space* ArgumentSimpleValue Space*

Tag                  ::= ( PropertyAssignment
                         | VariableTag
                         | ObjectInstance
                         | ComponentInstance ) Space*

PropertyAssignment   ::= "[" Space* Identifier Space*
                         ( "=" Space* SimpleValue Space* "]"
                         | "]" Space* "=" Space* InstanceTag
                         | "]" Space* Tag* "[/" Space* ( "]" | Identifier Space* "]" ) )
```

`FunctionArgument` has no `Flag` and no `Assignment`: in the function form an argument
is named with `name: value` or given positionally, and nothing else. `name: value` is
likewise only a function-form notation; in the command line form `name:` is a word,
because `:` is a `WordChar`.

A `CommandName` may be all digits, like any identifier: `42` parses as a command named
`42`, and fails later as an unknown command.

In the tag productions the first `Identifier` before `|` is a variable name and the
second is the type; in `TagAttribute` it is an attribute name; in `PropertyAssignment`
it is a property name. The tree distinguishes them; the grammar does not.

A closing tag's name, when written, is checked against the opening tag's after the
production has matched; see [Errors](#errors). `[/` accepts any property name, or none,
and does not check it.

### Where words are allowed

`ArgumentSimpleValue` admits a `Word`; `SimpleValue` does not. Therefore:

- A command argument **may** be a bare word: `read notes.txt`, `in ../docs`,
  `download https://host/f.txt`.
- A tag attribute's value **may** be a bare word too, including a path:
  `<file path=documents/notes.txt/>`. The lookahead on `/` is what keeps the final
  slash the tag's rather than the path's.
- A `PropertyAssignment`'s value still takes a `SimpleValue` only, so `[name=a/b]`
  is not a word. Nothing has asked for it to be one.

An earlier version of this specification required a tag attribute's value to be quoted
and said that widening it made `<thing path=a/b/>` ambiguous. It is not ambiguous once
`/>` is recognised by lookahead: the only `/` that can end a word is one with `>` or
`}` after it, and a path's interior slashes never are.

### Expressions

An `Expression` whose `Comparison` has no `CompareOp`, and which is neither negated nor
combined, **must** produce the operand's own node rather than a wrapper around it. A
line written before expressions existed therefore parses to exactly the tree it always
did, and only a line that actually writes an operator produces an expression node.

An operator **must** be followed by something that is not a `WordChar`, so `eq` is an
operator and `equals` is a word. This is the fourth ordered choice that matters.

Once a `CompareOp` is written, its `RightOperand` **must** follow. When nothing that
could begin an operand is there, an implementation **must** fail at the position where
the operand should begin, and the explanation **should** name the operator and give an
example of what it compares with. The reference implementation says
`eq needs a value to compare with, such as folder`: the example is `10` for `gt`, `ge`,
`lt` and `le`, `"*.txt"` for `like`, and `folder` for the rest. An operand that began
and went wrong keeps its own error: a string that never closes, and a reserved word
written as the value, are reported as such.

`and` binds tighter than `or`, and both are left associative. `not` takes the whole
`NotExpr` after it, so `not $row.kind eq folder` negates the comparison rather than its
left operand. Expressions have no symbol operators and no parenthesised
sub-expressions: a parenthesis in operand position is a nested pipeline, not a
grouping.

Which parameter an `Expression` reaches is [binding](execution-model.md#argument-binding),
not grammar: an expression that wrote an operator binds only to a parameter declared
`Predicate`.

### Lines, stages and recovery

`else` binds looser than `|`: each side of it is a whole `PipedCommandList`, so
`a | b else c | d` is `(a | b) else (c | d)`. A `Line` with no `else` **must** produce
the `PipedCommandList` itself rather than a line of one, so every tree written before
recovery existed is unchanged. `else` is not an argument: a `CliExpression`'s argument
list **must** end in front of it rather than reporting it as a reserved word, which is
the fifth ordered choice that matters.

`try` and the `Default` belong to one `Stage`, not to the pipeline. `try read x | set p`
marks `read x` alone, and `first (ls) ?? none | set p` defaults what `first` answered.
One `Default` per stage: `a ?? b ?? c` is a syntax error rather than a chain. The
default is an `Operand`, not an `Expression`, so an operator after it is a syntax error
too.

A `Stage` is a command, a tag, a pipeline in parentheses or a variable reference
([decision 0032](../decisions/0032-a-stage-may-be-a-value.md)). The last is a *value
stage*: `$files`, `$problem.kind`, `$files | where $row.size gt 10` and
`$maybe ?? "default"` are lines, and `try` and `??` apply to a value stage as to any
other. It is the fifth alternative, and takes nothing away from the other four, because
`$` can begin no command name, tag or parenthesis. A value stage has no arguments:
`$v x` is a syntax error at column 3, where the stage should have ended. A string or a
word cannot stand as a stage, so `"x"` is a syntax error at column 0.

A `FunctionExpression`'s opening parenthesis **must** be adjacent to its `CommandName`.
With `Space` between them the parenthesis is not a call: it begins an `Operand`, which
is a nested pipeline, so `first(ls)` calls `first` with the word `ls` and `first (ls)`
calls `first` with whatever the pipeline `ls` answers
([decision 0023](../decisions/0023-adjacent-function-parenthesis.md)). A nested pipeline
contains a `PipedCommandList`, not a `Line`: `else` inside parentheses is a syntax error.
Nested pipelines nest: `echo (echo (ls | count))` is three pipelines deep.

A pipeline in parentheses may appear in exactly three places: as a whole stage
(`ls | (where $row.kind eq folder | count)`), as an `Operand` — an argument, either side
of a comparison, or under `and`, `or` or `not` — and as the operand of a `Default`. A tag
attribute and a `PropertyAssignment` take a simple value, so neither can hold one.

What these mean at run time is [execution](execution-model.md#recovery), not grammar.

### Assignments

`Assignment` is `name=value` with no spaces, and it is **data**: a name paired with a
value, carried through to the command. It **must not** bind a declared parameter;
`name: value` is the only notation that does. See
[decision 0017](../decisions/0017-assignment-arguments.md).

An implementation **must** try `Assignment` before `ArgumentValue`, because an
`ArgumentValue` would otherwise take the name as a `Word` and leave `=value` behind.
This is the third ordered choice that matters.

### The empty program

`Program` tries end-of-input **before** the command list. This is the second ordered
choice that matters, and it is not merely an optimisation: trying the command list
first and backtracking discards the failure position, and every syntax error is then
reported at column zero.

An implementation **must** report the position where the input actually stopped making
sense.

## Errors

A failed parse **must** produce one of:

| Kind | Carries | Raised when |
| --- | --- | --- |
| Syntax error | line, column, expected symbols, and an optional explanation | The input stopped matching. |
| Lexical error | a syntax error, wrapped | A character cannot begin any token. |
| Messages | a list of strings | A structural rule failed, such as a mismatched closing tag. |

The explanation is a sentence the grammar supplies when it knows *why* the input is
wrong rather than only what could have appeared. The reference implementation gives one
in five cases: a reserved word used as an argument, a reserved word used as a command
name, a stop after a variable with no name after it, an `@` after a variable's stop with
no name after it, and a comparison operator with nothing to compare with. A host **should** show the explanation in preference to the
expected symbols when there is one, and **should** say the expected symbols in words
rather than as labels (see
[Parse response](host-interfaces.md#parse-response)).

The lexical error kind is kept for parsers with a separate tokeniser. The reference
implementation has none: an unexpected character is a syntax error at its position, and
it never produces a lexical error.

`Line` is zero-based. `Column` is the zero-based offset from the start of the input,
not from the start of the line; for the single-line programs this grammar accepts, the
two coincide.

Expected symbols are the labels the grammar could have accepted at that position, for
example `identifier`, `argument`, `end of input`, `"`, `$`, `<`, `<$`, `{`, `(`, `|`,
`/>`, `>`, `??`, `try`, `else`, `not` or a comparison word. A name the grammar reads in
a particular place is labelled for what it names: `variable name` after `$`,
`column name` after a variable's stop, `tag type` after `<` or `{`, `attribute name`
inside a tag and `property name` inside a property assignment. An implementation
**should** report them; the set itself is not normative.

A closing tag whose type differs from its opening tag **must** fail with the message
`Closing tag 'X' does not match opening tag 'Y'`. The empty closing forms `</>` and
`{/}` always match.

### Worked error positions

| Input | Result |
| --- | --- |
| `<thing` | Syntax error at column 6, expecting `attribute name`, `/>`, `>` |
| `{renderer` | Syntax error at column 9, expecting `attribute name`, `/}`, `}` |
| `echo --double` | Syntax error at column 5 |
| `mkdir \|` | Syntax error at column 7 |
| `[size=3]` | Syntax error at column 0 |
| `else echo x` | Syntax error at column 0: `'else' is a reserved word and cannot name a command` |
| `echo a \| else` | Syntax error at column 9, with the same explanation |
| `echo eq` | Syntax error at column 5: `'eq' is an operator; write "eq" to pass it as text` |
| `echo not` | Syntax error at column 8, expecting an operand |
| `read x else` | Syntax error at column 11 |
| `echo (a else b)` | Syntax error at column 8, expecting `)`, `??`, `\|` |
| `first ?? a ?? b` | Syntax error at column 11, expecting `end of input`, `else`, `\|` |
| `$maybe ?? "default"` | A line: a value stage with a default |
| `"x"` | Syntax error at column 0 |
| `$` | Syntax error at column 1, expecting `variable name` |
| `$v x` | Syntax error at column 3, expecting `end of input`, `??`, `else`, `\|` |
| `$row.` | Syntax error at column 5: `a column name belongs after the stop, as in $row.kind` |
| `ls \| where $row.` | Syntax error at column 16, with the same explanation |
| `echo $v.@` | Syntax error at column 9: `a name belongs after the @, as in $v.@tag` |
| `echo $v.@ x` | Syntax error at column 9, with the same explanation |
| `ls \| where $row.kind eq` | Syntax error at column 23: `eq needs a value to compare with, such as folder` |
| `ls \| where $row.size gt` | Syntax error at column 23: `gt needs a value to compare with, such as 10` |
| `ls \| where $row.name like` | Syntax error at column 25: `like needs a value to compare with, such as "*.txt"` |
| `<a></b>` | Message: `Closing tag 'b' does not match opening tag 'a'` |

## Entry points besides a program

Two further entry points parse a fragment rather than a line.

```
IdentifierOnly ::= Space* Identifier Space* EOF
ExpressionOnly ::= Space* Expression Space* EOF
```

An implementation **must** expose `IdentifierOnly` so a host can ask whether text is a
valid name without running a command.

An implementation **must** expose `ExpressionOnly` if it supports saved views, because
a view is a record whose content is a predicate with no command line around it, and
reading one back **must** use the same `Expression` rule that read it in the first
place.
