# Getting started

This page takes you from an empty screen to a working session in about five minutes.
Everything here runs in a browser tab, including on a phone.

## Open a terminal

You have three ways to run it. They are the same program.

**The hosted build.** The repository owner has a private link to a published build at
<https://claude.ai/artifact/CcDmgfyanWjtEC2b6cs8mU>. Only accounts it has been shared
with can open it.

**A local static server.** Publish the WebAssembly client and serve the output:

```bash
dotnet publish WebClient/WebClient.csproj -c Release -o publish
cd publish/wwwroot && python3 -m http.server 8080
```

Then open <http://127.0.0.1:8080>. See [Building and testing](building.md) for the
prerequisites.

**The ASP.NET host.** `dotnet run --project Web` serves the same client plus a
`/api/parse` endpoint and a WebSocket at `/ws` for tooling.

The .NET runtime takes a second or two to load on a first visit. Until it has, and
the log has been replayed, the input is disabled and the note at the top of the
scrollback says `Loading the terminal…` and then `restoring…`. There is no title bar or
status line: that note, the banner, is where the page says how its start went.

## Start with the readme

The first thing in the scrollback, a panel labelled `note`, says where to begin:

```
A command line that runs in this tab. New here? Run read readme.txt, or tap readme below. Your files stay in this browser.
```

If this browser is not keeping your files, a private window for example, the line says
that instead, and on a return visit it says how many lines it restored. Type `read readme.txt`, or tap the `readme` suggestion:

```
$ read readme.txt
This is a command line that runs in this browser tab.

The guide folder explains how it works, one idea per file. Start with the first:

  read guide/1-start.txt

or list them all:

  ls guide
```

The `guide` folder holds seven short files, read with `read` like any other: getting
around, everything is a value, tables, records and views, failure as a value, undo and
history, and files and scripts. Each ends by naming the next, and every example line in
them can be typed as it stands. Reading them in the terminal is the first exercise; this page
covers the same ground with more of what you will see.

## Run a program first

The quickest way to see what the terminal does is to let it show you. Type
`run examples/tables.clr` and press Enter, or tap the `run` suggestion under the input:

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

`run` executed a small program from the `examples` folder a line at a time, showing
each line after `> ` and then its answer. The answers are values rather than text: a
listing is a table you can filter and count, `vars` lists the variables the program
set, and even `help` is a table you can ask a question of. The last answer appears
twice because it is also the result of `run` itself. `read examples/tables.clr` shows
the program, and [Programs](examples.md#programs) has all four example programs with
their output.

The rest of this page takes the same ideas one command at a time.

## What you are looking at

```
+--------------------------------------------+
| note                                       |     <- the banner
| A command line that runs in this tab...    |
|                                            |     <- scrollback
+--------------------------------------------+
  where · Keep the rows a predicate is true for   <- detail line
  [↶] [↷] [↑] [↓] /                               <- buttons and working directory, with `out` below the root
+------------------------------------+ +-----+
| wh                                 | | Run |    <- input and Run/Stop
+------------------------------------+ +-----+
  [where]                                        <- completions, while you type
  [readme] [guide] [help] [ls] [where] [sort]    <- suggestions
```

The scrollback starts at the top of the screen with the banner, and keeps every command
with its output. What the terminal says of its own, the banner among it, is drawn in a
panel with a small label and a plainer font, so it is never mistaken for output: the
banner, the help under a command called wrongly, and the notes under a line that went
wrong, which say what you probably meant. The
line above the input shows the current directory, after the buttons for undo (↶), redo
(↷) and walking back and forward through the lines you have run (↑ and ↓). The rows
below the input hold the completions for the word you are typing, and tappable
suggestions. The small line at the top says what the selected completion is, which
parameter of a command you are writing, or what a word you tapped in the scrollback is.
[The web terminal](web-terminal.md#completions) covers all three.

Selecting text in the scrollback copies it, and a short `copied` note says so. On a
phone, that is a long press and a drag of the handles.

## Your first commands

The tab starts with a small filesystem already in it. Type `ls` and press Enter, or tap
the `ls` suggestion:

```
$ ls
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
guide       folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       193   2026-09-22T09:30:00.0000000+00:00
```

That is a table, not a block of text: on the page it is drawn as one, and tapping a
cell inserts it into the input, which saves typing a path on a phone. Tapping a column
header re-sorts what is on screen. Below the root the location line offers an `out`
button, and it always has ↶ and ↷ for undo and redo.

Under the table is a badge reading `live`: the listing redraws itself when something
changes. The newest listing starts live and older ones pause, reading `paused`; tap
`paused` to make one live again, as many as you like.

While you type, the chips below the input offer what can come next. Type `ls ` with its
space and the first chip is `|`: once a command has what it needs, sending its result on
is offered first. Tap it and the chips turn to the commands that take a table.

A listing is a value you can question:

```
$ ls | where $row.kind eq folder | count
4
```

Ask it something no row answers and the empty table says why. The indented lines are a
note the terminal adds beside the answer, drawn on the page as a panel labelled `why`:

```
$ ls | where $row.kind eq foldr
name  kind  folder  size  modified
  explanation: kind is folder or text
  fix: ls | where $row.kind eq folder
```

The `fix` is the line it thinks you meant, drawn as a chip. Tapping it puts that line in
the input, and does not run it: you run it, or change it first.

[Tables and predicates](tables.md) is the guide to that.

You have read one file already. A path reaches into a folder:

```
$ read documents/notes.txt
Try: ls, in documents, mkdir scratch, echo "hello"
```

Leave the folder out and the line fails, and a note under the error, labelled
`did you mean` on the page, says where the file is, with the line that reads it as a
chip:

```
$ read notes
File does not exist : /notes
  suggestion: Did you mean documents/notes.txt?
  fix: read documents/notes.txt
```

A mistyped command, folder or variable is answered the same way. The note is for you
to read: the failure itself, which a script would see, is only the red line.

Go into a folder with `in` and come back out with `out`, and notice that the working
directory line changes. `back` goes to where you were before the last move, one step
further each time, like a browser's back button. (If you know a shell, these were `cd`,
`up` and `cat` here once; typing an old name offers the new one, and running one fails
with `Unknown command : cd`, with a note that offers `in` in its place.)

```
$ in documents
documents
$ pwd
/documents
$ out
/
$ back
documents
$ back
/
$ back
Nowhere further back: you are in /
```

Make something, then take it back:

```
$ mkdir scratch
scratch
$ ls | select name
name
documents
examples
guide
projects
scratch
readme.txt
$ undo
Undone: mkdir scratch
$ ls | select name
name
documents
examples
guide
projects
readme.txt
```

`undo` steps back one line each time you use it, and names what it reversed. A line
that changed nothing — an `ls`, a `read`, any question about a table — was never
recorded, so `undo` reaches past it to the last line that did something.

## Write a file with a pipe

The vertical bar sends one command's result into the next one:

```
$ echo hi | write note.txt
note.txt
$ read note.txt
hi
```

`echo` returns the text `hi`. `write` takes a path and some text; because you only gave
it a path, it takes the piped value as its text. Nothing was converted to a string and
re-parsed in between. See [The command language](language.md#pipes) for the rules.

## Name a value

```
$ set greeting hello
hello
$ echo $greeting
hello
$ ls | set files
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
guide       folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
note.txt    text    /       2     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       193   2026-09-22T09:30:00.0000000+00:00
$ vars
name      value
files     6 rows
greeting  hello
```

`$files` holds the table itself, not a printed copy of it, which is why `vars` says how
many rows it has instead of drawing it again. A variable is a value you can look at by
typing its name, and pipe on like any other result:

```
$ $files | count
6
$ $greeting
hello
```

To see your variables without running anything, type `$`. The completion row offers
every one you have bound, each with a line saying what it holds, and the detail line
above the location shows the selected one's:

```
$files · table · 6 rows · name, kind, folder…
$greeting · text · "hello"
```

Tap one to put it in the line, or press Tab to step through them.

## Run something slow and stop it

```
$ progress
```

The counter and bar update while it runs, and the Run button turns into a red **Stop**
button. Press it, or press Escape:

```
$ progress
9%
==>
Cancelled at 9%
Stopped.
```

## Get help

Type `help` for a table of every command with its parameters and description,
generated from the commands themselves rather than written by hand. It is a table like
any other, so you can ask it about one command:

```
$ help | where $row.name eq write
name   parameters     description
write  <path> <text>  Write text to a file, replacing its contents
```

`help` with a command's name says what it does and what each parameter is for, and
which one a pipe fills:

```
$ help write
Write text to a file, replacing its contents
name  required  piped  takes    description
path  true      false  a path   The file to write
text  true      true   a value  What to write
```

While you type an argument, the detail line shows the same thing in one line, with the
parameter you are writing in bold: `write <path> <text> · The file to write`.

Call a command wrongly and its help comes to you: the line fails, and under the error
the page draws the same description and table in a panel labelled `help`, shown here
indented:

```
$ write note.txt
'write' needs an argument for 'text'.
  help
  Write text to a file, replacing its contents
  name  required  piped  takes    description
  path  true      false  a path   The file to write
  text  true      true   a value  What to write
```

Type `clear` to empty the screen. `clear` is not a command: the page handles it itself
and it changes nothing but the scrollback. See
[The web terminal](web-terminal.md#words-the-page-handles-itself).

## Where the files live

The filesystem is kept in your browser, for this site, and it is seeded on first use
with:

```
/
├── documents/
│   └── notes.txt
├── examples/
│   ├── inventory.clr
│   ├── journal.clr
│   ├── resilient.clr
│   └── tables.clr
├── guide/
│   ├── 1-start.txt
│   ├── 2-values.txt
│   ├── 3-tables.txt
│   ├── 4-places.txt
│   ├── 5-failure.txt
│   ├── 6-undo.txt
│   └── 7-files.txt
├── projects/
└── readme.txt
```

**It survives a reload.** Make a file, close the tab, come back tomorrow, and it is
still there. If the banner at the top of the scrollback says `not persisted`, this
browser is not keeping anything and the session lasts only as long as the tab. A
private window is the usual reason. The guide and the readme are kept up to date for
you until you change them: one you have edited stays as you left it.

Nothing is uploaded. Everything is stored by your browser, on your device, for this
site alone, in the same place a website keeps its own data. Clearing site data removes
it, and so does `reset`:

```
$ reset
Reset. 17 files restored.
```

`reset` empties the log and starts again from the seeded files above. It is the only
command that cannot be undone.

A file is not a name and some text. It is a record with attributes, and you can add
your own:

```
$ attr readme.txt tag=work
readme.txt
$ attr readme.txt
name      value
created   2026-09-22T09:30:00.0000000+00:00
folder    /
kind      text
modified  2026-09-22T09:30:00.0000000+00:00
name      readme.txt
tag       work
```

Once a file carries an attribute, it becomes a column in the listing, and a listing is
something you can ask questions of:

```
$ ls | where $row.tag eq work | select name
name
readme.txt
```

And a question is somewhere you can go. `in` takes a predicate as readily as a
directory name, and once you are in one, `ls` answers it — across directories, because
the question was not about directories:

```
$ in $row.tag eq work
$row.tag eq work

$ ls
name        kind  folder  size  modified                           tag
readme.txt  text  /       193   2026-09-22T09:30:00.0000000+00:00  work

$ out
/
```

[The filesystem](filesystem.md) is the guide to attributes, views and what a file
really is here.

## Next

- [Worked examples](examples.md) for complete sessions to copy, and the four
  [example programs](examples.md#programs) with their output.
- [Tables and predicates](tables.md) for filtering, sorting and counting a listing,
  and for reading inside a nested tag or document with `pick`.
- [The filesystem](filesystem.md) for attribute records, views and `find`.
- [The command language](language.md) for tags, components, variables and the function
  call form.
- [How it works](concepts.md) for what happens between Enter and the answer.
