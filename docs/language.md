# The command language

Everything you type is parsed into a tree before anything runs. This page covers every
form the parser accepts, what each one means, and how the words you write reach a
command's parameters.

Examples show what the terminal displays underneath. Results that appear as tappable
chips are shown as plain words here.

## One line, one pipeline

A command line is a single line. Spaces and tabs separate words; a newline ends the
program, so there is no line continuation. There are no comments on a line; a
[script](commands.md#run) is a file of lines, and that is where `#` comments live.

A line is one pipeline, or several joined by `else`: see
[Errors as values](#errors-as-values).

The grammar is case insensitive when matching command names, and the text you typed is
preserved exactly in the tree.

## Three ways to call a command

The same command can be written three ways. They produce the same call.

### Command line form

The name, then arguments separated by spaces.

```
$ read documents/notes.txt
Try: ls, in documents, mkdir scratch, echo "hello"
```

A name may be several words joined by hyphens, as `save-view` is. The hyphen has to
touch a word on both sides, so a space before it makes it a flag again: `ls -l` is a
command and a flag, and `echo -5` is a command and a negative number.

### Function form

The name, then arguments in parentheses separated by commas, with the parenthesis
against the name. Use `name: value` to pick a parameter by name.

```
$ write(note.txt, hello)
note.txt
$ read(path: documents/notes.txt)
Try: ls, in documents, mkdir scratch, echo "hello"
```

Function form is the only form that takes `name: value`. In command line form, name an
argument with a flag instead: `ls -path documents`.

The parenthesis has to touch the name. With a space between them it is not a call but a
[pipeline in parentheses](#pipelines-in-parentheses), handed to the command as its
argument:

```
$ first(ls)
'first' needs a table, not text.
$ first (ls | sort name desc)
<row name=readme.txt kind=text folder=/ size=193 modified=2026-09-22T09:30:00.0000000+00:00/>
```

The first line calls `first` with the word `ls`; the second runs `ls | sort name desc`
and gives `first` the table it answered.

### Tag form

An instance tag on its own is an expression that produces a value without calling a
command. See [Tags](#tags-objects-and-components).

```
$ <measurement unit=metres value=3/>
<measurement unit=metres value=3/>
```

A variable can stand on its own the same way: see
[A variable as a stage](#a-variable-as-a-stage).

## Arguments

### Bare words

An unquoted word may contain letters, digits and underscore, plus `. \ / : ~ + @ % -`
after the first character. The first character must be a letter, digit, underscore, or
one of `. \ / ~ *`, or a `-` with a digit after it. That covers the things a shell needs
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
[error recovery](#errors-as-values). Writing one as an ordinary word is a syntax error
that says how to write it as text instead:

```
$ echo eq
Column 5: 'eq' is an operator; write "eq" to pass it as text
$ echo "eq"
eq
```

The message names the column the word starts in, counted from zero. The match is exact
and case-sensitive, so `equals`, `eq.txt` and `Eq` are ordinary words. No command is named after a reserved word, and none may be; writing one where a
command belongs says so:

```
$ else echo x
Column 0: 'else' is a reserved word and cannot name a command
```

### Assignments

An argument written `name=value`, with no spaces around the `=`, is an assignment: a
name carrying a piece of data. Commands that take arbitrary named data, such as `attr`
and `save`, collect them:

```
$ attr documents/notes.txt tag=work due=2026-10-01
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

Numbers use a point as the decimal separator regardless of your locale. A command that
wants a number, such as `progress`, wants one written as a number: `progress 20` is
twenty steps, and `progress "20"` is text and is refused.

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

An empty string is `""`. Four quotes, `""""`, is the empty string too, not the start of
a string quoted twice.

The number of quotes is kept in the tree, so re-serialising your command gives back
exactly what you typed.

### Flags

A flag is a single dash followed by a name: `-path`. Two dashes is a syntax error.

A flag takes the next plain value as its value:

```
$ ls -path documents
name       kind  folder      size  modified
notes.txt  text  /documents  50    2026-09-22T09:30:00.0000000+00:00
```

A flag with nothing after it is a switch and binds `true`:

```
$ ls -path
Directory does not exist : true
```

That message is what a switch looks like when the command wanted a value. Flags bind
`true` only because some parameters are genuinely switches.

A parameter that is a switch can also be given a word: its own name turns it on, and a
word the command offers for the other way turns it off. `sort name desc` sorts downwards
and `sort name asc` upwards; any other word is refused rather than read as on:

```
$ ls | sort name up
'sort' takes 'desc' or 'asc' for 'desc', not 'up'.
```

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
`folder`, `path` and `id`, and a fault the members listed under
[Keeping a failure with `try`](#keeping-a-failure-with-try). A member that is not there
is *nothing* rather than an error, which is what lets a predicate skip a row that is
missing a column instead of stopping the line.

A full stop has to be followed by a name. With nothing after it, the line does not
parse, and the message says what belongs there:

```
$ echo $thing.
Column 12: a column name belongs after the stop, as in $row.kind
```

This is what a predicate uses to read a column: `$row.size` is a member access like
any other. What is particular to `$row` is where it exists. `where`, `find`, `in` and
`save-view` bind it to each row they test, and nothing else does, so reading it
anywhere else says where it can be used:

```
$ echo $row
$row is the row a predicate is testing. It exists only inside where, find, in and save-view: ls | where $row.kind eq folder.
```

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

An operand with no operator around it is just that operand: `in documents` has not
become an expression because expressions exist. Only a line that actually writes an
operator produces one.

Which command an expression can be written for is not a question of grammar. A command
declares a parameter that takes a predicate — `where` is the one that does — and a
command that declares none says so:

```
$ echo $a eq b
'echo' takes a value for 'text', not an expression.
```

What the operators mean is in [Tables and predicates](tables.md#predicates). An
expression is also what `in`, `find` and `save-view` take, which is how a question
becomes somewhere you can be: see [The filesystem](filesystem.md#views).

A parenthesis in operand position is a [nested pipeline](#pipelines-in-parentheses), not
a grouping. It runs once, before the predicate, and every row is compared against the one
value it answered:

```
$ ls | where $row.size gt (ls | count)
name        kind  folder  size  modified
readme.txt  text  /       193   2026-09-22T09:30:00.0000000+00:00
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

A call that fails either way, or names a flag the command does not have, is a call made
wrongly, and the page shows the command's help under the error: what `help <command>`
answers ([decision 0038](decisions/0038-a-wrong-call-shows-its-help.md)). Like the
notes that name what you probably meant, it is drawn in the terminal's own style, apart
from output, and is not part of the fault.

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
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
guide       folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       193   2026-09-22T09:30:00.0000000+00:00
$ echo documents | in
documents
$ read notes.txt | write copy.txt
copy.txt
```

On the page, once a stage has every argument it needs, the first chip completion offers
is `|`, so sending the result on is always one tap away
([decision 0039](decisions/0039-the-pipe-comes-first.md)).

A command uses the piped value only for a parameter that accepts one and that you did
not write out yourself. `read` and `in` take their path that way; `write` takes its text
that way, because its path is the argument you are most likely to write.

If a stage fails, the pipeline stops there and reports that stage's error.

```
$ echo nowhere | in
Directory does not exist : nowhere
```

Unless the line says what to do instead: see [Errors as values](#errors-as-values).

## Pipelines in parentheses

A pipeline written in parentheses, with a space before the opening one, runs on its own
and its result is the value written there. It can stand as an argument, as one side of a
comparison, as a [default](#defaults-with-) or as a stage of its own.

```
$ echo (ls | count)
5
$ ls | (where $row.kind eq folder | count)
4
```

As an argument it is given nothing through the pipe, because the pipe belongs to the
stage the parenthesis is in rather than to the pipeline inside it. As a stage it is given
the pipe, the same as any other stage, so the second line means what it would without the
parentheses.

It runs once, when the line reaches the stage it is written in, and in the same
transaction as the rest of the line: whatever it changes, a later stage sees, and `undo`
takes back with the line. Inside a predicate, what is kept is the value it produced, so a
view saved with one asks the question as it stood when it was saved:

```
$ save-view big $row.size gt (ls | count)
big
$ in big
$row.size gt 5
```

The parenthesis has to be separated from a command name by a space. Against the name,
it is the [function form](#function-form).

## Errors as values

A failure stops the line, and a failed line changes nothing. That is the rule, and
there are three ways for a line to say what should happen instead.

### Recovering with `else`

`else` joins two pipelines. The right one runs only when the left one failed, and it is
given the failure through the pipe.

```
$ read missing.txt else echo "none"
none
$ read missing.txt else echo
File does not exist : /missing.txt
```

`else` binds looser than `|`, so each side is a whole pipeline:
`read x else echo "starting fresh" | write today.txt` writes `today.txt` only when `x`
could not be read, and `a | b else c | d` means `(a | b) else (c | d)`. Several can be
chained; each branch runs only if the one before it failed, and is given that failure.

```
$ read missing.txt else read other.txt else echo "neither"
neither
$ read missing.txt else read other.txt
File does not exist : /other.txt
```

A failed branch leaves nothing behind, exactly as a failed line does. Only the branch
that answered commits, so after this line there is no folder called `today`:

```
$ mkdir today | in nowhere else echo "rolled back"
rolled back
```

### Keeping a failure with `try`

`try` in front of a stage turns that stage's failure into a value, and the line goes on
with it. What the next stage receives is the fault itself.

```
$ try read missing.txt | set problem
File does not exist : /missing.txt
$ echo $problem.kind
NotFound
$ echo $problem.message
File does not exist : /missing.txt
$ echo $problem.path
/missing.txt
```

The terminal draws a caught fault in amber rather than red, because it is a result: the
line succeeded. A fault reads as its message, and these members can be read off it:

| Member | Holds |
| --- | --- |
| `kind` | The fault's kind as a word: `NotFound`, `Conflict`, `Invalid`, `Binding`, `Syntax`, `UnknownCommand`, `Internal`. See [the kinds](errors.md). |
| `message` | The sentence the red line would have shown, without any note beside it. |
| `path` | The path it was about, when there was one; nothing otherwise. |
| `stage` | Which stage failed, counted from one. |
| `cause` | The fault underneath, when there was one; nothing otherwise. |

`try` covers the one stage it is written in front of. Put the part that may fail in
parentheses to cover more than one command: `try (mkdir inside | in nowhere) | set r`.
Whatever the failed stage had done is discarded, so `inside` is not created.

`is-fault` answers whether a value is one:

```
$ is-fault $problem
true
$ echo fine | is-fault
false
```

What the terminal says beside a failure, a `Did you mean …?` and the corrected lines it
offers, is a note for whoever reads the screen, and never part of the fault
([decision 0041](decisions/0041-guidance-is-drawn-apart-from-output.md)). A fault that
`try` or `else` hands on has no notes, so a script reads the same message whether or not
a suggestion was shown. The indented lines are the note:

```
$ read notes
File does not exist : /notes
  suggestion: Did you mean documents/notes.txt?
  fix: read documents/notes.txt
$ try read notes | set lost
File does not exist : /notes
$ echo $lost.message
File does not exist : /notes
$ echo $lost.path
/notes
```

See [Notes beside a message](errors.md#notes-beside-a-message).

Neither `try` nor `else` catches [Stop](web-terminal.md). A stopped line stops, whatever
is written around the stage that was running
([decision 0024](decisions/0024-stop-is-not-recoverable.md)).

### Defaults with `??`

`??` after a stage gives the value to use when that stage answered nothing — `first` of
an empty table, for instance.

```
$ first (ls | where $row.kind eq view) ?? "no views yet"
no views yet
  explanation: kind is folder or text
$ echo hello ?? "never used"
hello
```

The indented line is a note, not part of the answer: the `where` inside the parentheses
kept nothing, and the terminal says why
([An empty answer says why](tables.md#an-empty-answer-says-why)). What flows on is
`no views yet`.

The default belongs to the stage, so it flows on down the pipe:
`first (ls) ?? "none" | set latest` sets `$latest` either way. It is only evaluated when
it is needed, so a default that would fail does no harm on a line that does not reach it.
A fault is an answer, not nothing, so `try read missing.txt ?? fine` keeps the fault.

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
guide       folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       193   2026-09-22T09:30:00.0000000+00:00
$ vars
name      value
entries   5 rows
greeting  hello
```

A cell is one line, so a table bound to a variable says how many rows it has when it
appears inside another table. It is still the table:

```
$ echo $entries | count
5
```

Names may contain letters, digits and underscore. Writing `set $x 1` does not name a
variable `x`; `$x` is read as a variable reference, so it reports
`Unknown variable: $x`. A name a slip from one that is bound gets a note naming it, with
the line that uses it: see [`Unknown variable`](errors.md#unknown-variable-name).

On the page, typing `$` offers every bound variable as a completion, each saying what
it holds, such as `table · 5 rows · name, kind, folder…`. See
[The web terminal](web-terminal.md#completions).

### A variable as a stage

A variable may stand as a stage of its own, with or without members, and the stage's
value is the variable's value
([decision 0032](decisions/0032-a-stage-may-be-a-value.md)). So a name you kept can be
looked at by typing it, and piped on like any other result:

```
$ set v 5
5
$ $v
5
$ ls | set files
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
guide       folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       193   2026-09-22T09:30:00.0000000+00:00
$ $files | count
5
$ $files | where $row.size gt 10
name        kind  folder  size  modified
readme.txt  text  /       193   2026-09-22T09:30:00.0000000+00:00
$ try read missing.txt | set problem
File does not exist : /missing.txt
$ $problem.kind
NotFound
```

It is a stage like any other, so `??`, `else` and `try` apply to it: an unbound name
is a fault that `else` recovers from.

```
$ $nope else echo fallback
fallback
```

A variable stage takes nothing from the pipe: `ls | $v` answers `5`. `$row` standing alone is the fault above, since no row is
being tested.

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
| Wildcards in paths, such as `read *.txt` | Name files individually. `*` is a glob for `like` in a predicate, and nothing else. |
| Redirection `>` and `>>` | Pipe into `write`. |
| Several commands per line with `;` or `&&` | Run them one at a time, or put them in a [script](commands.md#run). `else` is the one way to join pipelines on a line. |
| Escapes inside strings | Strings cannot contain a double quote at all. |
| Background jobs | One command runs at a time; use Stop to end it. |
| Environment variables | Use `set` and `$name`. |

## Reading the tree back

The terminal colours each word by the role the grammar gave it. The roles are: command,
flag, string, variable, identifier, type, attribute, operator, member, keyword and
punctuation. `try` and `else` are keywords; `??` is an operator. Tapping a word in the
scrollback says what it is where it stands: what a variable holds, a command's
parameters, a column's type, what an operator compares, and otherwise its role. See
[Tapping a word](web-terminal.md#tapping-a-word).

Because the tree is faithful, serialising it reproduces your text exactly. The parser's
tests check that round trip, which is what keeps colouring and structure honest.
