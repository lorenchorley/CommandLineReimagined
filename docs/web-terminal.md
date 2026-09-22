# The web terminal

A guide to the screen itself: every control, what it does, and the behaviour that is
the page's rather than the language's.

## The layout

| Area | What it is |
| --- | --- |
| Title bar | The project name, and a status that reads `wasm` in green once the runtime has loaded. |
| Scrollback | Every command you have run, with its live output, errors and results. |
| Token inspector | One line above the input, naming the role of the word you last tapped. |
| Location line | Where you are: a directory as its path, or a view as the question it is. Anywhere but the root, an `up` button comes before it. |
| Input | A transparent text field over a coloured mirror of what you type. |
| Run button | Runs the line. It becomes a red Stop button while a command is running. |
| Completion row | Appears while you type, offering commands, variables, columns, operators, keywords and paths. |
| Suggestion row | Fixed examples you can tap to fill the input. |

The page is laid out for a phone first: a 390 by 844 screen fits the scrollback, the
input and both button rows without horizontal scrolling.

## Typing

The input is a real text field, so the phone keyboard, autocorrect settings and text
selection behave normally. The colouring you see is a mirror rendered behind it, updated
on every keystroke by parsing the line. While a line is half-typed it usually does not
parse, and the mirror shows plain text rather than complaining.

| Key | Effect |
| --- | --- |
| Enter | Run the line, or stop the one that is running. |
| Up and Down | Walk back and forward through lines you have run. |
| Tab | Apply the only completion, or extend to the common prefix of several. |
| Escape | Stop a running command. |

History keeps what you actually submitted, including `clear`, for as long as the page is
open. A line submitted twice in a row is kept once.

## Completions

As you type, the page asks the session what the last word could become and shows up to
twelve of the answers as chips above the suggestions. Tapping one replaces that word and
adds a space after it. Directory completions end with a `/`; the space is added after
those too, so delete it to keep descending.

| You type | You are offered |
| --- | --- |
| `c` | `cat`, `cd`, `columns`, `count`, `cp`, `clear` |
| `cat re` | `readme.txt` |
| `cd doc` | `documents/` |
| `cat documents/no` | `documents/notes.txt` |
| `ls \| se` | `select`, `set` |
| `echo $` | every bound variable, and `$row` |
| `ls \| where $row.` | the columns a listing here would have |
| `ls \| where $row.kind e` | `eq` |
| `tr` | `try` |
| `cat x else c` | `cat`, `cd`, `columns`, `count`, `cp`, `clear` |
| `cat x el` | `else` |

The first word of a stage completes to command names: the first word of the line, and
the first word after a pipe, after `else`, after `try` and after an opening
parenthesis. `try` is offered there too. `else` is offered where an argument would be,
once two letters of it are typed.
A word starting with `$` completes to variables — `$row` among them, although nothing
bound it: it exists only inside a predicate, and it is the one variable you write
without having bound it. After a full stop, a word starting with `$` completes to
column names. Everything else completes to files and directories relative to the
current one. An empty line offers nothing, because the suggestion row already covers
that case.

The word operators are offered too, but only where an expression is plainly being
written — the test is a `$` earlier in the stage. Without it, `cat no` would offer
`not` beside `notes.txt`.

## Results on screen

A result is rendered by kind:

- **Tables** — a listing, `vars`, `attr`, `history`, `help` and anything a table
  function answered — become a real table. Tapping a column header re-sorts what is on
  screen, without running anything; tapping a cell appends it to the input. A wide
  table scrolls sideways inside its own entry rather than widening the page. Each cell
  is drawn by the rules below, so a folder in a listing is still coloured as a folder
  and still carries its path.
- **Paths, objects, components, numbers and booleans** become chips, and a list becomes
  a row of them. Tapping a chip appends its text to the input — for a file, its full
  path — which is how you avoid typing a file name on a phone. The path is inserted as
  it is, without quotes.
- **Text** becomes a block with a rule down its left side, so `cat` output keeps its
  line breaks.
- **Errors** are shown in red under the command. A command you stopped shows
  `Stopped.` in amber instead.
- **A caught fault** is drawn in amber: see [How a failure looks](#how-a-failure-looks).

Tapping a header sorts what you are looking at. `ls | sort size desc` is the one that
changes what the terminal answered, and it is a different question.

Tapping a cell that names a place writes the line that goes there rather than the path
on its own: a directory, such as one in the `name` column, becomes `cd /documents`, and
a cell in a `folder` column, which says which directory a row came from, becomes `cd`
and that folder. That is most useful in a view's listing, whose rows come from many
directories. Every other cell appends what it is.

Output written while a command runs appears above the result, and updates in place.

## The location line

Below the scrollback and above the input, one line says where you are. A directory
reads as its full path. A view reads as `view:`, the predicate, `in` and the directory
it was entered from, because that is still where a new file would land.

At the root with no view there is nowhere to go up to, and no button. Anywhere else, the
`up` button before the location runs the [`up`](commands.md#up) command, which comes out of
one thing at a time: the view first, then the directory. It is the page's answer to a
listing having nowhere to put a parent row — every row of a table is a record, and
`up` is not one.

## Live listings

The newest listing keeps itself up to date. A listing is a question about the
filesystem, and the filesystem changes underneath it: `mkdir x` in the next entry makes
the answer above it wrong.

A line is kept live when its first word is `ls` or `find` and it answered a table. Every
change is an entry in the log, so the page knows the moment one lands. It asks the same
question again, silently, and replaces the table in place with a brief flash so you can
see that it moved. The re-run leaves no entry in the scrollback, no transaction and
nothing in `history`. A line naming any command that could change something, such as
`ls | set files`, is refused rather than re-run, and the table it drew stays as it was.

Only the newest listing refreshes. The ones above it froze when you moved on, which is
what a scrollback is for, and say `frozen`. Underneath the live one is a badge reading
`live`; tapping it says `paused` and stops the refreshing for that listing, and tapping
it again resumes and redraws at once. `clear` ends it along with the scrollback.

## Running and stopping

Submitting a line creates its scrollback entry immediately, and the entry fills in as
the command writes, with a blinking cursor after the newest line. The Run button becomes
**Stop** for as long as the command is running.

Only one command runs at a time. While one is in flight, the Stop button, Enter and
Escape all stop it; none of them starts another line.

Stopping is cooperative: the command is asked to cancel, writes whatever it wants to
say about being interrupted, and the terminal adds `Stopped.` So a stopped `progress`
shows both its own `Cancelled at 9%` and the terminal's `Stopped.`

## How a failure looks

A failed line shows its message in red, with a small tag before it naming what sort of
failure it was: `notfound`, `conflict`, `binding` and so on. The message is the sentence
that has always been there; the tag is what lets you tell a missing file from a name
already taken at a glance. The [error reference](errors.md) lists every kind.

Nothing the line did survives. A line is one transaction, so `mkdir a | cd nowhere`
leaves no folder behind.

A fault that `try` caught, or that `else` handed on, is not a failure: the line went on
and answered with it. The page draws it in amber, with a left rule like any other
result and the same kind tag in front — `NotFound`, the word `$problem.kind` reads —
so the two are told apart by colour and by the tag's place: in the red line of a
failure, or in the body of a result. See
[Errors as values](language.md#errors-as-values).

`try` and `else` are coloured as keywords as you type them, and `??` as an operator.

## Words the page handles itself

One word never reaches the evaluator, because it is about the screen rather than about
the filesystem:

| Word | Effect |
| --- | --- |
| `clear` | Empties the scrollback. It does not touch the filesystem, the variables or the log. |

It also appears in completions, so typing `cl` offers `clear` alongside real commands.

`undo` and `help` used to be here too. Both are real commands now — `undo` with `redo`
and `history`, `help` as a table you can question with
`help | where $row.name eq set` — so they go through the evaluator like everything else
and can be piped. That also means the desktop shell and the browser get the same
commands, rather than each having its own half of the feature.

What `help` used to say about pipes, tags and variables is in the banner at the top of
the scrollback, where it is visible before anything has been typed rather than after.

## Tapping a word

Every word in the scrollback is a span tagged with the role the parser gave it. Tapping
one highlights it and the inspector line names its role and repeats its text, for
example `identifier` followed by `notes.txt`. This works on your input as it is echoed
back, not just on results. A line that did not parse is echoed as plain text, with
nothing to tap.

## The filesystem in the tab

The root is `/`, seeded on first use with `documents/notes.txt`, `examples/` holding
the four example programs, `projects/` and `readme.txt`. It is not a disk and not
Emscripten's filesystem: it is a projection folded from the log, so `mkdir`, `cp` and
`write` describe changes and the store applies them, and `cd ..` at the root stays at
the root. Files are attribute records rather than entries in a tree, and a query over
their attributes is somewhere you can be: [The filesystem](filesystem.md) is the
guide.

The log is kept in this browser between visits, so a reload replays it and the files
come back. `reset` empties it and seeds again, and is the one command that cannot be
undone.

Nothing is uploaded. The parser, the commands and the files are all inside the page, so
the terminal works offline once loaded. The exception is `download`, which really does
fetch over the network and is therefore subject to the remote host's CORS policy.

## Loading

The runtime is about 10 MB across about 70 files on a first visit, and is cached by the
browser afterwards. Until it is ready the input stays disabled and the status reads
`starting…`. If it has not started after about forty seconds, the status turns red and
reads `failed to load`.

Once the runtime is up the page replays the session's log before enabling the input,
showing `restoring…` while it does. Nothing may run until that has finished: an empty
filesystem and a lost one look identical, so the page refuses to show one as the other.
If the replay fails, the status turns red and reads `failed to restore`, the scrollback
says `Could not restore the session:` and why, and the input stays disabled.

The banner then says what happened — a first visit, or how many lines came back — and
the status line says `wasm`, with `not persisted` beside it when the browser is not
keeping anything; hover it to see the reason the browser gave. That is said before you
have typed, rather than after a morning's work turns out not to have been saved. If some
stored lines could not be read, a red line in the scrollback says how many were skipped.

## Where the log is kept

The log lives in this browser's IndexedDB, under the database `clr-terminal`, for this
origin only. Two stores: `transactions`, keyed by sequence number, and `blobs`, keyed
by content hash. Nothing is uploaded; there is no server to upload it to.

A reload replays the log, so the filesystem, the variables and the folder you were in
all come back, and `undo` reaches back across the reload because the compensation
chain is in the log rather than in memory.

Storage can be unavailable — a private window — or go away mid-session, if site data is
cleared while the page is open. Neither breaks the terminal: the log falls back to
memory and the session keeps working for as long as the tab is open. The status line is
set once, when the page loads, so it says `not persisted` in the first case; in the
second it does not change until the next load.

`reset` empties the log and seeds it again. It is the only command that cannot be
undone, which is why it is not one of the suggestion keys.
