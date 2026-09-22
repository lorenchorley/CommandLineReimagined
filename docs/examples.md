# Worked examples

Complete sessions you can type line by line. Output is what the terminal shows; result
chips are written as plain words.

Each example starts from a fresh tab, except that examples 1, 2 and 8 run in sequence:
the second uses the directory the first made, and the last clears up after both.

## 1. Edit a file and take it back

```
$ ls
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       41    2026-09-22T09:30:00.0000000+00:00

$ mkdir scratch
scratch

$ write scratch/plan.txt "first draft"
plan.txt

$ cat scratch/plan.txt
first draft

$ write scratch/plan.txt "second draft"
plan.txt

$ cat scratch/plan.txt
second draft

$ undo
Undone: write scratch/plan.txt "second draft"

$ cat scratch/plan.txt
first draft
```

One `undo` was enough. The two `cat` lines changed nothing, so they were never
recorded, and `undo` reached past them to the last line that did something — naming it,
so you can see what you are about to take back.

## 2. Copy a file through a pipe

```
$ cat documents/notes.txt
Try: ls, cd documents, mkdir scratch, echo "hello"

$ cat documents/notes.txt | write scratch/notes-copy.txt
notes-copy.txt

$ ls scratch
name            kind  folder    size  modified
notes-copy.txt  text  /scratch  50    2026-09-22T09:30:00.0000000+00:00
plan.txt        text  /scratch  11    2026-09-22T09:30:00.0000000+00:00
```

`cat` returned text, and `write` used it for the parameter you did not write out. The
text never became a command-line string in between, so quoting could not go wrong.

## 3. Name results and reuse them

```
$ ls | set files
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
scratch     folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       41    2026-09-22T09:30:00.0000000+00:00

$ set target documents
documents

$ ls $target
name       kind  folder      size  modified
notes.txt  text  /documents  50    2026-09-22T09:30:00.0000000+00:00

$ vars
name    value
files   5 rows
target  documents

$ undo
Undone: set target documents

$ vars
name   value
files  5 rows
```

`$files` holds the table itself, not a printed copy of it, which is why `vars` says how
many rows it has rather than drawing it again inside another table. Undo unwound the
`set target` binding and left the earlier one alone.

## 4. Structured values with tags

```
$ <size|measurement unit=metres value=3/>
<measurement unit=metres value=3/>

$ echo $size
<measurement unit=metres value=3/>

$ <$size>
<measurement unit=metres value=3/>

$ {renderer colour=red/}
{renderer colour=red/}

$ <outer><inner depth=2/></outer>
<outer><inner depth=2/></outer>
```

Angle brackets build an object, braces build a component, and each reads back the way
you wrote it. The `size|` prefix bound the value to a variable without becoming part of
it.

## 5. A long command, watched and stopped

```
$ progress 20 100
100%
=========================>
Progress test finished
100
```

The first two lines are one line each, updated in place while it runs: the percentage
climbs and the bar grows. What you see afterwards is their final state.

Run it again and press Stop after a moment:

```
$ progress
9%
====>
Cancelled at 9%
Stopped.
```

Then try to start two at once. Submit `progress`, and while it runs submit `ls`:

```
$ ls
A command is already running. Stop it first.
```

## 6. Everything the parser can tell you

```
$ echo "hello world"
hello world
```

Tap the words `hello world` in the scrollback and the inspector names them a string.
Tap `echo` and it names it a command. The quotes are separate tokens, also strings. Now make a mistake on purpose:

```
$ <thing
Syntax error at column 6: expected identifier, />, >.

$ <a></b>
Closing tag 'b' does not match opening tag 'a'

$ echo --double
Syntax error at column 5: expected argument, end of input, ", "", """, $, <, <$, {, |.
```

The column is a zero-based offset into the line, and the list is what the grammar could
have accepted at that point.

## 7. Same call, three ways

```
$ write note.txt hello
note.txt

$ write(note.txt, hello)
note.txt

$ echo hello | write note.txt
note.txt
```

All three bind the same two parameters. The third one leaves `text` to the pipe.

## 8. Clean up

```
$ rm scratch/plan.txt
Removed plan.txt

$ rm scratch/notes-copy.txt
Removed notes-copy.txt

$ rm scratch
Removed scratch

$ undo
Undone: rm scratch

$ ls
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
scratch     folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       41    2026-09-22T09:30:00.0000000+00:00
```

`rm` refuses a directory that still has anything in it, which is why the files went
first. Undo recreated the directory.

## 9. Question a listing

Every listing is a table, and a table can be filtered, counted, sorted and grouped
without leaving the line. This example makes a few records with `save`, then asks
things about them.

```
$ mkdir journal
journal

$ cd journal
/journal

$ save <note name=monday mood=good tag=work/>
monday

$ save <note name=tuesday mood=tired tag=work/>
tuesday

$ save <note name=saturday mood=great tag=home/>
saturday

$ ls | select name mood tag
name      mood   tag
monday    good   work
saturday  great  home
tuesday   tired  work

$ ls | where $row.tag eq work | select name mood
name     mood
monday   good
tuesday  tired

$ ls | where $row.tag eq work | count
2

$ ls | where $row.mood like gre | select name
name
saturday

$ ls | group tag
key   rows
work  2 rows
home  1 row

$ ls | sort mood desc | first
<row name=tuesday kind=note folder=/journal size=0 modified=2026-09-22T09:30:00.0000000+00:00 mood=tired tag=work/>
```

`mood` and `tag` are columns because the records carry them: `save` turns a tag's
attributes into a file's attributes, and a listing shows every attribute anything in
the folder has. Nothing was declared anywhere.

Not one of those lines changed anything, so `undo` after them reverses the last `save`
rather than the last question.

[Tables and predicates](tables.md) is the full guide.

## 10. Run an example program

Four programs are seeded into `/examples`, one per pillar of the design. `run` executes
one a line at a time, echoing each line before its result.

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
> ls | sort name desc | first
<row name=readme.txt kind=text folder=/ size=41 modified=2026-09-22T09:30:00.0000000+00:00/>
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

The last block appears twice: once as the line's own result while the script ran, and
once as the value the script answered with, which is its last line's.

Each line committed its own transaction, so `undo` steps back through the script a line
at a time:

```
$ undo
Undone: set answer 42
```

`cat examples/tables.clr` shows the program. `journal.clr`, `resilient.clr` and
`inventory.clr` run too, and are the subjects of examples 11, 12 and 13.

## 11. A question is somewhere you can be

A directory is a question the terminal keeps asking: which records say they are in it.
Any other question works the same way. Here are three notes in two directories, and a
question that does not care which.

```
$ mkdir journal
journal

$ cd journal
journal

$ save <note name=monday mood=good tag=work/>
monday

$ save <note name=saturday mood=great tag=home/>
saturday

$ up
/

$ save <note name=postcard mood=great tag=home/>
postcard

$ find $row.mood eq great
name      kind  folder    size  modified                           mood   tag
postcard  note  /         0     2026-09-22T09:30:00.0000000+00:00  great  home
saturday  note  /journal  0     2026-09-22T09:30:00.0000000+00:00  great  home
```

`find` asked once. `cd` on the same predicate moves in, so every `ls` afterwards asks
it again:

```
$ cd $row.mood eq great
$row.mood eq great

$ pwd
$row.mood eq great

$ ls
name      kind  folder    size  modified                           mood   tag
postcard  note  /         0     2026-09-22T09:30:00.0000000+00:00  great  home
saturday  note  /journal  0     2026-09-22T09:30:00.0000000+00:00  great  home
```

The two rows are in different directories, which is the point: the question was about
moods. A view is a way of looking rather than a place to put things, so a new
directory made while it is set still lands in the directory underneath:

```
$ mkdir keepsakes
keepsakes

$ up
/

$ ls
name        kind    folder  size  modified                           mood   tag
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
journal     folder  /       0     2026-09-22T09:30:00.0000000+00:00
keepsakes   folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
postcard    note    /       0     2026-09-22T09:30:00.0000000+00:00  great  home
readme.txt  text    /       41    2026-09-22T09:30:00.0000000+00:00
```

`up` put the view down and left you where you already were. A question worth asking
twice is worth keeping, and keeping one means making it a file:

```
$ save-view cheerful $row.mood eq great
cheerful

$ cd cheerful
$row.mood eq great

$ ls
name      kind  folder    size  modified                           mood   tag
postcard  note  /         0     2026-09-22T09:30:00.0000000+00:00  great  home
saturday  note  /journal  0     2026-09-22T09:30:00.0000000+00:00  great  home

$ up
/

$ rm cheerful
Removed cheerful
```

A view is an ordinary record of kind `view`, so it is listed, deleted and undone like
any other file. [The filesystem](filesystem.md) is the full guide, and
`run examples/journal.clr` is the same ideas as a program.

## 12. Recover without leaving the line

A failure stops a line, and a failed line changes nothing. That much has always been
true. What is new is that the line can say what to do instead. This is
`examples/resilient.clr`, a line at a time, from a fresh terminal.

`else` runs the pipeline after it only when the one before it failed:

```
$ cat notes-from-yesterday.txt else echo "starting fresh"
starting fresh
```

`else` binds looser than `|`, so everything after it is one pipeline, and `today.txt`
is written only because yesterday's notes could not be read:

```
$ cat notes-from-yesterday.txt else echo "starting fresh" | write today.txt
today.txt

$ cat today.txt
starting fresh
```

`try` keeps a failure instead of stopping at it. The stage's fault becomes its result,
and the next stage gets it like any other value. The terminal draws it in amber, not
red, because the line succeeded:

```
$ try cat nowhere.txt | set problem
File does not exist : /nowhere.txt

$ echo $problem.kind
NotFound

$ echo $problem.message
File does not exist : /nowhere.txt
```

`??` gives a default to a stage that answered nothing. There are no saved views yet, so
`first` of an empty table is nothing, and the default is what flows on. The spaced
parenthesis is a pipeline whose result `first` is given:

```
$ first (ls | where $row.kind eq view) ?? "no views yet"
no views yet

$ first (ls | where $row.kind eq view) ?? "no views yet" | set latest
no views yet

$ echo $latest
no views yet
```

The last line is the one that shows what recovery does not do. `mkdir today` succeeds,
`cd nowhere` fails, and the pipeline on the left of the `else` fails with it. The right
side answers, and only the right side commits:

```
$ mkdir today | cd nowhere else echo "the whole line was rolled back"
the whole line was rolled back

$ ls
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       41    2026-09-22T09:30:00.0000000+00:00
today.txt   text    /       14    2026-09-22T09:30:00.0000000+00:00

$ find $row.name eq today | count
0
```

`today.txt` is there; `today` never was. The line that tried to make it wrote nothing
the store needed to remember, so `undo` steps straight past it to the line before:

```
$ undo
Undone: first (ls | where $row.kind eq view) ?? "no views yet" | set latest
```

[Errors as values](language.md#errors-as-values) is the guide to all three, and
`run examples/resilient.clr` is the same program in one go.

## 13. Keep a table in a file

A table does not have to stay on the screen. This is `examples/inventory.clr`, a line at
a time, from a fresh terminal: stock levels written down as tags, kept as an XML file,
questioned, and the answer exported as CSV.

```
$ mkdir stock
stock

$ cd stock
stock
```

A tag whose children share a type is a table already. `to-xml` writes it out as a
document, and the file is an ordinary one: it is in `ls`, `cat` reads it, and `undo`
would take it away.

```
$ <items><item sku=A1 name=bolts qty=120 min=50/><item sku=B2 name=nuts qty=12 min=40/><item sku=C3 name=washers qty=0 min=20/></items> | to-xml items.xml
items.xml

$ from-xml items.xml | count
3
```

`from-xml` reads the document back into the same tree, and a table function reads that
as a table. Every `qty` and every `min` reads as a number, so they are number columns,
and `lt` and `sort` compare them as numbers:

```
$ from-xml items.xml | where $row.qty lt $row.min | sort qty | select sku name qty min
sku  name     qty  min
C3   washers  0    20
B2   nuts     12   40
```

The same question, with two columns kept, is the reorder list. `to-csv` writes it with a
header row, and `from-csv` reads it back as a table:

```
$ from-xml items.xml | where $row.qty lt $row.min | sort qty | select sku qty | to-csv reorder.csv
reorder.csv

$ from-csv reorder.csv | count
2

$ cat reorder.csv
sku,qty
C3,0
B2,12

```

The blank line after it is real: every line of a CSV ends in a line break, the last one
too. And the document still answers any other question:

```
$ from-xml items.xml | sort qty desc | first
<row sku=A1 name=bolts qty=120 min=50/>

$ ls
name         kind  folder  size  modified
items.xml    xml   /stock  168   2026-09-22T20:29:03.0152650+00:00
reorder.csv  csv   /stock  19    2026-09-22T20:29:03.0832217+00:00
```

Both files have the kind of what is in them. Writing one is a line like any other, so
`undo` takes the export back and `redo` puts it there again:

```
$ undo
Undone: from-xml items.xml | where $row.qty lt $row.min | sort qty | select sku qty | to-csv reorder.csv

$ cat reorder.csv
File does not exist : /stock/reorder.csv

$ redo
Redone: from-xml items.xml | where $row.qty lt $row.min | sort qty | select sku qty | to-csv reorder.csv
```

[Reading and writing files](tables.md#reading-and-writing-files) is the guide, and
`run examples/inventory.clr` is the same program in one go.
