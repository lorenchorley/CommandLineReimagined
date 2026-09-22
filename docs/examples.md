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

`cat examples/tables.clr` shows the program. The other three —
`journal.clr`, `resilient.clr` and `inventory.clr` — need views, error recovery and XML,
which are not built yet.
