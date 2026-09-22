# The command language

Everything you type is parsed into a tree before anything runs. This page covers every
form the parser accepts, what each one means, and how the words you write reach a
command's parameters.

Examples show what the terminal displays underneath. Results that appear as tappable
chips are shown as plain words here.

## One line, one pipeline

A command line is a single line. Spaces and tabs separate words; a newline ends the
program, so there is no line continuation and no multi-command script. There are no
comments.

The grammar is case insensitive when matching command names, and the text you typed is
preserved exactly in the tree.

## Three ways to call a command

The same command can be written three ways. They produce the same call.

### Command line form

The name, then arguments separated by spaces.

```
$ cat readme.txt
This filesystem lives in the browser tab.
```

### Function form

The name, then arguments in parentheses separated by commas. Use `name: value` to pick
a parameter by name.

```
$ write(note.txt, hello)
note.txt
$ cat(path: readme.txt)
This filesystem lives in the browser tab.
```

Function form is the only form that takes `name: value`. In command line form, name an
argument with a flag instead: `ls -path documents`.

### Tag form

An instance tag on its own is an expression that produces a value without calling a
command. See [Tags](#tags-objects-and-components).

```
$ <measurement unit=metres value=3/>
<measurement unit=metres value=3/>
```

## Arguments

### Bare words

An unquoted word may contain letters, digits and underscore, plus `. \ / : ~ + @ % -`
after the first character. The first character must be a letter, digit, underscore, or
one of `. \ / ~`, or a `-` with a digit after it. That covers the things a shell needs
to write without ceremony:

```
$ echo notes.txt
notes.txt
$ echo ../documents
../documents
$ echo C:\Users\me
C:\Users\me
$ echo well-known
well-known
$ echo user@host
user@host
```

Accented letters are ordinary identifier characters, so `café` and `größe` need no
quotes.

A leading `-` is a flag, except in front of a digit, where it is a negative number:

```
$ echo -5
-5
$ echo -x
'echo' has no argument named 'x'.
```

Bare words are recognised where a command argument is expected, and inside a tag.
A `/` inside a word joins it unless a `>` or a `}` comes next, so a path keeps its
separators and the tag still closes:

```
$ <file path=documents/notes.txt/>
<file path=documents/notes.txt/>
```

A `*` is an ordinary word character, so a glob for `like` needs no quotes:
`where $row.name like *.txt`.

Thirteen words are **reserved** and are never bare words, wherever they appear:

```
and  or  not  eq  ne  gt  ge  lt  le  like  has  else  try
```

They are the operators of an [expression](#expressions), and `else` and `try` belong to
error recovery. Writing one as an ordinary word is a syntax error that says how to write
it as text instead:

```
$ echo eq
'eq' is an operator; write "eq" to pass it as text
$ echo "eq"
eq
```

The match is exact and case-sensitive, so `equals`, `eq.txt` and `Eq` are ordinary
words. No command is named after a reserved word, and none may be.

### Assignments

An argument written `name=value`, with no spaces around the `=`, is an assignment: a
name carrying a piece of data. Commands that take arbitrary named data, such as `attr`
and `save`, collect them:

```
$ attr notes.txt tag=work due=2026-10-01
notes.txt
```

This is not the same as `name: value`, which binds a value to a parameter the command
has declared. An assignment's name is data, so a command may be given names it has
never heard of; that is what lets `attr` write any attribute. A command that takes no
assignments says so rather than ignoring them.

The `=` has to be tight against both sides. `a = b` and `a =b` are not assignments.

### Numbers

A word that reads as a number becomes a number, not text. The rest stay text.

```
$ echo 42
42
$ echo 3.5
3.5
$ echo v2
v2
```

Numbers use a point as the decimal separator regardless of your locale. Commands that
want a number, such as `progress`, accept either a number or a word that looks like one.

### Quoted strings

Use quotes for anything containing a space. A string's body cannot contain a double
quote, so there is no escape character. Three delimiters are accepted, and they exist so
a string can be quoted more heavily without changing its meaning:

| Written | Value |
| --- | --- |
| `"hello world"` | `hello world` |
| `""hello world""` | `hello world` |
| `"""hello world"""` | `hello world` |

```
$ echo "hello world"
hello world
$ echo ""quoted twice""
quoted twice
```

The number of quotes is kept in the tree, so re-serialising your command gives back
exactly what you typed.

### Flags

A flag is a single dash followed by a name: `-path`. Two dashes is a syntax error.

A flag takes the next plain value as its value:

```
$ ls -path documents
up  notes.txt
```

A flag with nothing after it is a switch and binds `true`:

```
$ ls -path
Directory does not exist : true
```

That message is what a switch looks like when the command wanted a value. Flags bind
`true` only because some parameters are genuinely switches.

### Named arguments in function form

```
$ echo(text: hello)
hello
```

The name must match a declared parameter name or its flag. `echo(nonsense: 1)` reports
`'echo' has no argument named 'nonsense'.`

### Tags as arguments

A tag may be written wherever an argument is expected, and the command receives the
object or component it builds:

```
$ set shape <square side=2/>
<square side=2/>
$ echo {renderer colour=red/}
{renderer colour=red/}
```

A tag that names a variable still binds it, even when it is an argument.

### Variables as arguments

`$name` is replaced by the value bound to that name, whatever its type:

```
$ set n 42
42
$ echo $n
42
```

### Members

A variable reference may read a member off what it names, with a full stop:

```
$ set thing <measurement unit=metres value=3/>
<measurement unit=metres value=3/>
$ echo $thing.unit
metres
```

An object or a component answers its attributes. A file answers `name`, `kind`,
`folder`, `path` and `id`. A member that is not there is *nothing* rather than an
error, which is what lets a predicate skip a row that is missing a column instead of
stopping the line.

This is what a predicate uses to read a column: `$row.size` is a member access on an
ordinary variable, not a special form.

## Expressions

An argument may be an expression: two operands and a comparison, combined with `and`,
`or` and `not`. Expressions are what [predicates](tables.md#predicates) are written in.

```
Expression ::= OrExpr
OrExpr     ::= AndExpr ( "or" AndExpr )*
AndExpr    ::= NotExpr ( "and" NotExpr )*
NotExpr    ::= "not" NotExpr | Comparison
Comparison ::= Operand ( CompareOp Operand )?
CompareOp  ::= "eq" | "ne" | "gt" | "ge" | "lt" | "le" | "like" | "has"
Operand    ::= ArgumentValue | "(" Pipeline ")"
```

`and` binds tighter than `or`, and both are left associative, so `a or b and c` reads
as `a or (b and c)`. `not` takes the whole comparison after it, so
`not $row.kind eq folder` negates the comparison rather than its left operand. There
are no symbol operators, no parenthesised sub-expressions, and no arithmetic.

An operand with no operator around it is just that operand: `cd documents` has not
become an expression because expressions exist. Only a line that actually writes an
operator produces one.

Which command an expression can be written for is not a question of grammar. A command
declares a parameter that takes a predicate — `where` is the one that does — and a
command that declares none says so:

```
$ echo $a eq b
'echo' takes a value for 'text', not an expression.
```

What the operators mean is in [Tables and predicates](tables.md#predicates).

A parenthesis in operand position is a nested pipeline, not a grouping. It parses, and
running one is not built yet:

```
$ ls | where $row.size gt (ls | count)
A pipeline in parentheses is not a value yet : (ls | count)
```

## How arguments reach parameters

Each command declares parameters in order. Binding happens in one pass:

1. Named arguments and flags bind to the parameter they name, wherever they appear.
2. Remaining arguments fill the remaining parameters in declaration order, optional
   parameters included.
3. A parameter still unfilled takes the piped value, if the parameter accepts one and
   something was piped in.
4. A parameter still unfilled takes its default, if it is optional.
5. Otherwise the command reports `'name' needs an argument for 'parameter'.`
6. Arguments left over after every parameter is filled report
   `'name' takes N arguments, but M were given.`

So `write` takes a path and a text, and these are all the same call:

```
write note.txt hello
write(note.txt, hello)
echo hello | write note.txt
```

## Pipes

`|` runs the commands left to right and hands each command's result to the next one as
its input. The value keeps its type: a list stays a list, a path stays a path.

```
$ ls | set files
up  documents  projects  readme.txt
$ echo documents | cd
documents
$ cat notes.txt | write copy.txt
copy.txt
```

A command uses the piped value only for a parameter that accepts one and that you did
not write out yourself. `cat` and `cd` take their path that way; `write` takes its text
that way, because its path is the argument you are most likely to write.

If a stage fails, the pipeline stops there and reports that stage's error.

```
$ echo nowhere | cd
Directory does not exist : nowhere
```

## Variables

Bind a value with `set`, or with a tag (below). Read it with `$name`.

```
$ set greeting hello
hello
$ set files
'set' needs an argument for 'value'.
$ ls | set entries
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       41    2026-09-22T09:30:00.0000000+00:00
$ vars
name      value
entries   4 rows
greeting  hello
```

A cell is one line, so a table bound to a variable says how many rows it has when it
appears inside another table. It is still the table:

```
$ echo $entries | count
4
```

Names may contain letters, digits and underscore. Writing `set $x 1` does not name a
variable `x`; `$x` is read as a variable reference, so it reports
`Unknown variable : $x`.

Undoing a `set` restores whatever the name was bound to before, or unbinds it if it was
new.

## Tags: objects and components

Tags build structured values directly, without a command. Angle brackets make an
object; braces make a component. The distinction mirrors an entity component system:
entities are things, components are what is attached to them.

### Objects

```
$ <measurement unit=metres value=3/>
<measurement unit=metres value=3/>
$ <outer><inner depth=2/></outer>
<outer><inner depth=2/></outer>
```

An attribute's value is a string, a variable reference or a bare word, including a
path such as `documents/notes.txt`. A closing tag
may repeat the type or be empty: `</thing>` and `</>` are both accepted. A closing tag
that names a different type is an error:

```
$ <a></b>
Closing tag 'b' does not match opening tag 'a'
```

### Components

```
$ {renderer colour=red/}
{renderer colour=red/}
```

Components read back the way you wrote them, with braces, so an object and a component
of the same name stay distinguishable.

### Binding a tag to a variable

Put a name and a `|` before the type:

```
$ <size|measurement unit=metres/>
<measurement unit=metres/>
$ echo $size
<measurement unit=metres/>
$ set copy <$size>
<measurement unit=metres/>
```

The variable name is not part of the value, which is why the result above reads
`<measurement unit=metres/>`.

### Reading a variable back as a tag

```
$ <$size>
<measurement unit=metres/>
```

### Property assignment

The grammar also accepts property tags inside a tag body, in four forms:
`[name=value]`, `[name]=<tag>`, `[name] tags [/name]` and `[name] tags [/]`. They parse
into the tree, but nothing evaluates them yet:

```
$ <t>[size=3]</t>
Cannot evaluate a child PropertyAssignment.
```

## What the language does not have

These are absent by design or not built yet. Nothing here silently half-works.

| Not supported | What to do instead |
| --- | --- |
| Wildcards in paths, such as `cat *.txt` | Name files individually. `*` is a glob for `like` in a predicate, and nothing else. |
| Redirection `>` and `>>` | Pipe into `write`. |
| Several commands per line with `;` or `&&` | Run them one at a time. |
| Escapes inside strings | Strings cannot contain a double quote at all. |
| Background jobs | One command runs at a time; use Stop to end it. |
| Environment variables | Use `set` and `$name`. |

## Reading the tree back

The terminal colours each word by the role the grammar gave it, and tapping a word in
the scrollback names that role. The roles are: command, flag, string, variable,
identifier, type, attribute, operator, member and punctuation.

Because the tree is faithful, serialising it reproduces your text exactly. That
round-trip is checked on every parse, which is what keeps colouring and structure
honest.
