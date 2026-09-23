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
  `cat notes.txt else echo "none"` recovers and `cat notes.txt else set problem` keeps
  it. Only the branch that answered commits anything.
- **`try`**: the stage's fault becomes its result, and the line goes on.
  `try cat notes.txt | set problem` binds it, and `$problem.kind` and
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
| `syntax` | `Syntax` | The line could not be turned into a tree. |
| `binding` | `Binding` | What was written does not fit what the command declared. |
| `unknowncommand` | `UnknownCommand` | No command by that name. |
| `notfound` | `NotFound` | A path, a variable or a record that is not there. |
| `conflict` | `Conflict` | Something is already there. |
| `invalid` | `Invalid` | Understood, and not allowed. |
| none | — | You stopped it. `Stopped.` is drawn in amber with no tag, and is never caught, so never read. |
| `internal` | `Internal` | A defect. Not your mistake; worth reporting. |

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

### `Column N: a column name belongs after the stop, as in $row.kind`

A full stop after a variable reads a member off it, so a name has to follow. `$row.`
on its own is not a variable followed by a path called `.`.

```
$ ls | where $row.
Column 16: a column name belongs after the stop, as in $row.kind
```

Any variable gets the same answer: after `set v 5`, `$v.` says so at column 3. While
you type, completion offers the names that can go there.

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

### `'<command>' needs an argument for '<parameter>'.`

A required parameter was not given, nothing was piped in, and it has no default.

```
$ cd
'cd' needs an argument for 'TargetPath'.
```

Give the argument, or pipe a value into a parameter that accepts one.

### `'<command>' takes N arguments, but M were given.`

More arguments than the command has parameters. N counts every parameter, optional ones
included.

```
$ ls documents extra
'ls' takes 1 argument, but 2 were given.
```

Quote an argument that was meant to be one word: `write note.txt "two words"`.

### `'<command>' has no argument named '<name>'.`

A flag or a named argument that the command does not declare.

```
$ echo -verbose
'echo' has no argument named 'verbose'.
```

Run `help` to see the parameters a command actually takes.

### `Unknown variable: $<name>`

A `$name` that is not bound. Check `vars` for what is bound, or type `$` to see the
variables as completions, and remember that `set $x` reads `$x` rather than naming it.
Its kind is `notfound`.

```
$ echo $nothing
Unknown variable: $nothing
```

### `$row is the row a predicate is testing. It exists only inside where, find, cd and save-view: ls | where $row.kind eq folder.`

`$row` is the one variable nobody sets. `where`, `find`, `cd` and `save-view` bind it
to each row they test, and it exists nowhere else, so reading it anywhere else, as an
argument or as a stage of its own, says where it does exist. Its kind is `notfound`.

```
$ echo $row
$row is the row a predicate is testing. It exists only inside where, find, cd and save-view: ls | where $row.kind eq folder.
$ $row.kind
$row is the row a predicate is testing. It exists only inside where, find, cd and save-view: ls | where $row.kind eq folder.
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
| `<items> is not a table: child N has children of its own.` | A child is a tree rather than a row. |
| `the list is not a table: child N is <kind>, not a tag.` | A list with an item that is not a tag, such as a piece of text. |
| `'count' must be a whole number, not '<value>'.` | `take` or `skip` was given something that is not a whole number of zero or more, such as `x`, `1.5` or `-1`. |
| `'where' needs an argument for 'predicate'.` | `where` with nothing to test. |
| `<predicate> never reads $row, so it is the same for every row.` | See [below](#predicate-never-reads-row-so-it-is-the-same-for-every-row). Kind `binding`. |
| `<predicate> is <kind> (<value>), not true or false.` | See [below](#predicate-is-kind-value-not-true-or-false). Kind `invalid`. |
| `$row is the row a predicate is testing. …` | `$row` read outside a predicate: see [Argument errors](#argument-errors). Kind `notfound`. |
| `'<command>' takes a value for '<parameter>', not an expression.` | A comparison was written for a command that does not take a predicate. |
| `A TagValue cannot be part of an expression.` | A tag as one side of a comparison, as in `where $row.name eq <t/>`. The name is the parser's for what was written there. |
| `A pipeline in parentheses only runs as part of a line : (...)` | A saved view whose text has a pipeline in parentheses in it, written by hand with `write` rather than kept by `save-view`. `cd` into it succeeds and `ls` there fails. A line runs its nested pipelines before the predicate is built, and `save-view` keeps the value; a view file has no line around it to run one. |
| `Unknown operator : <word>` | An operator the evaluator does not implement. The grammar only accepts the ones it does, so this is a defect. Kind `internal`. |

A predicate is a yes-or-no question about the row
([decision 0033](decisions/0033-a-predicate-is-a-question-about-the-row.md)). The two
predicates that cannot be one used to answer an empty table in silence, which looks
like a real answer. Both are faults now, and both say what to write instead.

### `<predicate> never reads $row, so it is the same for every row.`

A predicate with an operator in it that never mentions `$row` compares two fixed
values, so it keeps every row or none. The bare words it compared are named as the
likely columns, when there are any. It is found when the predicate is bound, before
any row is tested, and `where`, `find`, `cd` and `save-view` all say it. Kind
`binding`.

```
$ ls | where kind eq folder
kind eq folder never reads $row, so it is the same for every row. Did you mean $row.kind eq folder?
$ ls | where 3 lt size
3 lt size never reads $row, so it is the same for every row. Did you mean 3 lt $row.size?
$ set v 5
5
$ ls | where $v eq 5
$v eq 5 never reads $row, so it is the same for every row.
```

A plain word with no operator is not a question and is not checked here: `cd journal`
is a path.

### `<predicate> is <kind> (<value>), not true or false.`

A predicate whose value for a row is neither true nor false, such as a column read on
its own. The message names the value it had on the first such row, and how to ask a
question of it. Kind `invalid`.

```
$ ls | where $row.kind
$row.kind is text (folder), not true or false. Compare it: $row.kind eq folder.
$ ls | where name
name is text (name), not true or false. Did you mean $row.name?
```

A column that really is true or false can be read bare. `history`'s `undone` is one:
`history | where $row.undone` keeps the lines that were undone. A missing value counts
as false, so a row without the column is skipped rather than stopping the line.

## Document errors

From `from-xml`, `to-xml`, `from-csv` and `to-csv`.
[Reading and writing files](tables.md#reading-and-writing-files) is the guide. The
readers also give the two answers `cat` gives: `File does not exist : <path>` and
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

## View errors

From `cd`, `find` and `save-view`. [The filesystem](filesystem.md#views) is the guide.

| Message | Meaning |
| --- | --- |
| `'find' needs a predicate, such as $row.kind eq note.` | A plain word was written where a question belongs. `find monday` finds nothing by definition; `find $row.name eq monday` is the question. |
| `'save-view' needs a predicate, such as $row.kind eq note.` | The same, for `save-view`. |
| `A view needs a name.` | `save-view` was given an empty name, such as `""`. |
| `'<path>' does not hold a predicate : <text>` | `cd` on a file of kind `view` whose content is not a predicate: text that does not parse, or a plain word, as in `'/weekend' does not hold a predicate : monday`. `cat` it to see what is in there, or `rm` it. |

## Script errors

From `run`.

| Message | Meaning |
| --- | --- |
| `<path> line <n>: <message>` | A line of a script failed. The lines before it have already committed; the ones after it did not run. Blank and commented lines are counted, so `n` is the number an editor shows. |
| `Scripts are only allowed to run scripts 8 deep.` | A script that runs a script that runs a script, eight times over — usually one that runs itself. Each script on the way out adds its own `<path> line <n>:` in front, so a script that runs itself shows that prefix eight times before the sentence. |

## Execution errors

Raised by commands themselves. The command reports the path it actually used, which is
resolved against the current directory.

### `Unknown command : <name>. Did you mean <command>?`

No command by that name. When one or more commands are a slip or two away, up to three
are named, nearest first; when none is, the message stops at the name. Run `help`, or
type the start of a name to see the commands as completions. Kind `unknowncommand`.

```
$ lss
Unknown command : lss. Did you mean ls?
$ sot
Unknown command : sot. Did you mean set or sort?
$ ca
Unknown command : ca. Did you mean cat, cd or cp?
$ delete readme.txt
Unknown command : delete
```

The message only corrects spelling. Completion also finds a command by what it does:
typing `delete` offers `rm`. Typing `UnknownCommand`, the name of the command that
reports this, gets `Unknown command : UnknownCommand` like any other unknown name.

| Message | Meaning |
| --- | --- |
| `Directory does not exist : <path>` | `cd`, `ls` or `write` could not find it. |
| `File does not exist : <path>` | `cat`, `cp`, `attr`, `run` or a reader could not find it. |
| `That is a directory, not a file : <path>` | `cat`, `write`, `run`, `download` or a reader was given a directory. |
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
| `A live refresh only re-reads : <line>` | A live listing was asked to re-run a line that could change something, including one that moves you, such as `up`. You will not see this on the page: it keeps only lines starting with `ls` or `find` live, and when a refresh is refused it leaves the table as it was. |

## Page messages

Written by the browser page itself rather than by the session. See
[The web terminal](web-terminal.md#loading).

| Message | Meaning |
| --- | --- |
| `starting…` | The status while the .NET runtime loads. The input is disabled. |
| `restoring…` | The status while the log is replayed. The input is still disabled. |
| `wasm` | The status once the terminal is ready. |
| `not persisted` | Beside `wasm`: this browser is not keeping the log. Hover it for the browser's reason. |
| `failed to load` | The status when the runtime did not start within about forty seconds. |
| `failed to restore` | The status when replaying the log failed, with `Could not restore the session: <message>` in the scrollback. The input stays disabled. |
| `<n> stored line(s) could not be read and were skipped. reset starts over.` | The log holds lines this build cannot decode, usually from a different build. The rest were replayed. |

## Reading a path in a message

Most messages quote the resolved path rather than what you typed. Run `cat note.txt`
in `/documents` and the message names `/documents/note.txt`. That is
deliberate: the most common cause of a missing file is being somewhere else than you
thought, and the prompt above the input always shows where you are.

`cd` is the exception. It reports the target as you wrote it, because the thing you
usually need to see there is your own spelling.
