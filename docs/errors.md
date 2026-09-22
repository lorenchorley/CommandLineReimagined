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

### `Syntax error at column N: expected ...`

The parser reached column N, counted from zero, and could not continue. The list names
the symbols the grammar would have accepted there.

```
$ <thing
Syntax error at column 6: expected identifier, />, >.
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
column and the expected symbols instead, as above. A script line that does have a
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

### `Unknown variable : $<name>`

A `$name` that is not bound. Check `vars` for what is bound, and remember that `set $x`
reads `$x` rather than naming it.

```
$ echo $nothing
Unknown variable : $nothing
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
| `Unknown variable : $row` | A predicate written outside a table function, where nothing bound `$row`. |
| `'<command>' takes a value for '<parameter>', not an expression.` | A comparison was written for a command that does not take a predicate. |
| `A TagValue cannot be part of an expression.` | A tag as one side of a comparison, as in `where $row.name eq <t/>`. The name is the parser's for what was written there. |
| `A pipeline in parentheses only runs as part of a line : (...)` | A saved view whose text has a pipeline in parentheses in it, written by hand with `write` rather than kept by `save-view`. `cd` into it succeeds and `ls` there fails. A line runs its nested pipelines before the predicate is built, and `save-view` keeps the value; a view file has no line around it to run one. |
| `Unknown operator : <word>` | An operator the evaluator does not implement. The grammar only accepts the ones it does, so this is a defect. Kind `internal`. |

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

| Message | Meaning |
| --- | --- |
| `Unknown command : <name>` | No command by that name. Run `help`. Typing `UnknownCommand`, the name of the command that reports this, gets `Unknown command : UnknownCommand` like any other unknown name. |
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
| `Reset. <n> files restored.` | `reset` emptied the log and seeded it again; in the browser that is 9 files. A host that seeds nothing gets `Reset. The filesystem is empty.` |
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
