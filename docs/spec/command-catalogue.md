# Command catalogue

The normative contract of each built-in command: its parameters, its result, what it
writes, what it reverses and how it fails. The
[user-facing reference](../commands.md) covers the same ground with examples.

A parameter marked *piped* has `AcceptsPipedInput`. A parameter marked *optional* is an
`OptionalCommandParameter`. Parameters are listed in declaration order, which is the
order positional arguments fill them.

Every path argument is resolved against the current directory unless it is rooted.
Messages quote the resolved path except where noted.

## Registration

A host chooses which commands to register. The browser terminal registers every
command below. `UnknownCommand` **must** be registered by any host that wants an
unrecognised name reported as a normal failure; see
[Resolving a command](execution-model.md#resolving-a-command).

There are no host-specific commands. `debug`, which required the entity component
system, was dropped when the command layer moved to the core; a host that wants it
supplies it.

Every command below returns events rather than changing anything. The `Undo` rows say
what reversing the line does, which is a consequence of the events, not a method the
command implements.

## Navigation

### ls

| Field | Value |
| --- | --- |
| Name | `ls` |
| Parameters | `path` (optional, flag `path`) |
| Returns | `Table` |
| Undo | none |

Lists `path`; or, when a view is set and no path is written, every record in the store
the view matches; or the current folder. The rows **must** be ordered: folders, then
files, each group ordered by name with an ordinal comparison.

A view's listing **must** be built from the matching records alone, so its columns are
their attributes rather than every attribute in the store. Writing a path **must**
list that folder and **must not** clear the view.

This ordering is normative. It previously said entry order "follows the filesystem",
which meant it was whatever the host happened to return and could not be tested at all.

The columns **must** be `name`, `kind`, `folder`, `size` and `modified`, followed by
every other attribute any listed record carries, ordered by name with an ordinal
comparison. `created` **must not** be a column. The `name` cell **must** be the record
as a `File`, so it carries the path it argues; `size` **must** be the length of the
record's content in characters, computed rather than stored, and 0 where there is none.

A listing **must not** contain a parent entry. Every row of a table of records is a
record, and navigating upwards is the host's affordance and the `up` command.

Errors: `Directory does not exist : <path>`.

### cd

| Field | Value |
| --- | --- |
| Name | `cd` |
| Parameters | `TargetPath` (piped, kind `Predicate`) |
| Returns | `File` of the folder entered, `Text` of `/` for the root, or `Query` of the view entered |
| Undo | returns to the previous location, view included |

The parameter's kind is `Predicate`, so the argument arrives unevaluated and `cd`
decides what it is:

- An argument that used an operator is a **view**. `Location.View` **must** be set to
  it and `Location.Folder` **must** be left unchanged.
- An argument that is a plain operand, or a piped value, is a **name**. It is evaluated
  and resolved. A record of kind `view` **must** be entered as the view its content
  parses to; anything else **must** be resolved as a folder path.
- Entering a folder **must** clear `Location.View`.

The target **must** be normalised, so `cd ..` yields the parent's real path. Entering
the location already held **must** emit no event.

Errors: `Directory does not exist : <target>`, quoting the target as written;
`'<path>' does not hold a predicate : <text>` for a view file whose content is not an
expression.

### up

| Field | Value |
| --- | --- |
| Name | `up` |
| Parameters | none |
| Returns | `Text` of the current folder's path |
| Undo | returns to the previous location, view included |

With a view set, `up` **must** clear `Location.View` and **must** leave
`Location.Folder` unchanged. Without one it moves to the parent.

At the filesystem root, moving up **must** leave the current directory unchanged and
**must not** fail. The directory a session starts in is not a boundary: a user may
navigate above it, and what is reachable from there is the host's filesystem, which
under WebAssembly is the page's in-memory one.

### pwd

| Field | Value |
| --- | --- |
| Name | `pwd` |
| Parameters | none |
| Returns | `Query` of the view when one is set, otherwise `Text` of the current folder's path |
| Undo | none |

### find

| Field | Value |
| --- | --- |
| Name | `find` |
| Parameters | `predicate` (kind `Predicate`) |
| Returns | `Table` |
| Undo | none |

Lists every record in the store the predicate is true of, in the same shape `ls`
produces and with the same ordering. It **must not** change the location.

The predicate is evaluated once per candidate record, in a scope with `$row` bound to
that record's row, exactly as `where` binds it
([execution model](execution-model.md#predicates)).

Errors: `'find' needs a predicate, such as $row.kind eq note.` when the argument used
no operator.

### save-view

| Field | Value |
| --- | --- |
| Name | `save-view` |
| Parameters | `name`, `predicate` (kind `Predicate`) |
| Returns | `File` of the record created |
| Undo | deletes the view |

Creates a record in the current folder with `kind` of `view` and content equal to the
predicate's display text. A view is an ordinary record in every other respect.

Errors: `'save-view' needs a predicate, such as $row.kind eq note.`;
`Target file already exists : <path>`; `A view needs a name.`

## Files

### cat

| Field | Value |
| --- | --- |
| Name | `cat` |
| Parameters | `path` (piped) |
| Returns | `TextValue` of the file's contents |
| Undo | none |

Errors: `That is a directory, not a file : <path>`, `File does not exist : <path>`.

### write

| Field | Value |
| --- | --- |
| Name | `write` |
| Parameters | `path`, `text` (piped) |
| Returns | `File` |
| Undo | restores the previous contents, or deletes a file that did not exist |

The text written is the value's **display** string, so writing a list writes what the
list reads as.

Errors: `That is a directory, not a file : <path>`,
`Directory does not exist : <parent>`. Creating missing parents is **not** permitted.

### rm

| Field | Value |
| --- | --- |
| Name | `rm` |
| Parameters | `path` (piped) |
| Returns | `TextValue`, `Removed <name>` |
| Undo | restores the file with its contents, or recreates the directory |

A directory **must** be empty to be deleted, and the current directory **must not** be
deletable. Recursive deletion is deliberately absent: it cannot be undone from a
snapshot of one entry.

Errors: `Directory is not empty : <path>`, `Cannot delete the current directory.`,
`Nothing exists at : <path>`.

### cp

| Field | Value |
| --- | --- |
| Name | `cp` |
| Parameters | `sourcePathAndFile`, `targetPath` |
| Returns | `File` |
| Undo | deletes the copy |

The copy keeps the source's file name. Overwriting is refused.

Errors: `File does not exist : <source>`,
`Target directory does not exist : <target>`, `Target file already exists : <path>`.

### mkdir

| Field | Value |
| --- | --- |
| Name | `mkdir` |
| Parameters | `FolderName` |
| Returns | `File` of the new folder |
| Undo | deletes it |

Errors: `Target directory already exists : <path>`, including when the name is taken by
a file, since names are unique within a folder across files and folders together
([decision 0016](../decisions/0016-folders-as-records.md)).

## Attributes

### attr

| Field | Value |
| --- | --- |
| Name | `attr` |
| Parameters | `path` (piped), `assignments` (kind `Assignments`) |
| Returns | `Table` of `name` and `value` with no assignments, otherwise the `File` |
| Undo | restores every attribute the record had |

With no assignments it **must** return a table with columns `name` and `value`, one row
per attribute the record carries, ordered by name, `created` included. With assignments
it emits `AttributesChanged` and returns the file.

`name` and `kind` may be written and **must** be validated: renaming onto a name
already used in the folder is a `Conflict`. `folder`, `created` and `modified` are the
runtime's and **must** be refused.

Errors: `File does not exist : <path>`, `'<name>' is set by the terminal and cannot be
written.`, `Target file already exists : <path>`.

### save

| Field | Value |
| --- | --- |
| Name | `save` |
| Parameters | `tag` (piped) |
| Returns | `File` |
| Undo | deletes the record |

Creates a record from an object tag. The tag's type name becomes `kind`, its `name`
attribute becomes `name`, and every other attribute is copied across. The record has no
content, so `cat` on it **must** return empty text rather than failing.

Errors: `A saved tag needs a 'name' attribute.`, `Target file already exists : <path>`,
`'save' needs a tag, not <kind>.`

## Values

### echo

| Field | Value |
| --- | --- |
| Name | `echo` |
| Parameters | `text` (piped) |
| Returns | the value it was given, unchanged |
| Undo | none |

`echo` **must not** convert its argument. A number stays a number and a list stays a
list; this is how a test or a user inspects what a pipe carries.

### set

| Field | Value |
| --- | --- |
| Name | `set` |
| Parameters | `name`, `value` (piped) |
| Returns | the bound value |
| Undo | restores the previous binding, or unbinds a new name |

The name **must** consist of letters, digits and underscore. Returning the value lets
`set` sit mid-pipeline.

Errors: `'<name>' is not a valid variable name.`, `'set' needs a value for '<name>'.`

### vars

| Field | Value |
| --- | --- |
| Name | `vars` |
| Parameters | none |
| Returns | `Table` of `name` and `value` |
| Undo | none |

One row per variable in scope, ordered by name. With nothing bound it **must** still
return a table, so that `vars | count` is 0 rather than a fault, and **should** write
one output line inviting the user to bind something.

### is-fault

| Field | Value |
| --- | --- |
| Name | `is-fault` |
| Parameters | `value` (optional, piped) |
| Returns | `Boolean`: whether the value is a `Fault` |
| Undo | none |
| ReadOnly | yes |

A script that branches on whether something worked needs a question it can ask
without knowing what success would have looked like. Given nothing at all, the answer
**must** be `false`.

## Long-running commands

Both are `CommandActionAsync`. Both **must** observe the invocation's cancellation
token.

### progress

| Field | Value |
| --- | --- |
| Name | `progress` |
| Parameters | `steps` (optional, default 100), `delay` (optional, default 100 ms) |
| Returns | `NumberValue`, the percentage reached |
| Undo | writes `Progress test undone` |

Writes two output lines and updates them in place: a percentage and a bar. On success
it writes `Progress test finished`; on cancellation `Cancelled at N%`; on failure
`Progress test failed : <reason>`.

Errors: `'steps' must be at least 1.`,
`'steps' must be a whole number, not '<value>'.`

This command exists to exercise the asynchronous path. It **must not** touch the
filesystem.

### download

| Field | Value |
| --- | --- |
| Name | `download` |
| Parameters | `url` (optional), `into` (optional, default the current directory) |
| Returns | `File` |
| Undo | deletes the file |

Writes three output lines and updates them in place: a percentage, a bar and a rate in
MB/s. An existing file at the destination is deleted before the transfer starts.

The implementation **must** stream to disk rather than buffering the response, and
**must** fail rather than loop when the server closes early.

Errors: `Not a valid URL : <text>`, `Target directory does not exist : <path>`,
`The server did not report a content length.`,
`The transfer ended before all bytes arrived.` A failure is also written as
`Download failed : <reason>`.

## Table functions

Thirteen commands over tables. Each one **must**:

- declare an optional piped parameter, last, that carries the table;
- coerce whatever it is given per
  [Reading a tag as a table](execution-model.md#reading-a-tag-as-a-table), and raise
  `'<command>' needs a table, not <kind>.` when it cannot;
- raise `'<command>' has no column named '<name>'.` for a column it was asked for and
  the table does not have;
- emit no events.

| Name | Parameters | Returns |
| --- | --- | --- |
| `where` | `predicate` (kind `Predicate`) | the rows the predicate answered `Boolean true` for |
| `select` | `columns` (kind `Rest`) | those columns, in the order named |
| `sort` | `column`, `desc` (optional, flag `desc`) | the rows ordered by the column |
| `take` | `count` | the first `count` rows |
| `skip` | `count` | every row after the first `count` |
| `first` | none | the first row as an `Object` of type `row`, or `None` |
| `last` | none | the last row as an `Object` of type `row`, or `None` |
| `count` | none | a `Number` |
| `distinct` | `column` (optional) | unique rows, or that column's unique values as a one-column table |
| `group` | `column` | a table of `key` and `rows`, one row per distinct value |
| `columns` | none | a table of `name` and `type` |
| `rows` | none | a `List` of `Object` rows |
| `table` | none | the table itself |

`where` **must** evaluate its predicate in a child scope with `$row` bound to the row
as an `Object` of type `row`, and keep the row only where the answer is `Boolean true`.

`sort` **must** be stable in both directions: rows that compare equal keep the order
they arrived in, so an implementation **must not** reverse an ascending sort to
descend.

`select` with no columns **must** raise `'select' needs at least one column.` rather
than answering an empty table.

`take` and `skip` beyond the end of the table **must** answer everything and nothing
respectively, not a fault. A count that is not a whole number at least zero is
`'count' must be a whole number, not '<value>'.`

`group` **must** put a whole table in each `rows` cell, and preserve the order in which
the distinct values first appeared.

A row built for `first`, `last`, `rows` or `group` **must** carry the columns in the
table's order and **must** omit a cell that is `None`.

## Documents

Four commands over XML and CSV files (decision
[0011](../decisions/0011-real-xml-files.md)). The readers **must** resolve and read
their file exactly as `cat` does, raise the same faults for a missing path or a folder,
and emit no events. The writers **must** emit exactly the events `write` would for the
text they serialise, so that undo, redo and history treat a document like any other
file; a file they create **must** have kind `xml` or `csv` whatever its extension, and
a file they overwrite keeps its kind.

| Name | Parameters | Returns |
| --- | --- | --- |
| `from-xml` | `path` (piped) | the root element as an `Object` |
| `to-xml` | `path`, `value` (piped), `root` (optional), `row` (optional), `declaration` (optional) | `File` |
| `from-csv` | `path` (piped), `delimiter` (optional, default `,`) | `Table` |
| `to-csv` | `path`, `value` (piped), `delimiter` (optional, default `,`) | `File` |

### Reading XML

`from-xml` **must** parse the content as XML 1.0 and build a `Tag` per element:

- the element's name, as written with any prefix, is the `TypeName`;
- each attribute, as written with any prefix and including namespace declarations, is
  an attribute, in document order, its value read by the number-or-text rule a bare
  word is read by;
- the element's own text nodes that are not only whitespace, trimmed and joined with
  one space, are an attribute `text`, typed by the same rule, replacing any XML
  attribute of that name (decision [0025](../decisions/0025-xml-text-content.md));
- child elements are the children, in document order.

Comments and processing instructions **must** be dropped. A document with a DTD
**must** be refused. A document that does not parse **must** raise `Not well-formed XML
: <path> line <n>, position <m>` (`Invalid`), with the parser's line and position, at
least 1; the parser's own sentence **must not** be part of the message, since it differs
between hosts.

### Writing XML

`to-xml` **must** write:

- a `Tag` (`Object` or `Component`) as itself, renamed to `root` when given;
- a `Table` as a root named `root`, or `table`, with one child per row named `row`,
  or `row`, carrying the row's cells as attributes, a `None` cell omitted;
- a `List` as a root named `root`, or `list`, around its items, each of which **must**
  be a tag.

Anything else, or a list item that is not a tag, **must** raise `'to-xml' needs a tag, a
table or a list of tags, not <kind>.` (`Binding`). A name that is not an XML name
**must** raise `'<name>' is not a name XML allows.` (`Invalid`).

The output **must** be indented two spaces per level, one element per line, with each
line ending in `\n`. An attribute that is `None` or `Empty` is omitted. An attribute
named `text` **must** be written as the element's content, before its children; an
element with content and no children is written on one line. `&`, `<`, `>` and `\r`
are escaped in content, and additionally `"`, `\n` and `\t` in attribute values.
Values are written by their file text: a `Number` in its shortest exact form, anything
else by its display string. The declaration `<?xml version="1.0" encoding="UTF-8"?>` is
written only when `declaration` is.

### Reading CSV

`from-csv` **must** read RFC 4180: records end in `\n` or `\r\n`; a field in double
quotes may hold the delimiter, `""` for a quote, and line breaks; the line break after
the last record ends it. The first record is the header, and its fields name the
columns; an empty name raises `<path> column <n> has no name.`, and two names equal
ignoring case raise `<path> has two columns named '<name>'.`.

Every other record **must** have as many fields as the header, or raise `<path> line
<n> has <m> fields where the header has <k>.`, where `n` is the line the record began
on. A record that is one empty unquoted field is skipped when the header has more than
one column. An unclosed quote raises `<path> line <n>: a quoted field is never closed.`
and text after a closing quote `<path> line <n>: a closing quote is followed by more
text.`. All of these are `Invalid`.

An unquoted empty field **must** read as `None`. A column **must** be numeric, every cell
a `Number`, when every field in it that is not `None` reads as a number by the
number-or-text rule; otherwise every such field is `Text`. The empty file is the empty
table.

### Writing CSV

`to-csv` **must** coerce its value as a table function does, raising `'to-csv' needs a
table, not <kind>.`. It writes the header, then a record per row, each line ending in
`\n`, the last included. A cell that is `None` or `Empty` is an empty field. A field
**must** be quoted, with `"` doubled, when it contains the delimiter, `"`, `\n` or
`\r`, or is empty text, and **must not** be quoted otherwise. Values are written by
their file text, as for XML. A table with no columns is the empty file.

A `delimiter` is one character other than `"`, `\n` and `\r`, or the word `tab`;
anything else raises `'delimiter' must be one character, or 'tab', not '<text>'.`
(`Invalid`).

### help

| Field | Value |
| --- | --- |
| Name | `help` |
| Parameters | none |
| Returns | `Table` of `name`, `parameters` and `description` |
| Meta | yes |

One row per command, excluding `UnknownCommand`, ordered by name with an ordinal
comparison. `parameters` **must** be the declared parameters in order, each written
`<name>` when required, `[name]` when optional and `name...` when it collects the rest.

### run

| Field | Value |
| --- | --- |
| Name | `run` |
| Parameters | `path` (piped) |
| Returns | the value of the last line executed |
| Meta | yes |

Reads the file as text and splits it on line breaks. A line that is empty after
trimming, or whose first non-space character is `#`, **must** be skipped and **must**
still be counted.

Each remaining line **must** be parsed and executed exactly as if typed, committing its
own transaction, with `Transaction.Source` set to the line's trimmed text. `run` itself
**must** emit no events.

Each line **must** be written to the output as `> <line>` before it runs, followed by
the display string of its result when that is not empty.

Execution **must** stop at the first fault, and the fault **must** be re-raised with
the message `<path> line <n>: <message>`, keeping its kind, where `n` counts every line
in the file. Cancellation **must** be observed between lines. A `run` nested more than
eight deep is `Scripts are only allowed to run scripts 8 deep.`

Errors: `File does not exist : <path>`, `That is a directory, not a file : <path>`.

## Host commands

### exit

| Field | Value |
| --- | --- |
| Name | `exit` |
| Parameters | none |
| Returns | `Empty` |
| Undo | none |

Calls `IApplicationLifetime.Shutdown`. A command **must not** reach a window or a
process directly; what shutting down means is the host's decision, and a host with
nothing to close **may** do nothing.

### UnknownCommand

| Field | Value |
| --- | --- |
| Name | `UnknownCommand` |
| Parameters | `name` |
| Returns | never returns |

Raises `Unknown command : <name>`. It is not offered in listings or completion.

### undo

| Field | Value |
| --- | --- |
| Name | `undo` |
| Parameters | none |
| Returns | `Text` naming the line it reversed, or `Nothing to undo.` |
| Meta | yes |

Reverses the latest undoable, uncompensated line. Having nothing to undo **must not**
be a fault.

### redo

| Field | Value |
| --- | --- |
| Name | `redo` |
| Parameters | none |
| Returns | `Text` naming the line it restored, or `Nothing to redo.` |
| Meta | yes |

Reverses the latest undo that has not itself been reversed, and **must** name the
original line rather than the undo. Having nothing to redo **must not** be a fault.

### reset

| Field | Value |
| --- | --- |
| Name | `reset` |
| Parameters | none |
| Returns | `Text` naming how many files were restored |
| Meta | yes |

Empties the log and seeds it again. It is the only operation that removes anything
from the log, and it **must not** be undoable: the transactions that would have been
reversed are the ones it threw away. The description **must** say so, so `help` warns
before rather than after.

### history

| Field | Value |
| --- | --- |
| Name | `history` |
| Parameters | none |
| Returns | `Table` of `seq`, `at`, `source` and `undone` |
| Meta | yes |

One row per transaction, oldest first. `seq` is a `Number`, `at` the time as
`HH:mm:ss`, `source` the line as it was written, and `undone` a `Boolean` that is true
where the line's effect is not currently in force.
