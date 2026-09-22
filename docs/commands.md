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
| [`cat`](#cat) | Read a file as text | no |
| [`cd`](#cd) | Enter a directory, a saved view, or a predicate | yes, where you are |
| [`columns`](#columns) | A table's columns and their types | no |
| [`count`](#count) | How many rows a table has | no |
| [`cp`](#cp) | Copy a file into a directory | yes |
| [`distinct`](#distinct) | Unique rows, or a column's unique values | no |
| [`download`](#download) | Download a file, with progress | yes |
| [`echo`](#echo) | Return an argument or the piped value | no |
| [`find`](#find) | List every record a predicate is true of | no |
| [`exit`](#exit) | Ask the host to close | no |
| [`first`](#first) | The first row of a table | no |
| [`from-csv`](#from-csv) | Read a CSV file as a table | no |
| [`from-xml`](#from-xml) | Read an XML file as a tag, or a table | no |
| [`group`](#group) | Gather the rows that share a value | no |
| [`help`](#help) | The commands, as a table | no |
| [`history`](#history) | The lines that changed something | no |
| [`is-fault`](#is-fault) | Whether a value is a fault that `try` or `else` caught | no |
| [`last`](#last) | The last row of a table | no |
| [`ls`](#ls) | List a directory, or the view you are in | no |
| [`mkdir`](#mkdir) | Create a directory | yes |
| [`progress`](#progress) | Run a progress bar, to exercise long commands | no |
| [`pwd`](#pwd) | Where you are: a directory, or a view | no |
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
| [`up`](#up) | Leave the view, or move up one directory | yes, where you are |
| [`vars`](#vars) | List the variables in scope | no |
| [`where`](#where) | Keep the rows a predicate is true for | no |
| [`write`](#write) | Write text to a file | yes |

The thirteen table functions — `columns`, `count`, `distinct`, `first`, `group`,
`last`, `rows`, `select`, `skip`, `sort`, `table`, `take` and `where` — all take their
table from the pipe, coerce a table-shaped tag, and change nothing.
[Tables and predicates](tables.md) is the guide to them; this is the reference.
The four document commands — `from-csv`, `from-xml`, `to-csv` and `to-xml` — are covered
in [Reading and writing files](tables.md#reading-and-writing-files).

The column says whether the command produces events. A line made only of commands that
change nothing leaves no trace at all, which is why `undo` after `ls` reverses the line
before the `ls` rather than the `ls` itself. See
[How it works](concepts.md#events-and-what-a-line-is).

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
error. `folder`, `created` and `modified` are the terminal's and are refused:

```
$ attr readme.txt modified=yesterday
'modified' is set by the terminal and cannot be written.
```

Assignments are data, not named parameters. A command that does not collect them says
so rather than ignoring them:

```
$ progress steps=20
'progress' does not take 'steps=' assignments.
```

Undo restores every attribute the record had.

---

## cat

Reads a file and returns its contents as text.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | piped. The file to read. |

**Returns** the file's text. The terminal renders it as a block rather than a chip, so
line breaks survive.

```
$ cat readme.txt
This filesystem lives in the browser tab.
$ echo notes.txt | cat
Try: ls, cd documents, mkdir scratch, echo "hello"
$ cat(path: readme.txt)
This filesystem lives in the browser tab.
```

**Errors**

| Message | Cause |
| --- | --- |
| `File does not exist : <path>` | Nothing at that path. |
| `That is a directory, not a file : <path>` | The path names a directory. |

---

## cd

Goes somewhere. Somewhere is a directory, a saved view, or a question written out.

**Parameters**

| Name | Notes |
| --- | --- |
| `TargetPath` | piped. A directory, a view file, or a predicate. |

**Returns** the directory it entered, as a file, or the predicate, as a query.

```
$ cd documents
documents
$ cd ../projects
projects
$ echo documents | cd
documents
$ cd $row.mood eq great
$row.mood eq great
```

What decides between the two is whether an operator was written. `cd journal` is a
name: a directory, or a file of kind `view`, whose predicate is entered instead.
`cd $row.mood eq great` has `eq` in it, so it is a question, and entering it sets the
view without moving out of the directory you are in — new files still land there.
See [The filesystem](filesystem.md#views).

`cd ..` normalises, so the working directory shows the parent's real name rather than a
path ending in `..`. Entering a directory puts down whatever view was held.

**Undo** returns you to where you were, view and all.

**Errors**

| Message | Cause |
| --- | --- |
| `'cd' needs an argument for 'TargetPath'.` | No path given and nothing piped in. |
| `Directory does not exist : <path>` | No such directory, and no view file of that name. |
| `'<path>' does not hold a predicate : <text>` | A view file whose content is not a predicate. |

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
4
$ ls | where $row.kind eq folder | count
3
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

**Returns** the new file's path.

```
$ cp readme.txt documents
readme.txt
```

**Undo** deletes the copy.

**Errors**

| Message | Cause |
| --- | --- |
| `File does not exist : <path>` | No such source file. |
| `Target directory does not exist : <path>` | No such destination directory. |
| `Target file already exists : <path>` | Refuses to overwrite; use `write` or `rm` first. |

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

**Returns** the downloaded file's path.

```
$ download
100%
=====...=====>
Downloaded to /README.md
README.md
```

While it runs it writes three lines: a percentage, a bar that grows to a hundred
characters, and a transfer rate in MB/s. The bar above is shortened to fit this page.

**Undo** deletes the downloaded file.

**Errors**

| Message | Cause |
| --- | --- |
| `Not a valid URL : <text>` | The URL is not absolute. |
| `Target directory does not exist : <path>` | No such destination directory. |
| `The server did not report a content length.` | The response had no `Content-Length`; progress cannot be computed. |
| `The transfer ended before all bytes arrived.` | The connection closed early. |
| `Download failed : <reason>` | Written as output when the transfer throws. |

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
up  documents  projects  readme.txt
```

The value keeps its type: `echo 42` returns a number, and `ls | echo` returns the list
of paths rather than a printed copy of it. After `else`, what is piped in is the fault,
so `echo` with nothing written shows what went wrong:

```
$ cat missing.txt else echo
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

```
$ find $row.mood eq great
name      kind  folder    size  modified                           mood   tag
postcard  note  /         0     2026-09-22T09:30:00.0000000+00:00  great  home
saturday  note  /journal  0     2026-09-22T09:30:00.0000000+00:00  great  home

$ find $row.kind eq note and $row.tag eq work | count
2
```

`find` asks a question once; [`cd`](#cd) on the same predicate moves into it, so every
`ls` afterwards asks it again. Asking changes nothing, so a `find` leaves no
transaction and `undo` reaches past it.

**Errors**

| Message | Cause |
| --- | --- |
| `'find' needs a predicate, such as $row.kind eq note.` | A plain word was written instead of a question. |

---

## first

The first row of a table.

**Parameters**

| Name | Notes |
| --- | --- |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** the row as an object of type `row`, or nothing when the table is empty.
Nothing is an answer, not a failure, and `??` is how to give it a default.

```
$ ls | sort name desc | first
<row name=readme.txt kind=text folder=/ size=41 modified=2026-09-22T09:30:00.0000000+00:00/>
$ first (ls | where $row.kind eq view) ?? "no views yet"
no views yet
```

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

```
$ cat reorder.csv
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

```
$ from-xml items.xml | count
3
$ from-xml items.xml | sort qty desc | first
<row sku=A1 name=bolts qty=120 min=50/>
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
folder  3 rows
text    1 row
```

**Errors**

| Message | Cause |
| --- | --- |
| `'group' has no column named '<name>'.` | No such column. `columns` lists what there is. |

---

## help

Every command, with its parameters and what it does.

**Parameters** none.

**Returns** a table of `name`, `parameters` and `description`. A required parameter is
written `<name>`, an optional one `[name]`, and one that collects the rest `name...`.

```
$ help | take 4
name     parameters            description
attr     <path> [assignments]  Show a file's attributes, or set them with name=value
cat      <path>                Read a file and return its text
cd       <TargetPath>          Enter a directory
columns  [table]               The table's columns and their types
$ help | where $row.name eq set | select name description
name  description
set   Bind a value, or whatever was piped in, to a variable
```

It changes nothing and leaves nothing to undo.

---

## history

The lines that changed something, oldest first.

```
history
```

**Returns** a table of `seq`, `at`, `source` and `undone`.

```
$ history
seq  at        source                undone
1    09:30:00  seed                  false
2    09:30:00  mkdir alpha           false
3    09:30:00  write note.txt hello  false
```

Each row is a sequence number, the time, the line exactly as it was typed, and whether
its effect has been reversed:

```
$ undo
Undone: write note.txt hello
$ history
seq  at        source                undone
1    09:30:00  seed                  false
2    09:30:00  mkdir alpha           false
3    09:30:00  write note.txt hello  true
4    09:30:00  write note.txt hello  false
```

The fourth row is the undo itself, which is a line in its own right. Lines that changed
nothing, such as `ls`, never appear, because they were never recorded.

Being a table, it can be questioned:

```
$ history | where $row.undone eq true | count
1
```

`seed` is the filesystem the session started with. It is shown, and it cannot be undone.

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
$ try cat missing.txt | set problem
File does not exist : /missing.txt
$ is-fault $problem
true
$ echo fine | is-fault
false
$ cat missing.txt else is-fault
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
<row name=readme.txt kind=text folder=/ size=41 modified=2026-09-22T09:30:00.0000000+00:00/>
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
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       41    2026-09-22T09:30:00.0000000+00:00
$ ls documents
name       kind  folder      size  modified
notes.txt  text  /documents  50    2026-09-22T09:30:00.0000000+00:00
```

In a view, `ls` lists every record in the terminal the view matches, from whatever
directory, and the `folder` column is what says where each row came from. Writing a
path is how you look at a directory without leaving the view.

```
$ cd $row.mood eq great
$row.mood eq great
$ ls
name      kind  folder    size  modified                           mood   tag
postcard  note  /         0     2026-09-22T09:30:00.0000000+00:00  great  home
saturday  note  /journal  0     2026-09-22T09:30:00.0000000+00:00  great  home
```

`size` is the length of the file's content, worked out when the table is built rather
than stored, so it can never disagree with what `cat` shows. `created` is not a column;
`attr` shows it.

There is no parent entry. A listing used to begin with an `up` row, and a table of
records has nowhere to put one: every row is a record, and `up` is not. The browser
offers `up` in the location line instead, and it runs the [`up`](#up) command.

Everything in [Tables and predicates](tables.md) applies to a listing.

**Errors**

| Message | Cause |
| --- | --- |
| `Directory does not exist : <path>` | No such directory. |

---

## mkdir

Creates a directory.

**Parameters**

| Name | Notes |
| --- | --- |
| `FolderName` | The directory to create, relative to the current one. |

**Returns** the new directory's path.

```
$ mkdir scratch
scratch
```

**Undo** deletes it.

**Errors**

| Message | Cause |
| --- | --- |
| `Target directory already exists : <path>` | Something is already there. |

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

Press **Stop** while it runs and it reports where it stopped:

```
$ progress
9%
====>
Cancelled at 9%
Stopped.
```

**Undo** writes `Progress test undone`.

**Errors**

| Message | Cause |
| --- | --- |
| `'steps' must be at least 1.` | `steps` was zero or negative. |
| `'steps' must be a whole number, not '<value>'.` | The argument was not a number, for example a bare `-steps` flag. |

---

## pwd

Returns where you are: the current directory, or the view you are in.

**Parameters** none.

**Returns** the directory's path as text, or the view as a query.

```
$ pwd
/documents

$ cd $row.mood eq great
$row.mood eq great
$ pwd
$row.mood eq great
```

The line above the input shows the same thing at all times, and says which of the two
it is.

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

---

## reset

Empties the log and starts again from the seeded filesystem.

```
reset
```

```
$ reset
Reset. 4 files restored.
```

This is the only command that cannot be undone. The lines it would have been undone
from are the ones it threw away:

```
$ reset
Reset. 4 files restored.
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
<row name=documents kind=folder/> <row name=examples kind=folder/> <row name=projects kind=folder/> <row name=readme.txt kind=text/>
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
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       41    2026-09-22T09:30:00.0000000+00:00
> ls | where $row.kind eq folder | count
3
```

Four example programs are seeded into `/examples`, one per pillar of the design. Only
`examples/tables.clr` runs to completion today; the others need phases that are not
built yet. See [Worked examples](examples.md).

**Errors**

| Message | Cause |
| --- | --- |
| `File does not exist : <path>` | No such file. |
| `<path> line <n>: <message>` | A line failed. The lines before it have already committed. |
| `Scripts are only allowed to run scripts 8 deep.` | A script that runs a script that runs a script, eight times over — usually one that runs itself. |

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
created = 2026-09-21T09:00:00.0000000+00:00
due = 2026-10-01
folder = /
kind = note
modified = 2026-09-21T09:00:00.0000000+00:00
name = todo
```

A tag with attributes is exactly what a file record is, which is why this is one word
rather than a `write` followed by several `attr` calls.

The tag can be piped in: `echo <note name=todo/> | save`.

The `name` attribute is required, and a name already used in the folder is an error.
The file has no content, so `cat` on it returns empty text; `write` gives it some.

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
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
cheerful    view    /       18    2026-09-22T09:30:00.0000000+00:00
postcard    note    /       0     2026-09-22T09:30:00.0000000+00:00  great
readme.txt  text    /       41    2026-09-22T09:30:00.0000000+00:00

$ cat cheerful
$row.mood eq great

$ cd cheerful
$row.mood eq great
```

A view is an ordinary record of kind `view` whose content is the predicate as it was
written, so it is listed, tapped, renamed with `attr`, undone and deleted like any
other file, and `cat` shows what it asks. The view is created in the current
directory; what it matches is not limited to that directory.

**Undo** deletes the view.

**Errors**

| Message | Cause |
| --- | --- |
| `'save-view' needs a predicate, such as $row.kind eq note.` | A plain word was written instead of a question. |
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
projects    0
readme.txt  41
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
up  documents  projects  readme.txt
$ echo $greeting
hello
```

**Undo** restores the previous binding, or unbinds a new name.

**Errors**

| Message | Cause |
| --- | --- |
| `'set' needs an argument for 'value'.` | No value written and nothing piped in. |
| `'<name>' is not a valid variable name.` | Names take letters, digits and underscore. |
| `Unknown variable : $<name>` | You wrote `set $x 1`; `$x` reads the variable rather than naming it. |

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
readme.txt
```

---

## sort

Orders the rows by a column.

**Parameters**

| Name | Notes |
| --- | --- |
| `column` | The column to order by. |
| `desc` | optional. Write `desc` after the column, or `-desc`, to order downwards. |
| `table` | optional, piped. The table to work on. Taken from the pipe when it is not written. |

**Returns** a table. Numbers order numerically, everything else by the text on screen.
The sort is stable in both directions, so rows that compare equal keep the order they
arrived in and `sort name | sort size desc` leaves the equal sizes in name order.

```
$ ls | sort size desc | select name size
name        size
readme.txt  41
documents   0
examples    0
projects    0
```

**Errors**

| Message | Cause |
| --- | --- |
| `'sort' has no column named '<name>'.` | No such column. |

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
$ cat short.xml
<?xml version="1.0" encoding="UTF-8"?>
<stock>
  <line sku="A1" qty="120"/>
  <line sku="B2" qty="12"/>
  <line sku="C3" qty="0"/>
</stock>
```

Pretty-printed with two spaces a level. A gap is an attribute that is not written, and
an attribute called `text` is written as the element's content. `-declaration` is a
switch: write it after the path, since a flag followed by a plain word takes the word as
its value.

**Undo** restores the previous contents, or deletes the file if it did not exist.

**Errors**

| Message | Cause |
| --- | --- |
| `'to-xml' needs a tag, a table or a list of tags, not <kind>.` | The value, or an item in a list, has no elements in it. |
| `'<name>' is not a name XML allows.` | An element or attribute name XML cannot hold, such as a column with a space in it. |
| `That is a directory, not a file : <path>` | The path names a directory. |
| `Directory does not exist : <path>` | The parent directory is missing. |

---

## undo

Reverses the last line that changed something, and names it.

```
undo
```

```
$ write note.txt second
note.txt
$ undo
Undone: write note.txt second
$ cat note.txt
first
```

It works on lines, not on commands. A line that changed nothing is not in the way:

```
$ mkdir alpha
alpha
$ ls
alpha documents projects readme.txt
$ undo
Undone: mkdir alpha
```

With nothing to undo it says so, and that is not an error:

```
$ undo
Nothing to undo.
```

Undoing does not erase history: it appends the reverse, so `redo` can reverse it in
turn and `history` shows both.

---

## up

Comes back out. In a view, that means putting the view down; otherwise it means the
parent directory.

**Parameters** none.

**Returns** the directory you are now in, as a path.

```
$ cd journal
journal
$ cd $row.mood eq great
$row.mood eq great
$ up
/journal
$ up
/
```

Two `up`s from a view over a subdirectory come out in the order they went in: the
question first, the directory second. At the root with no view, `up` stays at the root.

**Undo** returns you to where you were, view and all.

---

## vars

Every variable in scope.

**Parameters** none.

**Returns** a table of `name` and `value`, ordered by name.

```
$ vars
name      value
entries   4 rows
greeting  hello
```

A cell is one line, so a variable holding a table says how many rows it has. It is
still the table: `echo $entries | count` answers 4.

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
3
$ ls | where $row.qty lt $row.min | select name qty min
name     qty  min
nuts     12   40
washers  0    20
$ ls | where not $row.tag eq work | select name
name
saturday
```

The operators are `eq ne gt ge lt le like has`, combined with `and`, `or` and `not`.
[Tables and predicates](tables.md#predicates) says what each one means.

**Errors**

| Message | Cause |
| --- | --- |
| `Unknown variable : $row` | A predicate written outside a table function, where nothing bound `$row`. |
| `'where' needs an argument for 'predicate'.` | No predicate written. |

---

## write

Writes text to a file, creating it or replacing its contents.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | The file to write. |
| `text` | piped. What to write. |

**Returns** the file's path.

```
$ write note.txt hello
note.txt
$ echo hi | write note.txt
note.txt
$ cat notes.txt | write copy.txt
copy.txt
$ write(note.txt, hello)
note.txt
```

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
