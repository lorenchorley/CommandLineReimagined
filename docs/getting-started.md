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

The status in the top right reads `wasm` in green once the .NET runtime has loaded,
which takes a second or two on a first visit. The input is disabled until then.

## What you are looking at

```
CommandLineReimagined                    wasm      <- runtime status
+--------------------------------------------+
| A terminal running in this tab...          |     <- scrollback
|                                            |
+--------------------------------------------+
  identifier - notes.txt                          <- token inspector
  /                                               <- working directory, with `up` below the root
+------------------------------------+ +-----+
| type a command...                  | | Run |    <- input and Run/Stop
+------------------------------------+ +-----+
  [help] [ls] [cat readme.txt] [cd documents]     <- suggestions
```

The scrollback keeps every command with its output. The line above the input shows the
current directory. The row below the input holds tappable suggestions, and while you
type it is joined by a row of completions.

## Your first commands

The tab starts with a small filesystem already in it. Type `ls` and press Enter, or tap
the `ls` suggestion:

```
$ ls
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       41    2026-09-22T09:30:00.0000000+00:00
```

That is a table, not a block of text: on the page it is drawn as one, and tapping a
cell inserts it into the input, which saves typing a path on a phone. Tapping a column
header re-sorts what is on screen. Below the root the location line offers an `up`
button.

A listing is a value you can question:

```
$ ls | where $row.kind eq folder | count
3
```

[Tables and predicates](tables.md) is the guide to that.

Read a file:

```
$ cat readme.txt
This filesystem lives in the browser tab.
```

Move around, and notice that the working directory line changes:

```
$ cd documents
/documents
$ pwd
/documents
$ up
/
```

Make something, then take it back:

```
$ mkdir scratch
scratch
$ ls | select name
name
documents
examples
projects
scratch
readme.txt
$ undo
Undone: mkdir scratch
$ ls | select name
name
documents
examples
projects
readme.txt
```

`undo` steps back one line each time you use it, and names what it reversed. A line
that changed nothing — an `ls`, a `cat`, any question about a table — was never
recorded, so `undo` reaches past it to the last line that did something.

## Write a file with a pipe

The vertical bar sends one command's result into the next one:

```
$ echo hi | write note.txt
note.txt
$ cat note.txt
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
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       41    2026-09-22T09:30:00.0000000+00:00
$ vars
name      value
files     4 rows
greeting  hello
```

`$files` holds the table itself, not a printed copy of it, which is why `vars` says how
many rows it has instead of drawing it again. `echo $files | count` answers 4.

## Run something slow and stop it

```
$ progress
```

The counter and bar update while it runs, and the Run button turns into a red **Stop**
button. Press it, or press Escape:

```
$ progress
9%
====>
Cancelled at 9%
Stopped.
```

## Get help

Type `help` for a list of every command with its parameters and description, generated
from the commands themselves rather than written by hand. Type `clear` to empty the
screen. Neither is a command in the usual sense; see
[The web terminal](web-terminal.md#words-the-page-handles-itself).

## Where the files live

The filesystem is kept in your browser, for this site, and it is seeded on first use
with:

```
/
├── documents/
│   └── notes.txt
├── projects/
└── readme.txt
```

**It survives a reload.** Make a file, close the tab, come back tomorrow, and it is
still there. The status line at the top says `wasm` when the terminal is running; if it
also says `not persisted`, this browser is not keeping anything and the session lasts
only as long as the tab. A private window is the usual reason.

Nothing is uploaded. Everything is stored by your browser, on your device, for this
site alone, in the same place a website keeps its own data. Clearing site data removes
it, and so does `reset`:

```
$ reset
Reset. 4 files restored.
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

## Next

- [Worked examples](examples.md) for complete sessions to copy.
- [Tables and predicates](tables.md) for filtering, sorting and counting a listing.
- [The command language](language.md) for tags, components, variables and the function
  call form.
- [How it works](concepts.md) for what happens between Enter and the answer.
