# Lexical structure and grammar

Normative definition of the concrete syntax. The reference implementation is
`Parser.FParsec/Grammar.fs`; the retained GOLD grammar
`CommandLine/Commands/Parser/Grammar/CommandLineGrammar.grm` is historical and is not
normative.

## Notation

Productions use `::=`, alternation `|`, grouping `( )`, optional `?`, zero or more `*`
and one or more `+`. Terminals are in double quotes.

Alternatives are **ordered**: a parser **must** try them left to right and take the
first that succeeds. This matters in two places, both noted below.

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

`-` is a `WordChar` but not a `WordStart`, which is what keeps `-flag` a flag and makes
`--flag` a syntax error.

## Whitespace and lines

Only space and horizontal tab are whitespace. A newline is **not** whitespace: a
program is one line, and a newline reaches the end-of-input rule and fails the parse.

Whitespace is insignificant between tokens and **must not** appear inside an
identifier, a word, a flag or a multi-character delimiter such as `/>`.

## Tokens

```
Identifier        ::= IdentifierChar+
Word              ::= WordStart WordChar*
Flag              ::= "-" IdentifierChar+
VariableReference ::= "$" Identifier
StringLiteral     ::= '"""' StringBody '"""'
                    | '""'  StringBody '""'
                    | '"'   StringBody '"'
StringBody        ::= ( any character other than '"' )*
```

String delimiters **must** be tried longest first, so `""""` is one empty
double-delimited string and not two single-delimited ones. This is the first ordered
choice that matters.

A string body cannot contain a double quote and there is no escape character. A string
may contain spaces, tabs, carriage returns and line feeds.

The three delimiters carry the same value. The delimiter count **must** be retained in
the tree so that re-serialisation reproduces the input exactly.

There are no keywords, so nothing is matched case-insensitively at the lexical level.
Case is preserved. Command name resolution is case-insensitive and happens later, in
[execution](execution-model.md#resolving-a-command).

## Grammar

```
Program              ::= Space* ( EOF | PipedCommandList Space* EOF )

PipedCommandList     ::= CommandExpression ( "|" Space* CommandExpression )*

CommandExpression    ::= FunctionExpression
                       | CliExpression
                       | InstanceTag

FunctionExpression   ::= Identifier Space* "(" Space* FunctionArgumentList Space* ")" Space*
FunctionArgumentList ::= ( FunctionArgument ( "," Space* FunctionArgument )* )?
FunctionArgument     ::= Identifier Space* ":" Space* ArgumentValue
                       | ArgumentValue

CliExpression        ::= Identifier Space* CommandArgument*
CommandArgument      ::= Flag
                       | ArgumentValue

ArgumentValue        ::= InstanceTag
                       | StringLiteral
                       | VariableReference
                       | Word
Value                ::= InstanceTag | SimpleValue
SimpleValue          ::= StringLiteral | VariableReference | Identifier

InstanceTag          ::= VariableTag | ObjectInstance | ComponentInstance

ObjectInstance       ::= "<" Space* ( Identifier Space* "|" Space* )? Identifier Space* TagAttribute*
                         ( "/>" | ">" Space* Tag* ClosingObjectTag )
ClosingObjectTag     ::= "</" Space* ( ">" | Identifier Space* ">" )

ComponentInstance    ::= "{" Space* ( Identifier Space* "|" Space* )? Identifier Space* TagAttribute*
                         ( "/}" | "}" Space* Tag* ClosingComponentTag )
ClosingComponentTag  ::= "{/" Space* ( "}" | Identifier Space* "}" )

VariableTag          ::= "<$" Identifier Space* ">"

TagAttribute         ::= Identifier Space* "=" Space* SimpleValue Space*

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

`ArgumentValue` admits a `Word`; `Value` and `SimpleValue` do not. Therefore:

- A command argument **may** be a bare word: `cat notes.txt`, `cd ../docs`,
  `download https://host/f.txt`.
- A tag attribute's value **must not** be a bare word, because `/` closes a tag. Use a
  string: `<file path="documents/notes.txt"/>`.

An implementation **must** preserve this distinction. Widening `SimpleValue` to admit
words makes `<thing path=a/b/>` ambiguous with the closing delimiter.

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
| `<a></b>` | Message: `Closing tag 'b' does not match opening tag 'a'` |

## The identifier grammar

A second entry point validates a single name:

```
IdentifierOnly ::= Space* Identifier Space* EOF
```

An implementation **must** expose it so a host can ask whether text is a valid name
without running a command.
