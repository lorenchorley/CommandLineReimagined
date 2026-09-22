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
| `cancelled` | — | You stopped it. Never caught, so never read. |
| `internal` | `Internal` | A defect. Not your mistake; worth reporting. |

## Parse errors

These appear before anything runs, because the line could not be turned into a tree.

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

### `Closing tag 'b' does not match opening tag 'a'`

The shape was valid but the names disagree. Either repeat the opening type or use the
empty closing form:

```
$ <a></a>
<a/>
$ <a></>
<a/>
```

### `Lexical error at column N`

A character that cannot begin any token. Rare; usually a stray symbol such as `&`.

### `Could not parse the command.`

A parse failure with no message attached. It should not happen; if you can reproduce
it, the line is worth reporting.

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

### `'<command>' does not take '<name>=' assignments.`

A `name=value` argument given to a command that collects none. `attr` and `save` take
them; the rest do not, and say so rather than ignoring what you wrote.

```
$ progress steps=20
'progress' does not take 'steps=' assignments.
```

To bind a declared parameter by name, use a colon: `progress steps: 20`.

### `'<word>' is an operator; write "<word>" to pass it as text`

Thirteen words are reserved and are never bare words:
`and or not eq ne gt ge lt le like has else try`. They are the operators of an
expression, and the last two belong to error recovery. Quote one to pass it as text:
`echo "eq"`.

The match is exact, so `equals`, `eq.txt` and `Eq` are ordinary words. The message
names the column the word starts in.

### `'<word>' is a reserved word and cannot name a command`

The same thirteen words, written where a command's name belongs: `else echo x`, with
nothing before the `else`, or `eq x`. A line cannot start with `else`; there has to be
a pipeline for it to recover from.

### `Unsupported argument value : <node>`

The parser produced a value the binder does not know how to evaluate. Strings, words,
numbers, variable references and tags are all handled, so this means the grammar
accepts something execution does not implement yet. It is a gap, not your mistake.

## Table errors

From the table functions, and from reading a tag as a table.

| Message | Meaning |
| --- | --- |
| `'<command>' needs a table, not <kind>.` | Something that is not a table, and cannot be read as one, was piped in. |
| `'<command>' has no column named '<name>'.` | No such column. `columns` lists what there is. |
| `'select' needs at least one column.` | `select` with nothing to select. The pipe is the table, not the column list. |
| `<items> is not a table: child N is <other> where the first is <item>.` | A tag whose children disagree about their type. |
| `<items> is not a table: child N has children of its own.` | A child is a tree rather than a row. |
| `<items> is not a table: child N is text, not a tag.` | A child that is not a tag at all. |
| `'count' must be a whole number, not '<value>'.` | `take` or `skip` was given something that is not a count. |
| `'where' needs an argument for 'predicate'.` | `where` with nothing to test. |
| `Unknown variable : $row` | A predicate written outside a table function, where nothing bound `$row`. |
| `'<command>' takes a value for '<parameter>', not an expression.` | A comparison was written for a command that does not take a predicate. |
| `A <node> cannot be part of an expression.` | A tag as one side of a comparison. |
| `A pipeline in parentheses only runs as part of a line : (...)` | A saved view whose text has a pipeline in parentheses in it, written by hand with `write` rather than kept by `save-view`. A line runs its nested pipelines before the predicate is built, and `save-view` keeps the value; a view file has no line around it to run one. |

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
| `A view needs a name.` | `save-view` was given an empty name. |
| `'<path>' does not hold a predicate : <text>` | `cd` on a file of kind `view` whose content is not an expression any more. `cat` it to see what is in there, or `rm` it. |

## Script errors

From `run`.

| Message | Meaning |
| --- | --- |
| `<path> line <n>: <message>` | A line of a script failed. The lines before it have already committed; the ones after it did not run. Blank and commented lines are counted, so `n` is the number an editor shows. |
| `Scripts are only allowed to run scripts 8 deep.` | A script that runs a script that runs a script, eight times over — usually one that runs itself. |

## Execution errors

Raised by commands themselves. The command reports the path it actually used, which is
resolved against the current directory.

| Message | Meaning |
| --- | --- |
| `Unknown command : <name>` | No command by that name. Run `help`. |
| `Directory does not exist : <path>` | `cd`, `ls` or `write` could not find it. |
| `File does not exist : <path>` | `cat` or `cp` could not find it. |
| `That is a directory, not a file : <path>` | `cat` or `write` was given a directory. |
| `Target directory already exists : <path>` | `mkdir` will not overwrite. |
| `Target file already exists : <path>` | `cp` will not overwrite. Use `write` or `rm` first. |
| `Target directory does not exist : <path>` | `cp` or `download` has nowhere to put it. |
| `Nothing exists at : <path>` | `rm` found neither a file nor a directory. |
| `Directory is not empty : <path>` | `rm` only deletes empty directories. |
| `Cannot delete the current directory.` | Move out of it first. |
| `'<name>' is not a valid variable name.` | Letters, digits and underscore only. |
| `'set' needs a value for '<name>'.` | `set` was given a name but an empty value. |
| `'steps' must be at least 1.` | `progress` was given zero or a negative count. |
| `'steps' must be a whole number, not '<value>'.` | `progress` was given something that is not a number, such as a bare `-steps` flag. |
| `'<name>' is set by the terminal and cannot be written.` | `attr` was asked to write `folder`, `created` or `modified`. |
| `A saved tag needs a 'name' attribute.` | `save` was given a tag with no name for the file. |
| `'save' needs a tag, not <kind>.` | `save` was given text or a number. |
| `A file must have a name.` | Internal: an event named no file. |
| `Not a valid URL : <text>` | `download` needs an absolute URL. |
| `The server did not report a content length.` | `download` cannot show progress without one. |
| `The transfer ended before all bytes arrived.` | The connection closed early. |

## Evaluation errors

From building a value out of a tag.

| Message | Meaning |
| --- | --- |
| `Cannot evaluate a child PropertyAssignment.` | Property tags parse but are not evaluated yet. |
| `Cannot evaluate a <node>.` | A tree shape execution does not handle. A gap, not your mistake. |
| `Unknown command type : <type>` | A command that is neither synchronous nor asynchronous. Internal. |

## Terminal messages

Not errors from the language, but from the session.

| Message | Meaning |
| --- | --- |
| `A command is already running. Stop it first.` | One command at a time. Press Stop. |
| `Stopped.` | You cancelled the command. It may have written its own note as well, such as `Cancelled at 9%`. |
| `Nothing to undo.` | No line has changed anything yet. Not an error: nothing went wrong. |
| `Nothing to redo.` | Nothing has been undone. Not an error either. |
| `No variables. Try: set greeting hello` | `vars` with nothing bound. Not an error: the table is simply empty. |
| `Undone: <line>` | Undo reversed that line. Lines that changed nothing are not recorded, so they are never what it names. |
| `Redone: <line>` | Redo put that line back. It names the original line, not the undo. |
| `The session has not been initialised. Call Initialize first.` | A host executed a line before replaying the log. A defect in the host, not in what you typed. |
| `A live refresh only re-reads : <line>` | The page tried to keep a listing up to date with a line that could change something. You will not normally see this: only `ls` and `find` are kept live. |

## Reading a path in a message

Most messages quote the resolved path rather than what you typed. Run `cat notes.txt`
in `/documents` and the message names `/documents/notes.txt`. That is
deliberate: the most common cause of a missing file is being somewhere else than you
thought, and the prompt above the input always shows where you are.

`cd` is the exception. It reports the target as you wrote it, because the thing you
usually need to see there is your own spelling.
