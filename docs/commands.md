# Command reference

Every command available in the browser terminal, in the order `help` lists them.

Each entry gives the parameters in declaration order, which is the order positional
arguments fill them. A parameter marked **piped** takes the piped value when you do not
write it out. A parameter marked **optional** may be omitted.

Paths are relative to the current directory unless they start with a separator or a
drive. `..` and `.` work.

## Summary

| Command | Purpose | Changes anything? |
| --- | --- | --- |
| [`attr`](#attr) | Show a file's attributes, or set them | only when given `name=value` |
| [`back`](#back) | Go back to where you were before the last move | yes, where you are |
| [`columns`](#columns) | A table's columns and their types | no |
| [`count`](#count) | How many rows a table has | no |
| [`cp`](#cp) | Copy a file into a directory | yes |
| [`distinct`](#distinct) | Unique rows, or a column's unique values | no |
| [`download`](#download) | Download a file, with progress | yes |
| [`echo`](#echo) | Return an argument or the piped value | no |
| [`exit`](#exit) | Ask the host to close | no |
| [`find`](#find) | List every record a predicate is true of | no |
| [`first`](#first) | The first row of a table | no |
| [`from-csv`](#from-csv) | Read a CSV file as a table | no |
| [`from-xml`](#from-xml) | Read an XML file as a tag, or a table | no |
| [`group`](#group) | Gather the rows that share a value | no |
| [`help`](#help) | The commands, as a table | no |
| [`history`](#history) | The lines that changed something | no |
| [`in`](#in) | Go into a folder, a saved view, or a question | yes, where you are |
| [`is-fault`](#is-fault) | Whether a value is a fault that `try` or `else` caught | no |
| [`last`](#last) | The last row of a table | no |
| [`ls`](#ls) | List a directory, or the view you are in | no |
| [`mkdir`](#mkdir) | Create a directory | yes |
| [`out`](#out) | Come out of the view, or up out of the folder | yes, where you are |
| [`pick`](#pick) | The elements of a tree a CSS selector matches, as a table | no |
| [`progress`](#progress) | Run a progress bar, to exercise long commands | no |
| [`pwd`](#pwd) | Where you are: a directory, or a view | no |
| [`read`](#read) | Show what a file says | no |
| [`redo`](#redo) | Put back what `undo` took away | yes |
| [`reset`](#reset) | Empty the log and start again | yes, and cannot be undone |
| [`rm`](#rm) | Delete a file or empty directory | yes |
| [`rows`](#rows) | A table's rows, as objects | no |
| [`run`](#run) | Run a script, a line at a time | whatever its lines change |
| [`save`](#save) | Create a file from a tag | yes |
| [`save-view`](#save-view) | Keep a predicate as a file you can enter | yes |
| [`select`](#select) | Keep only the named columns | no |
| [`set`](#set) | Bind a value to a variable | yes |
| [`skip`](#skip) | Drop the first rows | no |
| [`sort`](#sort) | Order a table's rows by a column | no |
| [`table`](#table) | Read a tag as a table | no |
| [`take`](#take) | Keep the first rows | no |
| [`to-csv`](#to-csv) | Write a table to a file as CSV | yes |
| [`to-xml`](#to-xml) | Write a tag, a table or a list of tags as XML | yes |
| [`undo`](#undo) | Reverse the last line that changed something | yes |
| [`vars`](#vars) | List the variables in scope | no |
| [`where`](#where) | Keep the rows a predicate is true for | no |
| [`write`](#write) | Write text to a file | yes |

The thirteen table functions — `columns`, `count`, `distinct`, `first`, `group`,
`last`, `rows`, `select`, `skip`, `sort`, `table`, `take` and `where` — all take their
table from the pipe, coerce a table-shaped tag, and change nothing.
[Tables and predicates](tables.md) is the guide to them; this is the reference.
The four document commands — `from-csv`, `from-xml`, `to-csv` and `to-xml` — are covered
in [Reading and writing files](tables.md#reading-and-writing-files). `pick` reads inside
a tag that is a tree rather than a table, typed or read from a file:
[Reading a tree with `pick`](tables.md#reading-a-tree-with-pick).

`in`, `out` and `read` were called `cd`, `up` and `cat`; typing an old name offers the
new one, and running it names the new one in a note, with the line to run
([decision 0037](decisions/0037-in-out-back-and-read.md)). `back` is new with them.

The column says whether the command produces events. A line made only of commands that
change nothing leaves no trace at all, which is why `undo` after `ls` reverses the line
before the `ls` rather than the `ls` itself. See
[How it works](concepts.md#4-commit).

A live view re-runs a line only when every command in it is declared read-only, and
refuses any other line with `A live refresh only re-reads : <line>`. The read-only
commands are the ones marked "no" above except `exit` and `progress`.

A name that is not a command fails with `Unknown command : <name>`. That includes
`UnknownCommand`, the name the terminal uses internally to report one. When a command
or two are a slip away, or the name is one of a command's keywords, an old name such as
`cd` or a word for what it does such as `delete`, a note under the message says
`Did you mean …?` and offers the line with each in its place. `help <command>` describes
one command's parameters.

What the terminal adds of its own comes beside the answer as a **note**, never inside a
message ([decision 0041](decisions/0041-guidance-is-drawn-apart-from-output.md)). A
**suggestion** is under a fault: the command, file, folder or variable you probably
meant ([decision 0042](decisions/0042-a-missing-name-names-the-nearest.md)), or the
question a predicate probably meant to ask. An **explanation** is under an empty answer:
why `where`, `find` or a view kept no row of a table that had some
([decision 0043](decisions/0043-an-empty-filter-explains-itself.md)). Either can offer
**fixes**, whole corrected lines, which the page draws as chips that fill the input and
do not run it ([decision 0044](decisions/0044-a-fault-may-carry-fixes.md)). In the
examples here, a note is shown indented under the answer:

```
$ lss
Unknown command : lss
  suggestion: Did you mean ls?
  fix: ls
```

A note is not part of any value: `try`, `else` and `$problem.message` see the message
alone. [Notes beside a message](errors.md#notes-beside-a-message) has the rules.

A command called wrongly shows its help
([decision 0038](decisions/0038-a-wrong-call-shows-its-help.md)). When what you wrote
does not fit what a command declared (an argument missing, one too many, a flag it does
not have, a word a switch does not take), the line fails with the same fault as ever,
and the page draws that command's help under the error: its description and the table
`help <command>` answers, in a panel labelled `help`. Below, the indented lines under
an error are that panel:

```
$ read
'read' needs an argument for 'path'.
  help
  Show what a file says
  name  required  piped  takes   description
  path  true      true   a path  The file to read
```

A fault raised while the command runs is not a wrong call, and shows no help:
`read missing.txt` says `File does not exist : /missing.txt` and nothing more. Nor does an
unknown command, whose note says what it probably meant. A question that never reads
`$row` is a wrong call of `where`, `find`, `in` or `save-view`, and shows both: the note
that says what to write, and the help under it. `else` and `try` see the same fault
either way.

`clear` is handled by the page rather than by a command: it is about the screen rather
than about the filesystem. See
[The web terminal](web-terminal.md#words-the-page-handles-itself).

---

## attr

Shows a file's attributes, or writes new ones.

```
attr <path> [name=value ...]
```

| Parameter | Required | Meaning |
| --- | --- | --- |
| `path` | yes, or piped | The file |
| `name=value` | no | Attributes to set, written with no spaces around the `=` |

With no assignments it answers a table of `name` and `value`, one row per attribute the
record carries:

```
$ attr readme.txt
name      value
created   2026-09-22T09:30:00.0000000+00:00
folder    /
kind      text
modified  2026-09-22T09:30:00.0000000+00:00
name      readme.txt
```

With assignments it writes them and returns the file. The names are yours: a file can
carry any attribute you like, which is the point of an attribute filesystem.

```
$ attr readme.txt tag=work due=2026-10-01
readme.txt
```

`name` and `kind` may be set, and renaming to a name already used in the folder is an
error. `folder`, `created` and `modified` are the terminal's and are refused, and so is
`size`, which is not stored at all:

```
$ attr readme.txt modified=yesterday
'modified' is set by the terminal and cannot be written.
$ attr readme.txt size=3
'size' is worked out from the content and cannot be written.
```

Renaming a directory carries everything inside it, and moves you too if you are
standing in it. It is one line, so one `undo` puts it all back:

```
$ attr documents name=docs
docs
$ ls docs
name       kind  folder  size  modified
notes.txt  text  /docs   50    2026-09-22T09:30:00.0000000+00:00
$ undo
Undone: attr documents name=docs
$ ls documents
name       kind  folder      size  modified
notes.txt  text  /documents  50    2026-09-22T09:30:00.0000000+00:00
```

A directory keeps the kind `folder`. A record with no content, such as one made by
[`save`](#save), may become a directory with `kind=folder`; a file with content may not.

Assignments are data, not named parameters. A command that does not collect them says
so rather than ignoring them:

```
$ progress steps=20
'progress' does not take 'steps=' assignments.
```

Undo restores every attribute the record had.

**Errors**

| Message | Cause |
| --- | --- |
| `File does not exist : <path>` | Nothing at that path. |
| `'<name>' is set by the terminal and cannot be written.` | An assignment to `folder`, `created` or `modified`. |
| `'size' is worked out from the content and cannot be written.` | An assignment to `size`. |
| `Target file already exists : <path>` | A `name=` assignment names something already in the folder. |
| `A file must have a name.` | `name=""`. |
| `'<name>' is not a valid file name: '/' separates directories.` | The new name has a `/` in it. |
| `'<name>' is not a valid file name: it already names a directory.` | The new name is `.` or `..`. |
| `A directory cannot change its kind : <path>` | A `kind=` assignment on a directory. |
| `A file with content cannot become a directory : <path>` | `kind=folder` on a file with content. |

---

## back

Goes back to where you were before the last move, like a browser's back button.

```
back
```

**Parameters** none.

`in` and `out` leave a trail of the places they left, folders and views alike. `back`
goes to the most recent and takes it off the trail, so each `back` goes one step further;
it does not add to the trail itself. **Returns** where it took you: a directory as a
file, the root as the text `/`, a view as its question.

```
$ in documents
documents
$ in /examples
examples
$ back
documents
$ back
/
$ back
Nowhere further back: you are in /
```

At the start of the trail, as in a fresh tab, it is not a fault: it says where you are
and that there is nowhere further back, and changes nothing. In a view it names the
view's question instead of the folder.

A view is a place like any other, so `back` returns to one:

```
$ in $row.kind eq folder
$row.kind eq folder
$ in documents
documents
$ back
$row.kind eq folder
$ back
/
```

A place that is no longer there, a folder since deleted or renamed, is passed over, and
so is the place you are already in:

```
$ mkdir scratch
scratch
$ in scratch
scratch
$ in /documents
documents
$ rm /scratch
Removed scratch
$ back
/
```

**Undo** returns you to where you were before the `back`, and puts the place back on the
trail:

```
$ in documents
documents
$ back
/
$ undo
Undone: back
$ pwd
/documents
```

The trail is kept in the log, so it survives a reload.

---

## columns

The table's columns and the type each one holds.

**Parameters**

| Name | Notes |
| --- | --- |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** a table of `name` and `type`. A type is `text`, `number`, `boolean`,
`file`, `object` or `mixed`, and is read off the cells rather than declared.

```
$ ls | columns
name      type
name      file
kind      text
folder    text
size      number
modified  text
```

---

## count

How many rows there are.

**Parameters**

| Name | Notes |
| --- | --- |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** a number.

```
$ ls | count
5
$ ls | where $row.kind eq folder | count
4
```

**Errors**

| Message | Cause |
| --- | --- |
| `'count' needs a table, not text.` | Something that is not a table, and cannot be read as one, was piped in. |

---

## cp

Copies a file into a directory. The copy keeps the source's file name.

**Parameters**

| Name | Notes |
| --- | --- |
| `sourcePathAndFile` | The file to copy. |
| `targetPath` | The directory to copy it into. |

**Returns** the new file.

```
$ cp readme.txt documents
readme.txt
```

`cp` copies files only; a directory is refused.

**Undo** deletes the copy.

**Errors**

| Message | Cause |
| --- | --- |
| `File does not exist : <path>` | No such source file. |
| `Target directory does not exist : <path>` | No such destination directory. |
| `Target file already exists : <path>` | Refuses to overwrite; use `write` or `rm` first. |
| `'cp' copies files, and <path> is a directory.` | The source is a directory. |

---

## distinct

Unique rows, or the unique values of one column.

**Parameters**

| Name | Notes |
| --- | --- |
| `column` | optional. A column, to take the unique values of it. |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** a table. With no column, the rows with duplicates removed; with one, a
single-column table of that column's unique values, in the order they first appear.

```
$ ls | distinct kind
kind
folder
text
```

---

## download

Downloads a file into a directory, writing progress while it runs. This is an
asynchronous command: it can be stopped, and it reports what it wrote.

**Parameters**

| Name | Notes |
| --- | --- |
| `url` | optional. Defaults to this project's README on raw.githubusercontent.com. |
| `into` | optional. Defaults to the current directory. |

**Returns** the downloaded file. If a file of that name is already in the directory,
its content is replaced and its `modified` time updated, as `write` does.

```
$ download
100%
====================================================================================================>
0.00 MB
Downloaded to /README.md
README.md
```

It writes three lines: a percentage, a bar of a hundred characters, and the size of
what arrived in megabytes, to two decimal places. The whole body is read before any of
them is filled in, so they go from empty to complete in one step. The file name is the
last segment of the URL's path.

**Undo** deletes the downloaded file.

**Errors**

| Message | Cause |
| --- | --- |
| `Not a valid URL : <text>` | The URL is not absolute, or its path has no file name at the end. |
| `Target directory does not exist : <path>` | No such destination directory. |
| `That is a directory, not a file : <path>` | A directory of the downloaded file's name is already there. |
| `The server did not report a content length.` | The response had no `Content-Length`. |
| `Download failed : <reason>` | The request threw, for example because the host refused it. |

In a browser, a download is subject to the origin's CORS policy. Hosts that do not send
permissive headers fail regardless of the URL being valid. See
[Troubleshooting](troubleshooting.md#download-fails-in-the-browser-but-the-url-works-elsewhere).

---

## echo

Returns its argument unchanged, or whatever was piped in. Useful for turning a word
into a value, and for seeing what a pipe is carrying.

**Parameters**

| Name | Notes |
| --- | --- |
| `text` | piped. The value to return. |

```
$ echo hello
hello
$ echo 42
42
$ echo "hello world"
hello world
$ ls | echo
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
guide       folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       193   2026-09-22T09:30:00.0000000+00:00
```

The value keeps its type: `echo 42` returns a number, and `ls | echo` returns the table
itself rather than a printed copy of it. After `else`, what is piped in is the fault,
so `echo` with nothing written shows what went wrong:

```
$ read missing.txt else echo
File does not exist : /missing.txt
```

**Errors**

| Message | Cause |
| --- | --- |
| `'echo' needs an argument for 'text'.` | Nothing written and nothing piped in. |

---

## exit

Asks the host application to shut down.

**Parameters** none.

In the browser there is nothing to close, so the command succeeds and returns nothing.
In the desktop shell it closes the window.

---

## find

Lists every record a predicate is true of, wherever it is, without going anywhere.

**Parameters**

| Name | Notes |
| --- | --- |
| `predicate` | An expression over `$row`, such as `$row.kind eq note`. |

**Returns** a table of the matching records, in the same shape [`ls`](#ls) produces.
The `folder` column is what says where each row came from.

These examples, and the view examples under [`ls`](#ls), assume a journal like the one
`examples/journal.clr` builds, plus a postcard at the root:

```
$ mkdir journal
journal
$ in journal
journal
$ save <note name=monday mood=good tag=work/>
monday
$ save <note name=tuesday mood=tired tag=work/>
tuesday
$ save <note name=saturday mood=great tag=home/>
saturday
$ in /
/
$ save <note name=postcard mood=great tag=home/>
postcard
```

```
$ find $row.mood eq great
name      kind  folder    size  modified                           mood   tag
postcard  note  /         0     2026-09-22T09:30:00.0000000+00:00  great  home
saturday  note  /journal  0     2026-09-22T09:30:00.0000000+00:00  great  home

$ find $row.kind eq note and $row.tag eq work | count
2

$ find $row.mood eq grate
name  kind  folder  size  modified
  explanation: mood is great, good or tired
  fix: find $row.mood eq great
```

When nothing answers, a note says why: here, the moods there are, and the one a slip
from what was written, as a fix. [An empty answer says why](tables.md#an-empty-answer-says-why)
says what is explained.

`find` asks a question once; [`in`](#in) on the same predicate moves into it, so every
`ls` afterwards asks it again. Asking changes nothing, so a `find` leaves no
transaction and `undo` reaches past it.

**Errors**

| Message | Cause |
| --- | --- |
| `'find' needs a predicate, such as $row.kind eq note.` | A plain word was written instead of a question. |
| `'find' needs an argument for 'predicate'.` | Nothing was written. |
| `kind eq note never reads $row, so it is the same for every row.` | A question that never mentions the row. A note offers `find $row.kind eq note`. |

---

## first

The first row of a table.

**Parameters**

| Name | Notes |
| --- | --- |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** the row as an object of type `row`, or nothing when the table is empty.
Nothing is an answer, not a failure, and `??` is how to give it a default.

From a fresh tab:

```
$ ls | sort name desc | first
<row name=readme.txt kind=text folder=/ size=193 modified=2026-09-22T09:30:00.0000000+00:00/>
$ first (ls | where $row.kind eq view) ?? "no views yet"
no views yet
  explanation: kind is folder or text
```

The explanation is the `where`'s, which kept nothing: a note, not part of the answer.

The table can be written as a [pipeline in parentheses](language.md#pipelines-in-parentheses),
with a space before it: `first (ls)`. Written against the name, `first(ls)` is the
function form, and hands `first` the word `ls`.

---

## from-csv

Reads a CSV file with a header row as a table.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | piped. The file to read. |
| `delimiter` | optional. The character between fields: `,` by default, `tab` for a tab. |

**Returns** a table. The header names the columns. A column is a number column when
every cell in it reads as a number, and text otherwise. An empty field is a gap;
`""` is empty text.

In `/stock`, after `run examples/inventory.clr` has written `reorder.csv`:

```
$ read reorder.csv
sku,qty
C3,0
B2,12

$ from-csv reorder.csv | sort qty desc
sku  qty
B2   12
C3   0
```

Quoted fields may hold the delimiter, doubled quotes and line breaks, as RFC 4180 has
it. A blank line is skipped when the header has more than one column.

**Errors**

| Message | Cause |
| --- | --- |
| `File does not exist : <path>` | Nothing at that path. |
| `That is a directory, not a file : <path>` | The path names a directory. |
| `<path> line <n> has <m> fields where the header has <k>.` | A record is wider or narrower than the header. |
| `<path> line <n>: a quoted field is never closed.` | A quote opens a field and nothing closes it. |
| `<path> line <n>: a closing quote is followed by more text.` | Something other than a delimiter or a line break follows a quoted field. |
| `<path> has two columns named '<name>'.` | Two header cells name the same column, ignoring case. |
| `<path> column <n> has no name.` | An empty header cell. |
| `'delimiter' must be one character, or 'tab', not '<text>'.` | The delimiter is longer than one character, or is a quote or a line break. |

---

## from-xml

Reads an XML file into the tree the tag notation produces.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | piped. The file to read. |

**Returns** the root element as a tag: its name is the type, its attributes are typed
by the number-or-text rule a bare word gets, and its child elements are its children.
A table-shaped document is a table wherever one is expected.

In `/stock`, after `run examples/inventory.clr` has written `items.xml`:

```
$ from-xml items.xml | count
3
$ from-xml items.xml | sort qty desc | first
<row sku=A1 name=bolts qty=120 min=50/>
$ <memo to=ann text="Back at ten."/> | to-xml memo.xml
memo.xml
$ read memo.xml
<memo to="ann">Back at ten.</memo>

$ from-xml memo.xml
<memo to=ann text=Back at ten./>
```

An element's text is read as an attribute called `text`
([decision 0025](decisions/0025-xml-text-content.md)). Comments and processing
instructions are dropped; a document with a DTD is refused.

**Errors**

| Message | Cause |
| --- | --- |
| `File does not exist : <path>` | Nothing at that path. |
| `That is a directory, not a file : <path>` | The path names a directory. |
| `Not well-formed XML : <path> line <n>, position <m>` | The file is not XML, or has a DTD. The line and position are where the parser gave up. |

---

## group

Gathers the rows that share a column's value.

**Parameters**

| Name | Notes |
| --- | --- |
| `column` | The column to group by. |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** a table of `key` and `rows`, one row per distinct value, in the order the
values first appear. Each `rows` cell holds a whole table; a cell is one line, so it
reads as its row count, and `rows` is how you look inside one.

```
$ ls | group kind
key     rows
folder  4 rows
text    1 row
```

**Errors**

| Message | Cause |
| --- | --- |
| `'group' has no column named '<name>'.` | No such column. `columns` lists what there is. |

---

## help

Every command, with its parameters and what it does; or one command, parameter by
parameter.

**Parameters**

| Name | Notes |
| --- | --- |
| `command` | optional. The command to describe; every command when it is not written. |

**Returns**, with no command, a table of `name`, `parameters` and `description`. A
required parameter is written `<name>`, an optional one `[name]`, and one that collects
the rest `name...`.

```
$ help | take 4
name     parameters            description
attr     <path> [assignments]  Show a file's attributes, or set them with name=value
back                           Go back to where you were before the last move
columns  [table]               The table's columns and their types
count    [table]               How many rows there are
$ help | where $row.name eq set | select name description
name  description
set   Bind a value, or whatever was piped in, to a variable
```

With a command, it writes what the command does and answers a table of its parameters,
in the order positional arguments fill them:

| Column | Holds |
| --- | --- |
| `name` | The parameter's name. |
| `required` | `true` when it has to be given, written or piped. |
| `piped` | `true` when it takes the piped value if it is not written. |
| `takes` | What kind of thing goes there: `a column`, `a predicate`, `desc or asc`, `a path`, `a value` and so on. |
| `description` | What it is for. |

```
$ help where
Keep the rows a predicate is true for
name       required  piped  takes        description
predicate  true      false  a predicate  An expression over $row, such as $row.kind eq folder
table      false     true   a value      The table to work on; taken from the pipe when it is not written
$ help where | count
Keep the rows a predicate is true for
2
$ help lss
Unknown command : lss
  suggestion: Did you mean ls?
  fix: help ls
```

The description is written above the table rather than being part of it, so the table
can be questioned like any other: `help where | count` counts the parameters.

The same description and table are what the page draws under a command called wrongly
(see [the summary](#summary)). `help` called wrongly shows its own, since `help` is the
command that was called wrongly, not the one it was asked about:

```
$ help where extra
'help' takes 1 argument, but 2 were given.
  help
  The commands, with their parameters and what they do
  name     required  piped  takes           description
  command  false     false  a command name  The command to describe; every command when it is not written
```

It changes nothing and leaves nothing to undo.

---

## history

The lines that changed something, oldest first.

```
history
```

**Returns** a table of `seq`, `at`, `source`, `undone` and `compensates`. In a fresh
tab, after `mkdir alpha` and `write note.txt hello`:

```
$ history
seq  at        source                undone  compensates
1    09:30:00  seed                  false
2    09:30:00  mkdir alpha           false
3    09:30:00  write note.txt hello  false
```

Each row is a sequence number, the time, the line exactly as it was typed, whether its
effect has been reversed, and, for an undo or a redo, the sequence number of the
transaction it reverses:

```
$ undo
Undone: write note.txt hello
$ history
seq  at        source                undone  compensates
1    09:30:00  seed                  false
2    09:30:00  mkdir alpha           false
3    09:30:00  write note.txt hello  true
4    09:30:00  write note.txt hello  false   3
```

The fourth row is the undo itself, which is a transaction in its own right: it carries
the name of the line it reversed, and `compensates` says it reverses row 3. An undo or a
redo is never itself marked undone. Lines that changed nothing, such as `ls`, never
appear, because they were never recorded.

Being a table, it can be questioned:

```
$ history | where $row.undone eq true | count
1
```

`seed` is the filesystem the session started with. It is shown, and it cannot be undone.

---

## in

Goes into somewhere. Somewhere is a directory, a saved view, or a question written out.

**Parameters**

| Name | Notes |
| --- | --- |
| `TargetPath` | piped. A directory, a view file, or a predicate. |

**Returns** the directory it entered, as a file, or the predicate, as a query. The
root has no record, so entering it returns the text `/`.

```
$ in documents
documents
$ in ../projects
projects
$ echo /documents | in
documents
$ in $row.mood eq great
$row.mood eq great
```

What decides between the two is whether an operator was written. `in journal` is a
name: a directory, or a file of kind `view`, whose predicate is entered instead.
`in $row.mood eq great` has `eq` in it, so it is a question, and entering it sets the
view without moving out of the directory you are in — new files still land there.
See [The filesystem](filesystem.md#views).

`in ..` normalises, so the working directory shows the parent's real name rather than a
path ending in `..`. Entering a directory puts down whatever view was held.

The place it left goes on the trail [`back`](#back) retraces. Going in where you already
are is no move, and leaves nothing on it.

**Undo** returns you to where you were, view and all.

**Errors**

| Message | Cause |
| --- | --- |
| `'in' needs an argument for 'TargetPath'.` | No path given and nothing piped in. The page shows `in`'s help under it. |
| `kind eq folder never reads $row, so it is the same for every row.` | A question that never mentions the row, so it would hold of everything or nothing. A note offers `in $row.kind eq folder`. |
| `Directory does not exist : <path>` | No such directory, and no view file of that name. The path is shown as you wrote it. A note names the nearest folders, if any. |
| `'<path>' does not hold a predicate : <text>` | A view file whose content is not a predicate, such as `'/weekend' does not hold a predicate : monday`. |

---

## is-fault

Whether a value is a fault: the result of a stage written with `try`, or what `else`
pipes into the pipeline after it. See [Errors as values](language.md#errors-as-values).

**Parameters**

| Name | Notes |
| --- | --- |
| `value` | optional, piped. The value to ask about. |

**Returns** `true` or `false`. Given nothing at all it answers `false`: nothing is not a
failure.

```
$ try read missing.txt | set problem
File does not exist : /missing.txt
$ is-fault $problem
true
$ echo fine | is-fault
false
$ read missing.txt else is-fault
true
```

---

## last

The last row of a table.

**Parameters**

| Name | Notes |
| --- | --- |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** the row as an object of type `row`, or nothing when the table is empty.

```
$ ls | sort name | last
<row name=readme.txt kind=text folder=/ size=193 modified=2026-09-22T09:30:00.0000000+00:00/>
```

---

## ls

Lists where you are: a directory, or the view you are in.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | optional. Defaults to the current directory, or to the current view. |

**Returns** a table. Five columns are always there — `name`, `kind`, `folder`, `size`
and `modified` — followed by every other attribute anything listed carries, ordered by
name. A file that does not carry one has a gap in that column.

```
$ ls
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
guide       folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       193   2026-09-22T09:30:00.0000000+00:00
$ ls documents
name       kind  folder      size  modified
notes.txt  text  /documents  50    2026-09-22T09:30:00.0000000+00:00
```

In a view, `ls` lists every record in the terminal the view matches, from whatever
directory, and the `folder` column is what says where each row came from. Writing a
path is how you look at a directory without leaving the view. With the journal and
postcard from [`find`](#find):

```
$ in $row.mood eq great
$row.mood eq great
$ ls
name      kind  folder    size  modified                           mood   tag
postcard  note  /         0     2026-09-22T09:30:00.0000000+00:00  great  home
saturday  note  /journal  0     2026-09-22T09:30:00.0000000+00:00  great  home
$ ls documents
name       kind  folder      size  modified
notes.txt  text  /documents  50    2026-09-22T09:30:00.0000000+00:00
```

`size` is the length of the file's content, worked out when the table is built rather
than stored, so it can never disagree with what `read` shows. `created` is not a column;
`attr` shows it.

There is no parent entry. A listing used to begin with a row that led up a level, and a
table of records has nowhere to put one: every row is a record, and the way out is not.
The browser offers `out` in the location line instead, and it runs the [`out`](#out)
command.

Everything in [Tables and predicates](tables.md) applies to a listing.

**Errors**

| Message | Cause |
| --- | --- |
| `Directory does not exist : <path>` | No such directory, or the path names a file. The path is shown as you wrote it. |

---

## mkdir

Creates a directory.

**Parameters**

| Name | Notes |
| --- | --- |
| `FolderName` | The directory to create, relative to the current one. |

**Returns** the new directory, as a file.

```
$ mkdir scratch
scratch
```

**Undo** deletes it.

**Errors**

| Message | Cause |
| --- | --- |
| `Target directory already exists : <path>` | Something is already there, a file or a directory. |
| `Directory does not exist : <path>` | The parent directory is missing. `mkdir` creates one level at a time. |

---

## out

Comes back out. In a view, that means putting the view down; otherwise it means the
parent directory.

**Parameters** none.

**Returns** the directory you are now in, as a path. With the journal from
[`find`](#find), starting at the root:

```
$ in journal
journal
$ in $row.mood eq great
$row.mood eq great
$ out
/journal
$ out
/
```

Two `out`s from a view over a subdirectory come out in the order they went in: the
question first, the directory second. At the root with no view, `out` stays at the root.

Like `in`, it puts the place it left on the trail, so [`back`](#back) after `out` goes
back in.

**Undo** returns you to where you were, view and all.

---

## pick

Answers a table of every element a CSS selector matches, from a tag, a list of tags,
or a table `pick` answered
([decision 0049](decisions/0049-pick-selects-elements-with-css-selectors.md)).

**Parameters**

| Name | Notes |
| --- | --- |
| `selector` | The CSS selector: an element name, `*`, attribute tests `[a]`, `[a=v]`, `[a^=v]`, `[a$=v]` and `[a*=v]`, several of those together, a space for a descendant, `>` for a child, and commas between alternatives. Quote it when it has a space, `>`, `[` or `,` in it. |
| `document` | optional, piped. The tag, list of tags or table of elements to read. Taken from the pipe when it is not written. |

**Returns** a table with a row for every element matched, the one piped in included, in
document order, each element once. The columns are `@tag`, the element's name; the
attributes of the elements matched, in the order they first appear, with a gap where
an element lacks one; and `@children`, the element's children as a list, which a cell
shows as `N children`. Nothing matched is a table with only `@tag` and `@children`.

From a fresh tab:

```
$ set d <library city=paris><book title=dune year=1965><author name=herbert/></book><book title=emma year=1815><author name=austen/></book><shelf/></library>
<library city=paris><book title=dune year=1965><author name=herbert/></book><book title=emma year=1815><author name=austen/></book><shelf/></library>
$ $d | pick book
@tag  title  year  @children
book  dune   1965  1 child
book  emma   1815  1 child
$ $d | pick "book > author" | select name
name
herbert
austen
$ $d | pick book | where $row.year gt 1900 | select title
title
dune
$ $d | pick book | pick author | select name
name
herbert
austen
```

Names and values are compared exactly, and an attribute as the text it shows. A table
whose rows have an `@tag` column is read back as the elements they were made from, so
`pick` reads what `pick` answered; where the documents given overlap, an element inside
two of them is still answered once
([decision 0052](decisions/0052-pick-answers-each-element-once.md)). A list of tags,
such as `$d.@children`, is that many documents. On the page, completion offers the
element names of what flows in, as `book · 2 elements`.
[Reading a tree with `pick`](tables.md#reading-a-tree-with-pick) is the guide, with a
document read from a file. It changes nothing.

**Errors**

| Message | Cause |
| --- | --- |
| `The selector '<selector>' stops at <where>: <what was expected>.` | A selector outside the subset. `<where>` is `character <n>, '<c>'`, counted from one, or `its end`. Kind `syntax`, and `else` and `try` catch it. [Selector errors](errors.md#selector-errors) lists them. |
| `The selector is empty: write an element name, such as 'book', or '*' for every element.` | `pick ""`. Kind `syntax`. |
| `'pick' needs a tag, a list of tags or a table with a @tag column, not <kind>.` | Nothing with elements in it was piped in, such as text, a listing, or with `not empty`, nothing at all. |
| `'pick' cannot read row <n> as an element: its @tag is empty.` | A table with an `@tag` column has a row with nothing in it. |

---

## progress

Runs a progress bar. It exists to exercise long-running commands: live output,
cancellation and undo. It changes nothing.

**Parameters**

| Name | Notes |
| --- | --- |
| `steps` | optional. How many steps to take. Default 100. |
| `delay` | optional. Milliseconds between steps. Default 100. |

**Returns** the percentage reached, as a number.

```
$ progress 4 5
100%
=========================>
Progress test finished
100
$ progress -steps 2 -delay 1
100%
=========================>
Progress test finished
100
```

Press **Stop** while it runs and it writes `Cancelled at <n>%` under the bar, and the
line fails with `Stopped.`

It produces no events, so there is nothing for `undo` to reverse: `undo` after
`progress` reverses the line before it. It is not declared read-only, though, so a
live view does not re-run it. A delay of `0` runs the steps back to back.

**Errors**

| Message | Cause |
| --- | --- |
| `'steps' must be at least 1.` | `steps` was zero or negative. |
| `'steps' must be a whole number, not '<value>'.` | `steps` was not a whole number, for example a bare `-steps` flag. |
| `'delay' must be a whole number, not '<value>'.` | `delay` was not a whole number, such as `1.5`. |
| `'delay' must be zero or more, not '<value>'.` | `delay` was negative. |

---

## pwd

Returns where you are: the current directory, or the view you are in.

**Parameters** none.

**Returns** the directory's path as text, or the view as a query.

```
$ in documents
documents
$ pwd
/documents
$ in $row.mood eq great
$row.mood eq great
$ pwd
$row.mood eq great
```

The line above the input shows the same thing at all times, and says which of the two
it is.

---

## read

Shows what a file says: its contents, as text.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | piped. The file to read. |

**Returns** the file's text. The terminal renders it as a block rather than a chip, so
line breaks survive.

```
$ read readme.txt
This is a command line that runs in this browser tab.

The guide folder explains how it works, one idea per file. Start with the first:

  read guide/1-start.txt

or list them all:

  ls guide

$ echo documents/notes.txt | read
Try: ls, in documents, mkdir scratch, echo "hello"
$ read(path: readme.txt)
This is a command line that runs in this browser tab.

The guide folder explains how it works, one idea per file. Start with the first:

  read guide/1-start.txt

or list them all:

  ls guide

```

A file that is not there names the nearest ones, when there are any: one a slip away in
the folder it was looked for in, or else the same name, or a name it is the start of,
anywhere. The fix writes it where the path was, however the path was given:

```
$ read notes
File does not exist : /notes
  suggestion: Did you mean documents/notes.txt?
  fix: read documents/notes.txt
$ echo notes | read
File does not exist : /notes
  suggestion: Did you mean documents/notes.txt?
  fix: echo documents/notes.txt | read
```

**Errors**

| Message | Cause |
| --- | --- |
| `File does not exist : <path>` | Nothing at that path. A note names the nearest files, if any. |
| `That is a directory, not a file : <path>` | The path names a directory. |

---

## redo

Puts back what `undo` took away, and names the line it restored.

```
redo
```

```
$ mkdir alpha
alpha
$ undo
Undone: mkdir alpha
$ redo
Redone: mkdir alpha
```

With nothing undone it says so, and that is not an error:

```
$ redo
Nothing to redo.
```

Redo is undo applied to an undo, so `undo`, `redo`, `undo` leaves you where the first
`undo` did.

In the web terminal a redo leaves no entry of its own when the line it restores is on
screen: that line's entry comes back where it was instead. See
[Undo on screen](web-terminal.md#undo-on-screen).

---

## reset

Empties the log and starts again from the seeded filesystem.

```
reset
```

```
$ reset
Reset. 17 files restored.
```

This is the only command that cannot be undone. The lines it would have been undone
from are the ones it threw away:

```
$ reset
Reset. 17 files restored.
$ undo
Nothing to undo.
```

It is the way out of a log that cannot be read back, and the way to start a session
over. Because it cannot be reversed, it is deliberately not one of the suggestion keys
under the input: you have to type it.

---

## rm

Deletes a file, or a directory that is empty.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | piped. What to delete. |

**Returns** text naming what it removed.

```
$ rm note.txt
Removed note.txt
```

A directory with anything in it is refused: undoing that would mean restoring a whole
tree, and a one-word recursive delete is not a safe thing to hand a phone. Empty it
first.

**Undo** puts the file back with its contents, or recreates the directory.

**Errors**

| Message | Cause |
| --- | --- |
| `Nothing exists at : <path>` | No such file or directory. |
| `Directory is not empty : <path>` | Delete the contents first. |
| `Cannot delete the current directory.` | Move out of it first. |

---

## rows

A table's rows, as objects rather than as a table.

**Parameters**

| Name | Notes |
| --- | --- |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** a list of objects of type `row`.

```
$ ls | select name kind | rows
<row name=documents kind=folder/> <row name=examples kind=folder/> <row name=guide kind=folder/> <row name=projects kind=folder/> <row name=readme.txt kind=text/>
```

---

## run

Runs a script: every line in it, as if it had been typed.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | piped. The script to run. |

**Returns** the value of the last line it ran.

A script is a text file of command lines, one per line, conventionally with the
extension `.clr` — which infers the kind `script`. Blank lines and lines whose first
non-space character is `#` are skipped.

Each line is executed exactly as if typed, and **commits its own transaction**, so
`undo` after a script steps back a line at a time rather than taking the whole file
away. `run` itself commits nothing, which is what makes that possible. Each line is
written to the output as `> <line>`, followed by its result, so the scrollback shows
the run.

Execution stops at the first fault, and the message names the script and the line.
Skipped lines are counted, so the number is the one an editor shows.

```
$ run examples/tables.clr
> ls
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
guide       folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       193   2026-09-22T09:30:00.0000000+00:00
> ls | where $row.kind eq folder | count
4
> ls | sort name desc | first
<row name=readme.txt kind=text folder=/ size=193 modified=2026-09-22T09:30:00.0000000+00:00/>
> ls | select name kind | take 2
name       kind
documents  folder
examples   folder
> set greeting hello
hello
> set answer 42
42
> vars
name      value
answer    42
greeting  hello
> help | where $row.name eq set | select name description
name  description
set   Bind a value, or whatever was piped in, to a variable
name  description
set   Bind a value, or whatever was piped in, to a variable
```

The last table appears twice: once as the last line's output, and once as the value
`run` returns.

Four example programs are seeded into `/examples`, one per pillar of the design:
`tables.clr`, `journal.clr`, `resilient.clr` and `inventory.clr`. All four run to
completion. See [Worked examples](examples.md).

**Errors**

| Message | Cause |
| --- | --- |
| `File does not exist : <path>` | No such file. |
| `That is a directory, not a file : <path>` | The path names a directory. |
| `<path> line <n>: <message>` | A line failed. The lines before it have already committed. |
| `Scripts are only allowed to run scripts 8 deep.` | A script that runs a script that runs a script, eight times over — usually one that runs itself. Each level adds its own `<path> line <n>: ` in front. |

---

## save

Creates a file from a tag. The tag's type becomes the file's `kind`, its `name`
attribute becomes the name, and everything else is carried across.

```
save <tag/>
```

```
$ save <note name=todo due=2026-10-01/>
todo
$ attr todo
name      value
created   2026-09-22T09:30:00.0000000+00:00
due       2026-10-01
folder    /
kind      note
modified  2026-09-22T09:30:00.0000000+00:00
name      todo
```

A tag with attributes is exactly what a file record is, which is why this is one word
rather than a `write` followed by several `attr` calls.

The tag can be piped in: `echo <note name=todo/> | save`.

The `name` attribute is required, follows the same rules as a name `attr` sets, and a
name already used in the folder is an error. The tag may not carry `folder`, `created`,
`modified` or `size`, which the terminal works out. The file is created in the current
directory. It has no content, so `read` on it returns
empty text; `write` gives it some.

**Undo** deletes the file.

**Errors**

| Message | Cause |
| --- | --- |
| `A saved tag needs a 'name' attribute.` | The tag has no `name`, or an empty one. |
| `'<name>' is not a valid file name: <reason>.` | The name has a `/` in it, or is `.` or `..`. |
| `'<name>' is set by the terminal and cannot be written.` | The tag has a `folder`, `created` or `modified` attribute. |
| `'size' is worked out from the content and cannot be written.` | The tag has a `size` attribute. |
| `Target file already exists : <path>` | Something of that name is already here. |
| `'save' needs a tag, not <kind>.` | What was written or piped is not a tag. |

---

## save-view

Keeps a predicate as a file, so a question becomes somewhere you can go back to.

**Parameters**

| Name | Notes |
| --- | --- |
| `name` | What to call the view. |
| `predicate` | An expression over `$row`, such as `$row.mood eq great`. |

**Returns** the view it created, as a file.

```
$ save <note name=postcard mood=great/>
postcard

$ save-view cheerful $row.mood eq great
cheerful

$ ls
name        kind    folder  size  modified                           mood
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
guide       folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
cheerful    view    /       18    2026-09-22T09:30:00.0000000+00:00
postcard    note    /       0     2026-09-22T09:30:00.0000000+00:00  great
readme.txt  text    /       193   2026-09-22T09:30:00.0000000+00:00

$ read cheerful
$row.mood eq great

$ in cheerful
$row.mood eq great
```

A view is an ordinary record of kind `view` whose content is the predicate as it was
written, so it is listed, tapped, renamed with `attr`, undone and deleted like any
other file, and `read` shows what it asks. The view is created in the current
directory; what it matches is not limited to that directory.

**Undo** deletes the view.

**Errors**

| Message | Cause |
| --- | --- |
| `'save-view' needs a predicate, such as $row.kind eq note.` | A plain word was written instead of a question. |
| `'save-view' needs an argument for 'predicate'.` | Only a name was written. |
| `<predicate> never reads $row, so it is the same for every row.` | A question that never mentions the row. A note suggests the likely columns. |
| `Target file already exists : <path>` | Something of that name is already here. |
| `A view needs a name.` | The name was empty. |

---

## select

Keeps only the named columns, in the order named.

**Parameters**

| Name | Notes |
| --- | --- |
| `columns` | One or more column names. Collects every remaining positional argument. |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** a table of those columns.

```
$ ls | select name size
name        size
documents   0
examples    0
guide       0
projects    0
readme.txt  193
```

**Errors**

| Message | Cause |
| --- | --- |
| `'select' needs at least one column.` | Nothing was named. The pipe is the table, not the column list. |
| `'select' has no column named '<name>'.` | No such column. `columns` lists what there is. |

---

## set

Binds a value to a variable. This is how a command's result gets a name; tags bind
variables too, but only for objects and components.

**Parameters**

| Name | Notes |
| --- | --- |
| `name` | The variable's name, without the `$`. |
| `value` | piped. The value to bind. |

**Returns** the value, so `set` can sit in the middle of a pipeline.

```
$ set greeting hello
hello
$ ls | set files
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
guide       folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       193   2026-09-22T09:30:00.0000000+00:00
$ echo $greeting
hello
```

**Undo** restores the previous binding, or unbinds a new name.

**Errors**

| Message | Cause |
| --- | --- |
| `'set' needs an argument for 'value'.` | No value written and nothing piped in. |
| `'<name>' is not a valid variable name.` | Names take letters, digits and underscore. |
| `Unknown variable: $<name>` | You wrote `set $x 1`; `$x` reads the variable rather than naming it. |

---

## skip

Drops the first rows and keeps the rest.

**Parameters**

| Name | Notes |
| --- | --- |
| `count` | How many rows to drop. |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** a table. Dropping more rows than there are leaves none, which is not an
error.

```
$ ls | skip 3 | select name
name
projects
readme.txt
```

**Errors**

| Message | Cause |
| --- | --- |
| `'count' must be a whole number, not '<value>'.` | The count is negative, fractional or not a number. |

---

## sort

Orders the rows by a column.

**Parameters**

| Name | Notes |
| --- | --- |
| `column` | The column to order by. |
| `desc` | optional. Write `desc` after the column, or `-desc`, to order downwards; `asc`, or nothing, orders upwards. |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** a table. Numbers order numerically, everything else by the text on screen.
The sort is stable in both directions, so rows that compare equal keep the order they
arrived in and `sort name | sort size desc` leaves the equal sizes in name order.

```
$ ls | sort size desc | select name size
name        size
readme.txt  193
documents   0
examples    0
guide       0
projects    0
$ ls | sort size -desc | select name size
name        size
readme.txt  193
documents   0
examples    0
guide       0
projects    0
```

**Errors**

| Message | Cause |
| --- | --- |
| `'sort' has no column named '<name>'.` | No such column. |
| `'sort' takes 'desc' or 'asc' for 'desc', not '<word>'.` | Some other word was written after the column. |

---

## table

Reads a tag, or a list of tags, as a table.

**Parameters**

| Name | Notes |
| --- | --- |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** the table. The coercion is implicit wherever a table is expected, so this
is for seeing what a tag reads as, and for saying in a script that a value is meant to
be one.

```
$ <items><item sku=A1 name=bolts qty=120/><item sku=B2 name=nuts qty=12/><item sku=C3 qty=0/></items> | table
sku  name   qty
A1   bolts  120
B2   nuts   12
C3          0
```

A tag is table-shaped when every child has the same type and no child has children of
its own. The columns are the union of the children's attributes, in the order they
first appear, and a missing one is a gap.

**Errors**

| Message | Cause |
| --- | --- |
| `<items> is not a table: child 2 is <other> where the first is <item>.` | The children disagree about their type. |
| `<items> is not a table: child 2 has children of its own.` | A child is a tree rather than a row. |

---

## take

Keeps the first rows.

**Parameters**

| Name | Notes |
| --- | --- |
| `count` | How many rows to keep. |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** a table. Taking more rows than there are takes what there is.

```
$ ls | take 2 | select name
name
documents
examples
```

**Errors**

| Message | Cause |
| --- | --- |
| `'count' must be a whole number, not '<value>'.` | The count is negative, fractional or not a number. |

---

## to-csv

Writes a table to a file as CSV, with a header row.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | The file to write. |
| `value` | piped. The table, or anything that reads as one. |
| `delimiter` | optional. The character between fields: `,` by default, `tab` for a tab. |

**Returns** the file it wrote. A file it creates has kind `csv`.

With the `items.xml` from the [`to-xml`](#to-xml) example:

```
$ from-xml items.xml | where $row.qty lt $row.min | sort qty | select sku qty | to-csv reorder.csv
reorder.csv
```

Every line ends in a line break, including the last. A field is quoted only when it
holds the delimiter, a quote or a line break, or is empty text. A gap is an empty field.
Numbers are written with every digit they have, not rounded as a table displays them.

**Undo** restores the previous contents, or deletes the file if it did not exist.

**Errors**

| Message | Cause |
| --- | --- |
| `'to-csv' needs a table, not <kind>.` | What was piped is not a table and cannot be read as one. |
| `That is a directory, not a file : <path>` | The path names a directory. |
| `Directory does not exist : <path>` | The parent directory is missing. |

---

## to-xml

Writes a tag, a table or a list of tags to a file as XML.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | The file to write. |
| `value` | piped. The tag, table or list to write. |
| `root` | optional. The root element's name, instead of the tag's own, `table` or `list`. |
| `row` | optional. The name of a table's row elements; `row` by default. |
| `declaration` | optional. Write `-declaration` to begin with an XML declaration. |

**Returns** the file it wrote. A file it creates has kind `xml`.

```
$ <items><item sku=A1 name=bolts qty=120 min=50/><item sku=B2 name=nuts qty=12 min=40/><item sku=C3 name=washers qty=0 min=20/></items> | to-xml items.xml
items.xml
$ from-xml items.xml | select sku qty | to-xml short.xml -root stock -row line -declaration
short.xml
$ read short.xml
<?xml version="1.0" encoding="UTF-8"?>
<stock>
  <line sku="A1" qty="120"/>
  <line sku="B2" qty="12"/>
  <line sku="C3" qty="0"/>
</stock>
```

Pretty-printed with two spaces a level. A gap is an attribute that is not written, and
an attribute called `text` is written as the element's content. `-declaration` is a
switch: write it last, since a flag followed by a plain word takes the word as its value,
and `to-xml` refuses any value but none.

**Undo** restores the previous contents, or deletes the file if it did not exist.

**Errors**

| Message | Cause |
| --- | --- |
| `'to-xml' needs a tag, a table or a list of tags, not <kind>.` | The value, or an item in a list, has no elements in it. |
| `'<name>' is not a name XML allows.` | An element or attribute name XML cannot hold, such as a column with a space in it. |
| `'to-xml' takes '-declaration' on its own, not '<word>'.` | A word followed `-declaration`, as in `-declaration no`. |
| `That is a directory, not a file : <path>` | The path names a directory. |
| `Directory does not exist : <path>` | The parent directory is missing. |

---

## undo

Reverses the last line that changed something, and names it.

```
undo
```

```
$ write note.txt first
note.txt
$ write note.txt second
note.txt
$ undo
Undone: write note.txt second
$ read note.txt
first
```

It works on lines, not on commands. A line that changed nothing is not in the way:

```
$ mkdir alpha
alpha
$ ls
name        kind    folder  size  modified
alpha       folder  /       0     2026-09-22T09:30:00.0000000+00:00
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
guide       folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
note.txt    text    /       5     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       193   2026-09-22T09:30:00.0000000+00:00
$ undo
Undone: mkdir alpha
```

With nothing to undo it says so, and that is not an error. The seeded filesystem
cannot be undone, so this is what a fresh tab answers:

```
$ undo
Nothing to undo.
```

Undoing does not erase history: it appends the reverse, so `redo` can reverse it in
turn and `history` shows both.

The transcripts above show what `undo` answers. In the web terminal, when the line it
reverses is on screen, that line's entry disappears and the undo leaves no entry of its
own, because to the person typing, undo means the line did not happen. See
[Undo on screen](web-terminal.md#undo-on-screen).

---

## vars

Every variable in scope.

**Parameters** none.

**Returns** a table of `name` and `value`, ordered by name.

```
$ vars
name      value
entries   5 rows
greeting  hello
```

A cell is one line, so a variable holding a table says how many rows it has. It is
still the table: `echo $entries | count` answers 5.

With nothing bound it writes `No variables. Try: set greeting hello` beside an empty
table, so `vars | count` is 0 rather than a fault.

---

## where

Keeps the rows a predicate is true for.

**Parameters**

| Name | Notes |
| --- | --- |
| `predicate` | An expression over `$row`, such as `$row.kind eq folder`. |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** a table of the rows the predicate answered true for. The predicate is
evaluated once per row, in a scope of its own with `$row` bound to that row, so a
variable called `row` outside it is left alone.

```
$ ls | where $row.kind eq folder | count
4
```

A predicate that keeps no row answers the empty table, and a note says why. From a
fresh tab:

```
$ ls | where $row.kind eq foldr
name  kind  folder  size  modified
  explanation: kind is folder or text
  fix: ls | where $row.kind eq folder
$ ls | where $row.knd eq folder
name  kind  folder  size  modified
  explanation: No row has knd; did you mean kind?
  fix: ls | where $row.kind eq folder
```

A predicate can compare two columns. In `/stock`, after `run examples/inventory.clr`:

```
$ from-xml items.xml | where $row.qty lt $row.min | select name qty min
name     qty  min
nuts     12   40
washers  0    20
```

In `/journal`, with the notes from [`find`](#find):

```
$ ls | where not $row.tag eq work | select name
name
saturday
```

The operators are `eq ne gt ge lt le like has`, combined with `and`, `or` and `not`.
[Tables and predicates](tables.md#predicates) says what each one means.

**Errors**

| Message | Cause |
| --- | --- |
| `'where' needs an argument for 'predicate'.` | No predicate written. |
| `kind eq folder never reads $row, so it is the same for every row.` | A predicate that compares two fixed words and never reads the row. A note offers `ls \| where $row.kind eq folder`. |
| `$row.kind is text (folder), not true or false.` | A predicate that is a value rather than a question. A note offers a comparison, `$row.kind eq folder`. |
| `$row is the row a predicate is testing. It exists only inside where, find, in and save-view: ls \| where $row.kind eq folder.` | `$row` read outside a predicate, where no row is being tested. |

[A predicate is a question about the row](tables.md#a-predicate-is-a-question-about-the-row)
has examples of each.

---

## write

Writes text to a file, creating it or replacing its contents.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | The file to write. |
| `text` | piped. What to write. |

**Returns** the file.

```
$ write note.txt hello
note.txt
$ echo hi | write note.txt
note.txt
$ read documents/notes.txt | write copy.txt
copy.txt
$ write(note.txt, hello)
note.txt
```

A file `write` creates takes its kind from the extension, such as `text` for `.txt` and
`script` for `.clr`. A file that already exists keeps the kind it has.

**Undo** restores the previous contents, or deletes the file if it did not exist.

**Errors**

| Message | Cause |
| --- | --- |
| `That is a directory, not a file : <path>` | The path names a directory. |
| `Directory does not exist : <path>` | The parent directory is missing; create it first. |
| `'write' needs an argument for 'text'.` | No text written and nothing piped in. |

---

## Commands only in the desktop shell

None at present. `debug`, which wrote the entity and component tree to a file, was
dropped when the command layer moved to F#: it reaches into the entity component
system, which the core knows nothing about. It can come back as a host-supplied
command if it is wanted.
