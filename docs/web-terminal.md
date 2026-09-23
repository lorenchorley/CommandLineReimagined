# The web terminal

A guide to the screen itself: every control, what it does, and the behaviour that is
the page's rather than the language's.

## The layout

| Area | What it is |
| --- | --- |
| Title bar | The project name, and a status that reads `wasm` in green once the runtime has loaded. |
| Scrollback | Every command you have run, with its live output, errors and results. |
| Detail line | One line above the location line. While you type, it shows the parameters of the command you are writing an argument of, what the selected completion is, or a parse error already behind the word; after a tap, what the tapped word is. See [The detail line](#the-detail-line). |
| Location line | Where you are: a directory as its path, or a view as the question it is. Undo (↶) and redo (↷) buttons always come before it, and anywhere but the root an `up` button too. |
| Input | A transparent text field over a coloured mirror of what you type. |
| Run button | Runs the line. It becomes a red Stop button while a command is running. |
| Completion row | Appears below the input while you type, offering what the word under the caret could become: commands, variables, members, columns, values, operators, flags, keywords and paths. |
| Suggestion row | Fixed examples you can tap to fill the input. |

The page is laid out for a phone first: a 390 by 844 screen fits the scrollback, the
input and both button rows without horizontal scrolling.

## On a phone

Nothing the page does on its own opens or closes the keyboard, or moves what is on
screen:

- The keyboard stays as you left it. Tapping Run, `up`, ↶, ↷, a chip, a suggestion key,
  a table cell or a word neither closes it nor opens it, and a line run from the
  keyboard leaves it up. The return key is a plain return, so it does not close the
  keyboard either.
- The terminal fits the part of the screen the keyboard leaves. The input sits just
  above the keyboard, and running a line does not move it.
- The newest output is shown to its last line, even when something below it changes
  size afterwards, such as the location line gaining an `up` button, or the keyboard
  opening. If you have scrolled up to read, the scrollback stays where you are until
  you run the next line.

With a mouse, a tap on a button still puts the focus back in the input, since there
is no keyboard to open, and pressing on a table still selects its text to copy.

## Typing

The input is a real text field, so the phone keyboard, autocorrect settings and text
selection behave normally. The colouring you see is a mirror rendered behind it, updated
on every keystroke by parsing the line.

While a line is half-typed it usually does not parse, and an error at the word you are
typing is expected, so the mirror shows the line as plain text rather than complaining.
An error that is already behind the word you are typing will not go away by typing on.
The mirror underlines it in red, from where the parser stopped to the end of that
token, and the [detail line](#the-detail-line) gives the sentence running the line
would show. Typing `ls | where $row. eq folder` underlines `$row.` and says:

```
Column 16: a column name belongs after the stop, as in $row.kind
```

| Key | Effect |
| --- | --- |
| Enter | Run the line, or stop the one that is running. |
| Up and Down | Walk back and forward through lines you have run. |
| Tab | The first time, apply the only completion, or extend the word to the common prefix of several. Once that does nothing more, step through the chips, putting each one in place in turn. |
| Shift+Tab | Step back through the chips. |
| Escape | Put back what was typed before the first Tab. While a command runs, stop it instead. |

History keeps what you actually submitted, including `clear`, for as long as the page is
open. A line submitted twice in a row is kept once.

## Completions

As you type, the page asks the session what the word under the caret could become, and
shows the answers as chips below the input. The session reads the line rather than
guessing from the last word
([decision 0031](decisions/0031-completion-reads-the-line.md)). It parses the line
with the word left out, so it knows which command and which parameter the word belongs
to, or which part of a predicate it is. Where the answer depends on what flows into the
stage, such as the columns after `ls | sort `, it runs the stages before it the way a
[live listing](#live-listings) re-runs a line: only commands that change nothing, with
nothing committed, nothing in the history and nothing on the screen, and for at most
150 ms. When it cannot, because a stage could change something or the time ran out, it
answers with what a listing of the current folder would have. An answer to an older
keystroke is dropped once a newer one has been asked.

The first chip is selected, and the [detail line](#the-detail-line) says what it is.
Twelve chips show at most; past that, a `+N` chip opens the rest into the same row.
Chips are 44 pixels tall, a finger's width. Tapping one, or Tab, replaces the word under
the caret from its start to its end, so a word in the middle of the line is completed
and the rest of the line stays: with the caret after `re` in `cat re documents`, the
chip is `readme.txt`, and taking it leaves ` documents` where it was. The caret lands after what was put in, followed by a
space unless one is already there. A directory completes to its name and a `/` with no
space, so the next completion goes on into it. A name that is not a bare word, such as
one with a space in it, is completed in quotes, and so is any name once you have typed
the opening quote.

### What each place offers

Every example below is in a fresh tab after `set v 5`, `ls | set files` and
`try cat missing.txt | set problem`. Each chip is shown with its detail after a `·`.
The detail line shows the selected chip's detail, except while the caret is in an
argument or a predicate, where it shows the command's parameters instead: there a
column's type or a value's count comes with the chip but is not on the screen. See
[The detail line](#the-detail-line).

| Where the word is | Offered | For example |
| --- | --- | --- |
| The first word of a stage: the start of the line, and after `else`, `try` or `(` | Command names, each with its description, and `try` and the page's `clear` | `wh`: `where · Keep the rows a predicate is true for` |
| Straight after a pipe | Only the commands that take the piped value, and `try`. After a table, not the ones that take a path or a place from the pipe, since a table is not a name | `ls \| `: `columns`, `count`, `distinct` … `where`, `write`, `try`; not `ls` or `mkdir`, which would ignore it, nor `cat`, `rm` or `cd`. `echo readme.txt \| ` offers `cat`, `rm` and `cd` too |
| A command name written another way, once three letters are typed | A command found by what it does, or one a slip away | `delete`: `rm · rm · matches "delete"`; `lss`: `ls` |
| After `$` | The variables in name order, each saying what it holds | `$`: `$files · table · 4 rows · name, kind, folder…`, `$problem · fault · NotFound · File does not exist : /missing.txt`, `$v · number · 5` |
| After `$` inside a predicate | `$row` first, then the variables | `ls \| where $`: `$row · the row being tested`, then `$files`, `$problem`, `$v` |
| After `$name.` | The members of what the variable holds: a tag's or a file's attributes, a fault's `kind`, `message`, `stage` and `path`; nothing for a number, a text, a boolean or a table | `$problem.`: `$problem.kind · text · "NotFound"`, `$problem.message`, `$problem.stage · number · 1`, `$problem.path`; `$v.`: nothing |
| After `$row.` | The columns of what flows into the stage, with their types | `ls \| where $row.`: `$row.name · file`, `$row.kind · text`, `$row.folder`, `$row.size · number`, `$row.modified`; `ls \| select name \| where $row.`: `$row.name` only |
| An argument that takes a column | The columns of what flows in, with their types; for `select`, the ones not yet written | `ls \| sort `: `name · file`, `kind · text`, `folder · text`, `size · number`, `modified · text` |
| A switch | Its two words | `ls \| sort name `: `desc · Write 'desc' to order downwards`, `asc` |
| After `-` | The command's flags | `ls \| sort name -`: `-desc · Write 'desc' to order downwards` |
| An argument that takes a path | Files and folders, in the current folder or in the folder typed so far | `cat re`: `readme.txt`; `cat documents/no`: `documents/notes.txt`; `cat "doc`: `"documents/"` |
| An argument that takes a place | Folders and saved views | `cd doc`: `documents/` |
| A variable's name, for `set` | The variables, to replace one | `set `: `files`, `problem`, `v`, each with what it holds |
| A command's name, for `help` | The commands, with their descriptions | `help wh`: `where` |
| A count, a number, a new name, a URL or text | Nothing, since there is nothing to pick; the detail line says what is wanted | `ls \| take `: no chips, and `take <count> [table] · How many rows to keep` |
| After `attr <file> ` | That record's attributes as `name=`, each with its value | `attr readme.txt `: `name= · text · "readme.txt"`, `kind= · text · "text"` |
| Where a predicate's operand starts | `$row.`, `not` and `(` | `ls \| where `: `$row. · a column of the row being tested`, `not · true where what follows is false`, `( · a group, or a pipeline to compare with` |
| After an operand | The eight comparisons | `ls \| where $row.kind `: `eq`, `ne`, `gt`, `ge`, `lt`, `le`, `like`, `has` |
| After a comparison | The values the column holds in what flows in, most frequent first, at most twelve; after `like`, each with a `*`. Nothing for `cd`, `find` and `save-view`, which have no stage before them to read values from | `ls \| where $row.kind eq `: `folder · 3 rows`, `text · 1 row`; `ls \| where $row.name like `: `documents*`, `examples*`, `projects*`, `readme.txt*` |
| After a whole comparison | `and` and `or` | `ls \| where $row.kind eq folder `: `and`, `or` |
| After `<` | The tag types in use: the kinds of the records, and the types of the tags variables hold | `save <`: `<script · 4 records`, `<text · 2 records` |
| Inside `<type ` | The attribute names records of that type carry, as `name=`, `name` first, leaving out any already written | after `save <note name=monday mood=good tag=work/>` and `save <note name=tuesday mood=better/>`, `save <note `: `name= · 2 of 2 carry it`, `mood= · 2 of 2 carry it`, `tag= · 1 of 2 carry it` |
| Two letters of `else`, where an argument goes | `else` | `cat x el`: `else` |

An empty line offers nothing, because the suggestion row already covers that case.
`$row` is offered only inside a predicate, because it exists nowhere else, so `echo $`
does not offer it.

A table offers nothing after `$files.`. Reading a column off the table itself, as in
`echo $files.name`, answers nothing, because a column is read off one row, which is
what `$row.` inside a predicate is for: `$files | where $row.` offers the table's
columns.

### What a value is, in one line

A variable's chip, and a tapped variable, describe its value in one line: the kind
first, then what it carries. After these lines, run in this order in a fresh tab, `$`
offers:

| Bound by | Detail |
| --- | --- |
| `set v 5` | `number · 5` |
| `ls \| set files` | `table · 4 rows · name, kind, folder…` |
| `try cat missing.txt \| set problem` | `fault · NotFound · File does not exist : /missing.txt` |
| `set b (is-fault $v)` | `boolean · false` |
| `set t hello` | `text · "hello"` |
| `set long "a sentence that runs on for rather more than forty characters"` | `text · "a sentence that runs on for rather more…"` |
| `write x.txt hi \| set f` | `file · x.txt · text` |
| `mkdir d \| set d` | `folder · d` |
| `set tg <note a=1 b=2/>` | `tag · note · 2 attributes` |
| `cd $row.kind eq folder \| set q` | `query · $row.kind eq folder` |
| `ls \| rows \| set l` | `list · 6 items` |

`vars` does not use these words yet: it still shows a table as `4 rows` and anything
else as its text.

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
  path — which is how you avoid typing a file name on a phone. A path that is not a bare
  word, such as `/my notes.txt` or a name that is a reserved word, is inserted in
  quotes, so it stays one argument.
- **Text** becomes a block with a rule down its left side, so `cat` output keeps its
  line breaks.
- **A variable typed on its own**, such as `$files`, is drawn as whatever it holds,
  as a table if it holds one ([decision 0032](decisions/0032-a-stage-may-be-a-value.md)).
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

Before them both are two buttons that are always there: ↶ runs
[`undo`](commands.md#undo) and ↷ runs [`redo`](commands.md#redo). They do exactly what
typing the command does — the entry the undo takes back leaves the screen, and redo
puts it back where it was — and they leave anything you were halfway through typing in
the input. The `up` button does the same.

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

## Undo on screen

`undo` takes the last line back, so its entry leaves the scrollback, and the undo
leaves no entry of its own. `redo` puts that entry back where it was, as it was, with a
brief flash. On screen it looks as if the line was never run, and then as if it had
been:

| You run | The scrollback shows |
| --- | --- |
| `mkdir a`, `mkdir b`, `ls` | `mkdir a`, `mkdir b`, `ls` |
| `undo` | `mkdir a`, `ls` |
| `undo` | `ls` |
| `redo` | `mkdir a`, `ls` |

A line that changed nothing, such as the `ls` above, is never taken back, so it stays.
It is a live listing, so its table still changes to match.

The undo and the redo are still in the log. `history` lists them as lines of their own,
each naming the line it acted on, because that is the record of what happened; the
scrollback shows what you meant.

The undo or redo keeps an entry of its own, saying `Undone:`, `Redone:`, `Nothing to
undo.` or `Nothing to redo.`, when there is nothing on screen to act on. That happens
when there was nothing to undo, when the line it reverses was cleared with `clear` or
ran before a reload, and when the line did more than undo, such as `run` of a script
with `undo` in it. Otherwise nothing would show that it had done anything. After
`reset`, entries waiting for a redo are dropped, because nothing can redo them.

The rule is [decision 0030](decisions/0030-undo-takes-the-line-back-on-screen.md).

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
`help | where $row.name eq set`, and `help where` for one command's parameters — so
they go through the evaluator like everything else
and can be piped. That also means the desktop shell and the browser get the same
commands, rather than each having its own half of the feature.

What `help` used to say about pipes, tags and variables is in the filesystem: the
banner at the top of the scrollback points to `readme.txt`, and `readme.txt` points to
the `guide` folder, one file per idea, read with `cat`.

## The detail line

One line above the location line, in small type. It has one job at a time, in this
order:

1. **While the caret is in an argument or a predicate**, the command's parameters,
   with the one the word would fill in bold and what it is for after it. Typing `ls | sort ` shows
   `sort <column> [desc] [table] · The column to order by`, with `<column>` in bold. A
   required parameter is written `<name>` and an optional one `[name]`. Where there is
   nothing to pick, as after `ls | take `, this is how you find out what is wanted:
   `take <count> [table] · How many rows to keep`. After a `-`, before a flag is
   chosen, nothing is bold and the command's own description follows.
2. **Otherwise, the selected chip's detail**: `where · Keep the rows a predicate is
   true for` while typing `wh`, or `$files · table · 4 rows · name, kind, folder…`
   while typing `$`.
3. **Otherwise, a parse error already behind the word being typed**, in red: see
   [Typing](#typing).

Tapping a word in the scrollback takes it over until the next keystroke.

## Tapping a word

Every word in the scrollback is a span tagged with the role the parser gave it. Tapping
one highlights it, and the detail line says what it is where it stands, read from the
line it is in the way completion reads it. This works on your input as it is echoed
back, not just on results. In the same tab as the examples above:

| You tap | The detail line says |
| --- | --- |
| A variable: `$files` in `echo $files` | `$files · table · 4 rows · name, kind, folder…` |
| A command: `where` in `ls \| where $row.kind eq folder` | `where <predicate> [table] · Keep the rows a predicate is true for` |
| A column: `kind` in `ls \| where $row.kind eq folder` | `$row.kind · column · text` |
| A member: `kind` in `echo $problem.kind` | `$problem.kind · text · "NotFound"` |
| `$row` in a predicate | `$row · the row being tested` |
| An operator: `eq` in `$row.kind eq folder` | `eq · true when $row.kind is equal to folder` |
| `like` in `$row.name like *.txt` | `like · true when $row.name matches the pattern (* for anything) *.txt` |
| `and` in `$row.kind eq folder and $row.size gt 0` | `and · true when both sides are true` |
| An argument: `folder` in `ls \| where $row.kind eq folder` | the command's parameters with the one it fills in bold: `where <predicate> [table] · An expression over $row, such as $row.kind eq folder` |

A quote is answered for the string it belongs to. Where there is nothing more to say
about a word, such as a `|`, the line names its role and repeats its text, as
`punctuation — |`. A line that did not
parse is echoed as plain text, with nothing to tap.

## The filesystem in the tab

The root is `/`, seeded on first use with `documents/notes.txt`, `examples/` holding
the four example programs, `guide/` holding one file per idea, `projects/` and
`readme.txt`, which points to the guide. A log begun before the guide existed is given
it once, on its next load ([decision 0036](decisions/0036-the-guide-is-in-the-filesystem.md)). It is not a disk and not
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

The banner then says what happened — a first visit, or how many lines came back —
points to `cat readme.txt` (the `readme` key runs it), and says whether this browser is
keeping your files. The status line says `wasm`, with `not persisted` beside it when the browser is not
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
memory and the session keeps working for as long as the tab is open. The status line
says `not persisted` in the first case as soon as the page loads, and in the second
after the first line that runs once storage has gone.

`reset` empties the log and seeds it again. It is the only command that cannot be
undone, which is why it is not one of the suggestion keys.
