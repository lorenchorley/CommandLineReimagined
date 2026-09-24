# Error reference

Every message the terminal can show, what it means, and what to do. Messages are
grouped by where they come from.

A failure is a value. Every one is a **fault**: a message, a kind, and where it has
one, the path it was about and the stage of the line that raised it. What happens to
the fault is up to the line.

- **Nothing written**: the line stops at the stage that failed and shows the message in
  red. A failed line changes nothing at all: the whole line is one transaction, so a
  failure discards everything its earlier stages described. The next line runs normally,
  and errors never stop the session.
- **`else`**: the pipeline after it runs instead, with the fault piped in, so
  `read notes.txt else echo "none"` recovers and `read notes.txt else set problem` keeps
  it. Only the branch that answered commits anything.
- **`try`**: the stage's fault becomes its result, and the line goes on.
  `try read notes.txt | set problem` binds it, and `$problem.kind` and
  `$problem.message` read it back. The terminal draws a caught fault in amber, because
  the line did not fail.

[Errors as values](language.md#errors-as-values) has the whole of it, including `??`
for a stage that answered nothing. Stop is the one fault neither `else` nor `try` will
catch.

The wording is what you read. The **kind** is what tells a missing file apart from a
name already taken without having to parse the sentence, and it is what a program
matches on: `$problem.kind` reads the word in the second column.

| Tag on screen | `$problem.kind` | Meaning |
| --- | --- | --- |
| `syntax` | `Syntax` | The line could not be turned into a tree, or a selector given to `pick` could not be read ([Selector errors](#selector-errors)). |
| `binding` | `Binding` | What was written does not fit what the command declared. The page shows the command's help under it. |
| `unknowncommand` | `UnknownCommand` | No command by that name. |
| `notfound` | `NotFound` | A path, a variable or a record that is not there. |
| `conflict` | `Conflict` | Something is already there. |
| `invalid` | `Invalid` | Understood, and not allowed. |
| none | — | You stopped it. `Stopped.` is drawn in amber with no tag, and is never caught, so never read. |
| `internal` | `Internal` | A defect. Not your mistake; worth reporting. |

## Notes beside a message

A message says what went wrong and nothing more
([decision 0041](decisions/0041-guidance-is-drawn-apart-from-output.md)). What the
terminal can add of its own, what you probably meant, comes beside it as a **note**,
under the red line, and a note is not part of the fault. There are two kinds:

- a **suggestion**, under a fault: the command, file, folder or variable you probably
  meant, or the question you probably meant to ask. The nearest names are found as
  [decision 0042](decisions/0042-a-missing-name-names-the-nearest.md) says: first a name
  in the folder it was looked for in that is a slip or two away, then the same name, or
  a name it is the start of, anywhere; at most three.
- an **explanation**, under an empty answer that is not a fault: why `where`, `find` or
  a view kept nothing. See
  [An empty answer says why](tables.md#an-empty-answer-says-why).

A note can offer **fixes**, each a whole corrected line
([decision 0044](decisions/0044-a-fault-may-carry-fixes.md)). In the transcripts on
this page a note is shown indented under the message, its kind and its text, then each
fix after `fix:`:

```
$ read notes
File does not exist : /notes
  suggestion: Did you mean documents/notes.txt?
  fix: read documents/notes.txt
```

On the page the note is a panel labelled `did you mean` for a suggestion and `why` for
an explanation, drawn in the terminal's own style rather than as output, and each fix
is a chip. Tapping the chip puts the line in the input with the caret at the end, and
does not run it: the guess may be wrong, so you run it, or change it first. See
[The terminal's own words](web-terminal.md#the-terminals-own-words).

A mistake that has no place in the line you typed, such as one in a line of a script
`run` ran, is still suggested, and offers no fix.

A script never sees a note. `$problem.message` is the message alone, and `else` and
`try` behave as they always did; the suggestion is there for whoever reads the screen:

```
$ try read notes | set problem
File does not exist : /notes
$ echo $problem.message
File does not exist : /notes
$ read notes else echo "no notes here"
no notes here
```

## Parse errors

These appear before anything runs, because the line could not be turned into a tree.
Their kind is `syntax`, and since the line never ran, neither `else` nor `try` on it can
catch one.

While you type, the page underlines a parse error once it is behind the word you are
writing, and the [detail line](web-terminal.md#the-detail-line) shows the same sentence
that running the line would. A half-written word is never marked.

### `Syntax error at column N: expected ...`

The parser reached column N, counted from zero, and could not continue. What follows
`expected` says in words what could have come there: `a command name`, `a variable`,
`a quoted string`, `a tag`, `a pipe`, `the end of the line` and so on, joined with
"or". A reserved word that could have come there is quoted: `'try'`, `'else'`.

```
$ <thing
Syntax error at column 6: expected an attribute name or the end of the tag.
$ mkdir |
Syntax error at column 7: expected a command name, a variable, a parenthesised pipeline, a tag, a component or 'try'.
```

Common causes:

| Line | Why |
| --- | --- |
| `<thing` | A tag was never closed. |
| `{renderer` | A component tag was never closed. |
| `echo --double` | Two dashes. A flag takes one. |
| `mkdir \|` | A pipe with nothing after it. |
| `[size=3]` | A property tag outside a tag body. |
| `echo "unterminated` | A string was never closed. |
| `echo a & b` | A symbol the language does not use. There is no `&&` and no `;`. |
| `< thing` | A space after `<`. A tag's type follows the bracket directly. |
| `$d \| pick book>author` | A selector with `>`, `[` or `,` in it, not quoted. Write `pick "book > author"`: see [Selector errors](#selector-errors). |

### `Column N: a column name belongs after the stop, as in $row.kind`

A full stop after a variable reads a member off it, so a name has to follow. `$row.`
on its own is not a variable followed by a path called `.`.

```
$ ls | where $row.
Column 16: a column name belongs after the stop, as in $row.kind
```

Any variable gets the same answer: after `set v 5`, `$v.` says so at column 3. While
you type, completion offers the names that can go there.

### `Column N: a name belongs after the @, as in $v.@tag`

A member written with `@` reads one of a tag's own parts, `@tag` or `@children`
([Members](language.md#members)), and the `@` needs a name after it:

```
$ set v <thing a=1/>
<thing a=1/>
$ echo $v.@
Column 9: a name belongs after the @, as in $v.@tag
```

An `@` on its own, where an argument goes, is a word: `echo @` answers `@`.

### `Column N: <operator> needs a value to compare with, such as <example>`

A comparison operator with nothing after it. The example depends on the operator:
`folder` for `eq`, `ne` and `has`, `10` for `gt`, `ge`, `lt` and `le`, and `"*.txt"`
for `like`.

```
$ ls | where $row.kind eq
Column 23: eq needs a value to compare with, such as folder
$ ls | where $row.size gt
Column 23: gt needs a value to compare with, such as 10
$ ls | where $row.name like
Column 25: like needs a value to compare with, such as "*.txt"
```

### `Column N: '<word>' is an operator; write "<word>" to pass it as text`

Thirteen words are reserved and are never bare words:
`and or not eq ne gt ge lt le like has else try`. They are the operators of an
expression, and the last two belong to error recovery. Quote one to pass it as text:

```
$ echo eq
Column 5: 'eq' is an operator; write "eq" to pass it as text
$ echo "eq"
eq
```

The rule holds inside a tag as well: `<t a=eq/>` gets the same answer, with column 5,
and `<t a="eq"/>` is the way to write it.

The match is exact, so `equals`, `eq.txt` and `Eq` are ordinary words. N is the column
the word starts in, counted from zero.

### `Column N: '<word>' is a reserved word and cannot name a command`

The same thirteen words, written where a command's name belongs: `else echo x`, with
nothing before the `else`, or `eq x`. A line cannot start with `else`; there has to be
a pipeline for it to recover from.

```
$ else echo x
Column 0: 'else' is a reserved word and cannot name a command
```

### `Closing tag 'b' does not match opening tag 'a'`

The shape was valid but the names disagree. Either repeat the opening type or use the
empty closing form:

```
$ <a></b>
Closing tag 'b' does not match opening tag 'a'
$ <a></a>
<a/>
$ <a></>
<a/>
```

### `Could not parse the command.`

A line in a [script](#script-errors) that would not parse, and that the grammar had no
more specific sentence for. A line you type never shows this: the page shows the
column and what was expected instead, as above. A script line that does have a
sentence shows that sentence, without the column.

```
$ write bad.clr "ls --x"
bad.clr
$ run bad.clr
> ls --x
/bad.clr line 1: Could not parse the command.
```

## Argument errors

These come from matching what you wrote against what the command declared.

A command called wrongly shows its help
([decision 0038](decisions/0038-a-wrong-call-shows-its-help.md)). The line fails with
the fault below, as it always did, and under it the page draws a panel labelled `help`
with that command's description and the table `help <command>` answers, so what is
right is on the screen beside what was wrong. The indented lines are that panel:

```
$ ls documents extra
'ls' takes 1 argument, but 2 were given.
  help
  List files and directories in a directory, the current directory by default
  name  required  piped  takes               description
  path  false     false  a folder or a view  Directory to list; defaults to the current one
```

It is the help of the command that was called wrongly: `help where extra` shows
`help`'s own help, not `where`'s. Every message in this section whose kind is `binding`
brings it, and so do the other `binding` faults that name their command, such as
`'count' needs a table, not text.` and `'select' needs at least one column.`. The
messages here whose kind is `notfound`, an unknown variable and `$row` outside a
predicate, do not. A question that never reads `$row` does, under the note that says
what to write instead, and so does a value of the wrong kind for a parameter, such
as `ls | take x` (`'count' must be a whole number, not 'x'.`). A failure raised while a
command runs, such as
`File does not exist : /missing.txt`, is not a wrong call and shows no help; nor is an
unknown command, whose note says what it probably meant. When a line has both, the
note comes first and the help under it. `else` and `try` see the same fault whether
the page shows help or not.

### `'<command>' needs an argument for '<parameter>'.`

A required parameter was not given, nothing was piped in, and it has no default.

```
$ in
'in' needs an argument for 'TargetPath'.
  help
  Go into a folder, a saved view, or a question written out
  name        required  piped  takes        description
  TargetPath  true      true   a predicate  The directory, view or predicate to enter
```

Give the argument, or pipe a value into a parameter that accepts one.

### `'<command>' takes N arguments, but M were given.`

More arguments than the command has parameters. N counts every parameter, optional ones
included.

```
$ ls documents extra
'ls' takes 1 argument, but 2 were given.
```

The help under it is the one shown [above](#argument-errors). Quote an argument that was meant to be one word: `write note.txt "two words"`.

### `'<command>' has no argument named '<name>'.`

A flag or a named argument that the command does not declare.

```
$ echo -verbose
'echo' has no argument named 'verbose'.
  help
  Writes its argument, or whatever was piped into it
  name  required  piped  takes    description
  text  true      true   a value  What to write
```

The help under it lists the parameters the command actually takes.

### `Unknown variable: $<name>`

A `$name` that is not bound. Check `vars` for what is bound, or type `$` to see the
variables as completions, and remember that `set $x` reads `$x` rather than naming it.
Its kind is `notfound`.

```
$ echo $nothing
Unknown variable: $nothing
```

When a variable is bound whose name is a slip away, or starts with the name written, a
note names it, nearest first, with a fix that writes the first in its place:

```
$ set greeting hello
hello
$ echo $greting
Unknown variable: $greting
  suggestion: Did you mean $greeting?
  fix: echo $greeting
```

### `$row is the row a predicate is testing. It exists only inside where, find, in and save-view: ls | where $row.kind eq folder.`

`$row` is the one variable nobody sets. `where`, `find`, `in` and `save-view` bind it
to each row they test, and it exists nowhere else, so reading it anywhere else, as an
argument or as a stage of its own, says where it does exist. Its kind is `notfound`.

```
$ echo $row
$row is the row a predicate is testing. It exists only inside where, find, in and save-view: ls | where $row.kind eq folder.
$ $row.kind
$row is the row a predicate is testing. It exists only inside where, find, in and save-view: ls | where $row.kind eq folder.
```

### `'<command>' does not take '<name>=' assignments.`

A `name=value` argument given to a command that collects none. `attr` and `save` take
them; the rest do not, and say so rather than ignoring what you wrote.

```
$ progress steps=20
'progress' does not take 'steps=' assignments.
```

To bind a declared parameter by name, use a flag, `progress -steps 20`, or the function
form, `progress(steps: 20)`.

### `'<command>' takes '<on>' or '<off>' for '<parameter>', not '<word>'.`

A switch given a word it does not take. A switch is on for its own name and off for the
word the command offers for the other way; anything else is refused rather than read as
on.

```
$ ls | sort name up
'sort' takes 'desc' or 'asc' for 'desc', not 'up'.
```

A switch with no word for off says so in a shorter form:

```
$ ls | to-xml l.xml -declaration no
'to-xml' takes '-declaration' on its own, not 'no'.
```

### `Unsupported argument value : <node>`

The parser produced a value the binder does not know how to evaluate. Strings, words,
numbers, variable references and tags are all handled, so this means the grammar
accepts something execution does not implement yet. It is a gap, not your mistake. Kind
`internal`.

## Table errors

From the table functions, from predicates, and from reading a tag as a table.

| Message | Meaning |
| --- | --- |
| `'<command>' needs a table, not <kind>.` | Something that is not a table, and cannot be read as one, was piped in: `echo hi \| count` says `not text`. With nothing piped in at all it says `not empty`. |
| `'<command>' has no column named '<name>'.` | No such column, from `select`, `sort`, `distinct` or `group`. `columns` lists what there is. |
| `'select' needs at least one column.` | `select` with nothing to select. The pipe is the table, not the column list. |
| `<items> is not a table: child N is <other> where the first is <item>.` | A tag whose children disagree about their type. |
| `<items> is not a table: child N has children of its own.` | A child is a tree rather than a row. A list of tags says `the list is not a table`, as `$d.@children \| count` does when the children have children. `pick` reads inside a tree: see [Selector errors](#selector-errors). |
| `the list is not a table: child N is <kind>, not a tag.` | A list with an item that is not a tag, such as a piece of text. |
| `'count' must be a whole number, not '<value>'.` | `take` or `skip` was given something that is not a whole number of zero or more, such as `x`, `1.5` or `-1`. |
| `'where' needs an argument for 'predicate'.` | `where` with nothing to test. |
| `<predicate> never reads $row, so it is the same for every row.` | See [below](#predicate-never-reads-row-so-it-is-the-same-for-every-row). Kind `binding`. |
| `<predicate> is <kind> (<value>), not true or false.` | See [below](#predicate-is-kind-value-not-true-or-false). Kind `invalid`. |
| `$row is the row a predicate is testing. …` | `$row` read outside a predicate: see [Argument errors](#argument-errors). Kind `notfound`. |
| `'<command>' takes a value for '<parameter>', not an expression.` | A comparison was written for a command that does not take a predicate. |
| `A TagValue cannot be part of an expression.` | A tag as one side of a comparison, as in `where $row.name eq <t/>`. The name is the parser's for what was written there. |
| `A pipeline in parentheses only runs as part of a line : (...)` | A saved view whose text has a pipeline in parentheses in it, written by hand with `write` rather than kept by `save-view`. Going in with `in` succeeds and `ls` there fails. A line runs its nested pipelines before the predicate is built, and `save-view` keeps the value; a view file has no line around it to run one. |
| `Unknown operator : <word>` | An operator the evaluator does not implement. The grammar only accepts the ones it does, so this is a defect. Kind `internal`. |

A predicate is a yes-or-no question about the row
([decision 0033](decisions/0033-a-predicate-is-a-question-about-the-row.md)). The two
predicates that cannot be one used to answer an empty table in silence, which looks
like a real answer. Both are faults now, and a note under each says what to write
instead, with the line as a fix.

A predicate that is a question and keeps no row is not a fault: the answer is the
empty table, and an explanation says why nothing was kept. See
[An empty answer says why](tables.md#an-empty-answer-says-why).

### `<predicate> never reads $row, so it is the same for every row.`

A predicate with an operator in it that never mentions `$row` compares two fixed
values, so it keeps every row or none. The note under it reads the bare words it
compared as the likely columns, when there are any, and offers the line with `$row.`
in front of them. It is found when the predicate is bound, before any row is tested,
and `where`, `find`, `in` and `save-view` all say it. Kind `binding`, so the command's
help is drawn under the note.

```
$ ls | where kind eq folder
kind eq folder never reads $row, so it is the same for every row.
  suggestion: Did you mean $row.kind eq folder?
  fix: ls | where $row.kind eq folder
$ ls | where 3 lt size
3 lt size never reads $row, so it is the same for every row.
  suggestion: Did you mean 3 lt $row.size?
  fix: ls | where 3 lt $row.size
$ set v 5
5
$ ls | where $v eq 5
$v eq 5 never reads $row, so it is the same for every row.
```

The message used to end with the suggestion, as
`… for every row. Did you mean $row.kind eq folder?`; the suggestion is the note now,
and the message stops at `row.`.

A plain word with no operator is not a question and is not checked here: `in journal`
is a path.

### `<predicate> is <kind> (<value>), not true or false.`

A predicate whose value for a row is neither true nor false, such as a column read on
its own. The message names the value it had on the first such row, and the note under
it how to ask a question of it: a comparison with that value, or, for a bare word, the
column it probably meant. Kind `invalid`.

```
$ ls | where $row.kind
$row.kind is text (folder), not true or false.
  suggestion: Did you mean $row.kind eq folder?
  fix: ls | where $row.kind eq folder
$ ls | where name
name is text (name), not true or false.
  suggestion: Did you mean $row.name?
  fix: ls | where $row.name
```

The message used to go on `Compare it: $row.kind eq folder.`; that is the note now,
worded like every other suggestion. When the part to change is written more than once in
the line, as in `ls | where $row.kind eq text or $row.kind`, the note names the
comparison and offers no fix, since it cannot tell which of the two to change.

A column that really is true or false can be read bare. `history`'s `undone` is one:
`history | where $row.undone` keeps the lines that were undone. A missing value counts
as false, so a row without the column is skipped rather than stopping the line.

## Document errors

From `from-xml`, `to-xml`, `from-csv` and `to-csv`.
[Reading and writing files](tables.md#reading-and-writing-files) is the guide. The
readers also give the two answers `read` gives: `File does not exist : <path>` and
`That is a directory, not a file : <path>`.

| Message | Meaning |
| --- | --- |
| `Not well-formed XML : <path> line <n>, position <m>` | The file is not XML, or declares a DTD, which is refused. The line and position are where the parser gave up; an empty file gives line 1, position 1. Kind `invalid`. |
| `'to-xml' needs a tag, a table or a list of tags, not <kind>.` | Only something with elements in it can be a document. A list with an item that is not a tag says what the item is. Kind `binding`. |
| `'<name>' is not a name XML allows.` | An element or attribute name XML cannot hold, usually a column from a CSV header with a space in it. `select` the columns you want, or rename them before writing. Kind `invalid`. |
| `<path> line <n> has <m> fields where the header has <k>.` | A CSV record wider or narrower than the header. `n` is the line the record starts on, counting line breaks inside quoted fields. Kind `invalid`. |
| `<path> line <n>: a quoted field is never closed.` | A quote opens a field and the file ends before one closes it. Kind `invalid`. |
| `<path> line <n>: a closing quote is followed by more text.` | A quoted field is followed by something other than the delimiter or a line break, such as `"a"b`. Kind `invalid`. |
| `<path> has two columns named '<name>'.` | The header names a column twice, ignoring case, which would make `$row.<name>` mean whichever came first. Kind `invalid`. |
| `<path> column <n> has no name.` | An empty header cell. Kind `invalid`. |
| `'delimiter' must be one character, or 'tab', not '<text>'.` | `-delimiter` was given more than one character, a quote or a line break. Kind `invalid`. |

## Selector errors

From `pick`. [Reading a tree with `pick`](tables.md#reading-a-tree-with-pick) is the
guide, and has the subset of CSS it reads.

A selector that `pick` cannot read is a fault of kind `syntax`, like a line that does
not parse, but it is raised when `pick` runs, so `else` and `try` catch it as they
catch any other. The message quotes the selector, says where it stopped, a character
counted from one or its end, and what it expected there. It is read before anything
piped in, so a bad selector is its own fault whatever the document is. From a fresh
tab:

```
$ set d <library city=paris><book title=dune year=1965><author name=herbert/></book><book title=emma year=1815><author name=austen/></book><shelf/></library>
<library city=paris><book title=dune year=1965><author name=herbert/></book><book title=emma year=1815><author name=austen/></book><shelf/></library>
$ $d | pick "book >"
The selector 'book >' stops at its end: an element name, '*' or '[' is expected after '>'.
$ $d | pick "book:first-child"
The selector 'book:first-child' stops at character 5, ':': ':' is not part of the CSS that pick reads: names, *, [attribute] tests, spaces, > and commas.
$ $d | pick "[title~=dune]"
The selector '[title~=dune]' stops at character 7, '~': '~=' is not part of the CSS that pick reads, whose tests are =, ^=, $= and *=.
$ $d | pick "[title"
The selector '[title' stops at its end: ']' is expected to close '['.
$ $d | pick ""
The selector is empty: write an element name, such as 'book', or '*' for every element.
$ $d | try pick "#main" | set problem
The selector '#main' stops at character 1, '#': '#' is not part of the CSS that pick reads: names, *, [attribute] tests, spaces, > and commas.
$ $problem.kind
Syntax
```

| What it expected | Where |
| --- | --- |
| `an element name, '*' or '[' is expected after '>'` | A combinator with nothing after it, as in `book >` or `book > > author`; `after ','` for a group left unfinished, as in `book,`; and `at the start` for a selector that starts with `>` or `,`. |
| `'<c>' is not part of the CSS that pick reads: names, *, [attribute] tests, spaces, > and commas` | A character outside the subset: `#` and `.` for ids and classes, `:` for pseudo-classes, `+` and `~` for sibling combinators. |
| `'<c>=' is not part of the CSS that pick reads, whose tests are =, ^=, $= and *=` | An attribute test outside the subset, such as `~=` or `\|=`. |
| `']' or one of =, ^=, $= and *= is expected after the attribute name` | Something else after the name, such as `[title!=dune]`. |
| `an attribute name is expected after '['` | `[` with no name in it. |
| `a value is expected after '<test>'` | A test with nothing to compare with, as in `[title=`. |
| `the quote is not closed` | A value opened with `'` and not closed. |
| `']' is expected after the value` | A value with more after it before the `]`, or none. |
| `']' is expected to close '['` | The selector ended inside the brackets. |

A selector with a space, `>`, `[` or `,` has to be in double quotes. Written bare, it is
split or does not parse, and the message is the line's own, a
[parse error](#syntax-error-at-column-n-expected-): `$d | pick book>author` says
`Syntax error at column 14: …`.

The piped value is the other half. `pick` reads a tag, a list of tags, or a table
whose rows have an `@tag` column, which is what `pick` answers:

```
$ echo hello | pick book
'pick' needs a tag, a list of tags or a table with a @tag column, not text.
$ ls | pick book
'pick' needs a tag, a list of tags or a table with a @tag column, not a table without a @tag column.
$ pick book
'pick' needs a tag, a list of tags or a table with a @tag column, not empty.
```

| Message | Meaning |
| --- | --- |
| `'pick' needs a tag, a list of tags or a table with a @tag column, not <kind>.` | Nothing with elements in it was piped in: text, a number, a listing, or, with `not empty`, nothing at all. A listing's rows are records, not elements; to read a file as a document, `from-xml` it first. A list with an item that is not a tag says `not a list whose item <n> is <kind>`. Kind `binding`, so the page draws `pick`'s help under it. |
| `'pick' cannot read row <n> as an element: its @tag is empty.` | A table with an `@tag` column, read back as elements, has a row with nothing in it, as a table read from a CSV file with an `@tag` header can. Kind `invalid`. |

## View errors

From `in`, `find` and `save-view`. [The filesystem](filesystem.md#views) is the guide.

| Message | Meaning |
| --- | --- |
| `'find' needs a predicate, such as $row.kind eq note.` | A plain word was written where a question belongs. `find monday` finds nothing by definition; `find $row.name eq monday` is the question. |
| `'save-view' needs a predicate, such as $row.kind eq note.` | The same, for `save-view`. |
| `A view needs a name.` | `save-view` was given an empty name, such as `""`. |
| `'<path>' does not hold a predicate : <text>` | `in` on a file of kind `view` whose content is not a predicate: text that does not parse, or a plain word, as in `'/weekend' does not hold a predicate : monday`. `read` it to see what is in there, or `rm` it. |

## Script errors

From `run`.

| Message | Meaning |
| --- | --- |
| `<path> line <n>: <message>` | A line of a script failed. The lines before it have already committed; the ones after it did not run. Blank and commented lines are counted, so `n` is the number an editor shows. |
| `Scripts are only allowed to run scripts 8 deep.` | A script that runs a script that runs a script, eight times over — usually one that runs itself. Each script on the way out adds its own `<path> line <n>:` in front, so a script that runs itself shows that prefix eight times before the sentence. |

## Execution errors

Raised by commands themselves. The command reports the path it actually used, which is
resolved against the current directory.

### `Unknown command : <name>`

No command by that name. When one or more commands are a slip or two away, or the
name is one of a command's keywords, a note names up to three, nearest first, with a
fix for each: the line with that command in place of the name, the rest as you wrote
it. When none is, there is no note. Run `help`, or type the start of a name to see the
commands as completions. Kind `unknowncommand`. It shows no help under it: there is no
command whose help it could be.

```
$ lss
Unknown command : lss
  suggestion: Did you mean ls?
  fix: ls
$ sot
Unknown command : sot
  suggestion: Did you mean set or sort?
  fix: set
  fix: sort
$ rn
Unknown command : rn
  suggestion: Did you mean in, rm or run?
  fix: in
  fix: rm
  fix: run
$ delete readme.txt
Unknown command : delete
  suggestion: Did you mean rm?
  fix: rm readme.txt
```

The message used to carry the suggestion, as `Unknown command : lss. Did you mean ls?`;
it stops at the name now, and `$problem.message` reads `Unknown command : lss`.

The keywords are how the old names lead to the new ones. `cd`, `up` and `cat` were
renamed `in`, `out` and `read`
([decision 0037](decisions/0037-in-out-back-and-read.md)), and each old name is the new
command's first keyword:

```
$ cd documents
Unknown command : cd
  suggestion: Did you mean in?
  fix: in documents
$ up
Unknown command : up
  suggestion: Did you mean out?
  fix: out
$ cat readme.txt
Unknown command : cat
  suggestion: Did you mean read?
  fix: read readme.txt
```

Completion finds them the same way: typing `cd` offers `in`, and `delete` offers `rm`.
A short word that merely equals a keyword by accident is not taken for one: `by` is
`Unknown command : by`, with no note. Typing `UnknownCommand`, the name of the
command that reports this, gets `Unknown command : UnknownCommand` like any other
unknown name.

### A path that is not there

`File does not exist`, `Directory does not exist`, `Nothing exists at` and
`Target directory does not exist` name the path the command looked for, as the table
below says. When something near it is there, a note names it, at most three, with a
fix that writes each in place of the path you wrote. A folder is looked for among
folders only: `in` and `ls` want one, and so do the target of `cp` and `download` and
the parent `write` and `mkdir` write into. A path is written as you would type it
standing where you are: relative when it is inside the current folder, absolute
otherwise:

```
$ read notes
File does not exist : /notes
  suggestion: Did you mean documents/notes.txt?
  fix: read documents/notes.txt
$ in documnts
Directory does not exist : documnts
  suggestion: Did you mean documents?
  fix: in documents
$ rm documents/note.txt
Nothing exists at : /documents/note.txt
  suggestion: Did you mean documents/notes.txt?
  fix: rm documents/notes.txt
$ cp readme.txt documnts
Target directory does not exist : /documnts
  suggestion: Did you mean documents?
  fix: cp readme.txt documents
```

With nothing near, the message is all there is:

```
$ read missing.txt
File does not exist : /missing.txt
$ in nowhere
Directory does not exist : nowhere
```

A mistake inside a script is suggested too, with no fix, since the line to correct is
in the script rather than in the input:

```
$ write typo.clr "read notes"
typo.clr
$ run typo.clr
> read notes
/typo.clr line 1: File does not exist : /notes
  suggestion: Did you mean documents/notes.txt?
```

| Message | Meaning |
| --- | --- |
| `Directory does not exist : <path>` | `in`, `ls` or `write` could not find it. |
| `File does not exist : <path>` | `read`, `cp`, `attr`, `run` or a reader could not find it. |
| `That is a directory, not a file : <path>` | `read`, `write`, `run`, `download` or a reader was given a directory. |
| `Target directory already exists : <path>` | `mkdir` will not overwrite, and a file of that name counts. |
| `Target file already exists : <path>` | `cp`, `save` or `save-view` will not overwrite, and `attr x name=y` will not rename onto a sibling's name. Use `write` or `rm` first. |
| `'cp' copies files, and <path> is a directory.` | `cp` copies one record, and a directory's record without what is in it would not be a copy. |
| `Target directory does not exist : <path>` | `cp` or `download` has nowhere to put it. |
| `Nothing exists at : <path>` | `rm` found neither a file nor a directory. |
| `Directory is not empty : <path>` | `rm` only deletes empty directories. |
| `Cannot delete the current directory.` | Move out of it first. `rm /` says the same. |
| `'<name>' is not a valid variable name.` | Letters, digits and underscore only. |
| `'set' needs a value for '<name>'.` | `set` was given a value that turned out to be nothing, such as `set x (ls \| where $row.kind eq view \| first)` with no views. An empty string is a value and is accepted. |
| `'steps' must be at least 1.` | `progress` was given zero or a negative count. |
| `'<parameter>' must be a whole number, not '<value>'.` | `progress` was given a `steps` or `delay` that is not a whole number, such as `1.5`, a bare `-steps` flag, which reads `true`, or quoted text such as `"3"`. The message names the one that is wrong. |
| `'delay' must be zero or more, not '<value>'.` | `progress` was given a negative delay, such as `-5`. |
| `'<name>' is set by the terminal and cannot be written.` | `attr` or `save` was asked to write `folder`, `created` or `modified`. |
| `'size' is worked out from the content and cannot be written.` | `attr` or `save` was asked to write `size`, which is not stored: it is the content's length. |
| `A directory cannot change its kind : <path>` | `attr` was asked to give a directory another `kind`. Whatever is in it names it as its folder. |
| `A file with content cannot become a directory : <path>` | `attr x kind=folder` on a file with content. A record with no content, such as a saved tag, may become one. |
| `'<name>' is not a valid file name: '/' separates directories.` | `save` or `attr name=` was given a name with a `/` in it, such as `a/b`. A name is one segment of a path. |
| `'<name>' is not a valid file name: it already names a directory.` | A name of `.` or `..`, which already mean somewhere else. |
| `A saved tag needs a 'name' attribute.` | `save` was given a tag with no name, or an empty one, for the file. |
| `'save' needs a tag, not <kind>.` | `save` was given text, a number or a component. |
| `A file must have a name.` | A file or directory with an empty name, such as `write "" hello`, `mkdir ""` or `attr x name=""`. |
| `Not a valid URL : <text>` | `download` needs an absolute URL. |
| `The server did not report a content length.` | `download` cannot show progress without one. |
| `Download failed : <reason>` | The request itself failed. In the browser this is usually a host that does not allow cross-origin reads; see [Troubleshooting](troubleshooting.md). |

## Evaluation errors

From building a value out of a tag, and from defects. All are kind `internal`.

| Message | Meaning |
| --- | --- |
| `Cannot evaluate a child PropertyAssignment.` | Property tags parse but are not evaluated yet. |
| `Cannot evaluate a <node>.` | A tree shape execution does not handle. A gap, not your mistake. |
| `<exception> : <message>` | A defect that reached the session: the name of the .NET exception and what it said. The session keeps working; the line is worth reporting. |

## Terminal messages

Not errors from the language, but from the session.

| Message | Meaning |
| --- | --- |
| `A command is already running. Stop it first.` | One command at a time. The page never sends a second line while one runs — Run and Enter stop the running command instead — so a host that does is what sees this. |
| `Stopped.` | You cancelled the command. It may have written its own note as well, such as `Cancelled at 9%`. |
| `Nothing to undo.` | No line has changed anything yet. Not an error: nothing went wrong. |
| `Nothing to redo.` | Nothing has been undone. Not an error either. |
| `No variables. Try: set greeting hello` | `vars` with nothing bound writes this above its empty table. Not an error. |
| `Undone: <line>` | Undo reversed that line. Lines that changed nothing are not recorded, so they are never what it names. |
| `Redone: <line>` | Redo put that line back. It names the original line, not the undo. |
| `Reset. <n> files restored.` | `reset` emptied the log and seeded it again; in the browser that is 17 files. A host that seeds nothing gets `Reset. The filesystem is empty.` |
| `The session has not been initialised. Call Initialize first.` | A host executed a line before replaying the log. A defect in the host, not in what you typed. |
| `A live refresh only re-reads : <line>` | A live listing was asked to re-run a line that could change something, including one that moves you, such as `out`. You will not see this on the page: it keeps only lines starting with `ls` or `find` live, and when a refresh is refused it leaves the table as it was. |

## Page messages

Written by the browser page itself rather than by the session. There is no status
line: the page says how its start went in the banner, the panel labelled `note` at the
top of the scrollback. None of these is a fault of a line anyone ran, so each is said
in a panel labelled `note`, in the terminal's own style, and never as an error
([decision 0046](decisions/0046-restore-messages-are-guidance.md)): a red error with a
kind tag is only ever the fault of a line you ran. See
[The web terminal](web-terminal.md#loading).

| Message | Meaning |
| --- | --- |
| `Loading the terminal…` | The banner while the .NET runtime loads. The input is disabled. |
| `restoring…` | The banner while the log is replayed. The input is still disabled. |
| `not persisted` | A mark at the end of the banner: this browser is not keeping the log. Hover it for the browser's reason. |
| `failed to load` | In red in the banner, when the runtime did not start within about forty seconds. Reloading may help. |
| `failed to restore` | In red in the banner, when replaying the log failed, with `Could not restore the session: <message>` in a `note` in the scrollback. The input stays disabled. |
| `copied` | A `note` over the foot of the scrollback, when text selected there has been copied to the clipboard. It fades on its own. |
| `<n> stored line(s) could not be read and were skipped. reset starts over.` | A `note` in the scrollback: the log holds lines this build cannot decode, usually from a different build. The rest were replayed. |

## Reading a path in a message

Most messages quote the resolved path rather than what you typed. Run `read note.txt`
in `/documents` and the message names `/documents/note.txt`. That is
deliberate: the most common cause of a missing file is being somewhere else than you
thought, and the prompt above the input always shows where you are.

`in` is the exception. It reports the target as you wrote it, because the thing you
usually need to see there is your own spelling.
