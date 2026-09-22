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
WordStart      ::= IdentifierChar | "." | "\" | "/" | "~"
WordChar       ::= IdentifierChar | "." | "\" | "/" | ":" | "~" | "+" | "@" | "%" | "-"
Space          ::= " " | HT
```

An implementation **must** treat the accented set as exactly the characters listed. It
is the set the original grammar declared, reproduced verbatim; it is idiosyncratic, and
widening it is a language change.

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
appear between `<` and what follows it.

## Tokens

```
Identifier        ::= IdentifierChar+
CommandName       ::= Identifier ( "-" Identifier )*
Word              ::= ( WordStart | "-" &Digit ) ( WordChar | "/" !( ">" | "}" ) )*
                      -- a leading WordStart of "/" is subject to the same lookahead
Flag              ::= "-" !Digit IdentifierChar+
Digit             ::= "0".."9"
VariableReference ::= "$" Identifier MemberName*
MemberName        ::= "." Identifier
ReservedWord      ::= "and" | "or" | "not" | "eq" | "ne" | "gt" | "ge" | "lt" | "le"
                    | "like" | "has" | "else" | "try"
StringLiteral     ::= '"""' StringBody '"""'
                    | '""'  StringBody '""'
                    | '"'   StringBody '"'
StringBody        ::= ( any character other than '"' )*
Default           ::= "??"
```

String delimiters **must** be tried longest first, so `""""` is one empty
double-delimited string and not two single-delimited ones. This is the first ordered
choice that matters.

A string body cannot contain a double quote and there is no escape character. A string
may contain spaces, tabs, carriage returns and line feeds.

The three delimiters carry the same value. The delimiter count **must** be retained in
the tree so that re-serialisation reproduces the input exactly.

A `Word` **must not** be a `ReservedWord`: the thirteen words above are the operators
and the recovery keywords, and they are reserved in every position
([decision 0019](../decisions/0019-reserved-words-in-expression-positions.md)). The
match is exact and case-sensitive, so `equals`, `eq.txt` and `Eq` are ordinary words;
only the bare word itself is taken. An implementation **must** report a `Word` that is a
reserved word as a syntax error rather than silently accepting it, and the message
**should** say how to write it as text: `echo "eq"`.

A `CommandName` **must not** be a `ReservedWord` either, and an implementation **must**
report one as a syntax error at the column the name starts in; the message **should**
say that the word is reserved and cannot name a command. Before recovery existed this
was harmless — `eq x` reached execution as an unknown command — but `else x` would read
as a command called `else` rather than a line whose first pipeline is missing.

`try` and `else` are matched as whole words: the next character **must not** be a
`WordChar`, so `trying` and `elsewhere` are ordinary names and words. `??` is the only
symbol operator; `?` is not a `WordChar`, so an argument list always ends in front of
it.

Reserved words are the only keywords, and with `??` they are the only things matched
literally at the lexical level. Case is otherwise preserved. Command name resolution is
case-insensitive and happens later, in
[execution](execution-model.md#resolving-a-command).

A `MemberName` carries its own full stop, so `$row.size` is three tokens — a sigil, a
variable name and one member — and re-serialising writes the stop back with the member
it belongs to.

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

CommandExpression    ::= FunctionExpression
                       | CliExpression
                       | InstanceTag
                       | "(" Space* PipedCommandList ")" Space*

FunctionExpression   ::= CommandName "(" Space* FunctionArgumentList Space* ")" Space*
FunctionArgumentList ::= ( FunctionArgument ( "," Space* FunctionArgument )* )?
FunctionArgument     ::= Identifier Space* ":" Space* Expression
                       | Expression

CliExpression        ::= CommandName Space* ( !"else" CommandArgument )*
CommandArgument      ::= Flag
                       | Assignment
                       | Expression

Assignment           ::= Identifier "=" ArgumentValue

Expression           ::= OrExpr
OrExpr               ::= AndExpr ( "or" Space* AndExpr )*
AndExpr              ::= NotExpr ( "and" Space* NotExpr )*
NotExpr              ::= "not" Space* NotExpr | Comparison
Comparison           ::= Operand ( CompareOp Space* Operand )?
CompareOp            ::= "eq" | "ne" | "gt" | "ge" | "lt" | "le" | "like" | "has"
Operand              ::= ArgumentValue Space*
                       | "(" Space* PipedCommandList ")" Space*

ArgumentValue        ::= InstanceTag
                       | ArgumentSimpleValue
ArgumentSimpleValue  ::= StringLiteral | VariableReference | Word
Value                ::= InstanceTag | SimpleValue
SimpleValue          ::= StringLiteral | VariableReference | Identifier

InstanceTag          ::= VariableTag | ObjectInstance | ComponentInstance

ObjectInstance       ::= "<" &TagOpener ( Identifier Space* "|" Space* )? Identifier Space* TagAttribute*
                         ( "/>" | ">" Space* Tag* ClosingObjectTag )
TagOpener            ::= IdentifierChar | "$" | "/"
ClosingObjectTag     ::= "</" Space* ( ">" | Identifier Space* ">" )

ComponentInstance    ::= "{" Space* ( Identifier Space* "|" Space* )? Identifier Space* TagAttribute*
                         ( "/}" | "}" Space* Tag* ClosingComponentTag )
ClosingComponentTag  ::= "{/" Space* ( "}" | Identifier Space* "}" )

VariableTag          ::= "<$" Identifier Space* ">"

TagAttribute         ::= Identifier Space* "=" Space* ArgumentSimpleValue Space*

Tag                  ::= PropertyAssignment
                       | VariableTag
                       | ObjectInstance
                       | ComponentInstance

PropertyAssignment   ::= "[" Space* Identifier Space*
                         ( "=" Space* SimpleValue Space* "]"
                         | "]" Space* "=" Space* InstanceTag
                         | "]" Space* Tag* "[/" ( "]" | Identifier Space* "]" ) )
```

In the tag productions the first `Identifier` before `|` is a variable name and the
second is the type; in `TagAttribute` it is an attribute name; in `PropertyAssignment`
it is a property name. The tree distinguishes them; the grammar does not.

### Where words are allowed

`ArgumentSimpleValue` admits a `Word`; `SimpleValue` does not. Therefore:

- A command argument **may** be a bare word: `cat notes.txt`, `cd ../docs`,
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

`and` binds tighter than `or`, and both are left associative. `not` takes the whole
`NotExpr` after it, so `not $row.kind eq folder` negates the comparison rather than its
left operand. There are no symbol operators and no parenthesised sub-expressions: a
parenthesis in operand position is a nested pipeline, not a grouping.

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

`try` and the `Default` belong to one `Stage`, not to the pipeline. `try cat x | set p`
marks `cat x` alone, and `first (ls) ?? none | set p` defaults what `first` answered.
One `Default` per stage: `a ?? b ?? c` is a syntax error rather than a chain.

A `FunctionExpression`'s opening parenthesis **must** be adjacent to its `CommandName`.
With `Space` between them the parenthesis is not a call: it begins an `Operand`, which
is a nested pipeline, so `first(ls)` calls `first` with the word `ls` and `first (ls)`
calls `first` with whatever the pipeline `ls` answers
([decision 0023](../decisions/0023-adjacent-function-parenthesis.md)). A nested pipeline
contains a `PipedCommandList`, not a `Line`: `else` inside parentheses is a syntax error.

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
| Syntax error | line, column, expected symbols | The input stopped matching. |
| Lexical error | the same, wrapped | A character cannot begin any token. |
| Messages | a list of strings | A structural rule failed, such as a mismatched closing tag. |

`Line` is zero-based. `Column` is the zero-based offset from the start of the input,
not from the start of the line; for the single-line programs this grammar accepts, the
two coincide.

Expected symbols are the labels the grammar could have accepted at that position, for
example `identifier`, `argument`, `end of input`, `"`, `<`, `<$`, `{`, `|`, `/>`, `>`.
An implementation **should** report them; the set itself is not normative.

A closing tag whose type differs from its opening tag **must** fail with the message
`Closing tag 'X' does not match opening tag 'Y'`. The empty closing forms `</>` and
`{/}` always match.

### Worked error positions

| Input | Result |
| --- | --- |
| `<thing` | Syntax error at column 6, expecting `identifier`, `/>`, `>` |
| `{renderer` | Syntax error at column 9, expecting `identifier`, `/}`, `}` |
| `echo --double` | Syntax error at column 5 |
| `mkdir \|` | Syntax error at column 7 |
| `[size=3]` | Syntax error at column 0 |
| `else echo x` | Syntax error at column 0: `'else' is a reserved word and cannot name a command` |
| `cat x else` | Syntax error at column 10 |
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
