# Vision

**Status.** The owner's aims for the project, as stated on 2026-09-24, and a proposed
route to them, written from the [decision log](decisions/README.md) as it stood at
record 0052. It sets aims and proposes a route; it decides nothing. Every change it proposes to the language is a
decision record to be written first, **Proposed** until the owner accepts it
([0001](decisions/0001-record-decisions.md)). Command syntax in this document is
illustrative: where a command does not exist yet, its form is for the record that
introduces it.

## The vision

A terminal that is easy to understand and learn, and that is not tied to text: what a
command answers is a value, drawn as what it is. It is versatile, and it is scripted
in a small functional language. Its filesystem is a database of typed records rather
than a tree of names. It has native data capabilities, and it can be customised
completely. Every command can be replayed, and it has a graphics engine built in.

The project already stands on most of the ground this needs. A line is parsed into a
tree before anything runs, and pipes carry typed values. Failure is a value, and every
line is one undoable transaction. The filesystem is attribute records whose queries
are places, and tables, XML and CSV are native. The whole terminal runs in a phone's
browser, and nothing leaves the page. The work ahead is less a change of direction than
a widening: the ideas that now serve files are to serve everything the terminal holds.

## The ideas under every pillar

Seven principles, each already true of part of the system, and each to become true of
all of it. When a proposal below is in doubt, these decide.

1. **Everything is a value.** A command answers a value, never a printed string: a
   table, a record, a tree, a fault. Text is one way of showing a value, not what the
   value is. (Design goals; [0006](decisions/0006-functional-core-in-fsharp.md),
   [0009](decisions/0009-table-coercion.md).)
2. **Everything that lasts is an event in one log.** Files and variables are
   projections of an append-only log ([0010](decisions/0010-undo-by-event-sourcing.md)),
   so undo, persistence and history come free. Settings, user commands and scenes
   should live there too, and then they come free for those as well.
3. **Every action is a line.** A tap on a chip writes text into the input, and a fix
   fills it in ([0044](decisions/0044-a-fault-may-carry-fixes.md)); the undo and `out`
   buttons run commands. Anything the screen lets you do by hand should be, or should
   write, a command line. That is what makes the screen teach the language, what makes
   a session replayable, and what makes any gesture scriptable.
4. **One query language for everything.** `where`, `$row`, `pick` and views work on
   files, tables and XML today. The same words should question history, settings,
   commands and entities.
5. **Failure and guidance are apart from output.** A fault is a value a line can
   recover from ([0014](decisions/0014-recovery-operator.md)), and what the terminal
   says of its own is drawn as guidance, never as an answer
   ([0041](decisions/0041-guidance-is-drawn-apart-from-output.md)).
6. **The phone decides.** Words rather than shifted symbols
   ([0007](decisions/0007-notation-conflicts.md),
   [0022](decisions/0022-hyphenated-command-names.md)), 44-pixel targets, and a keyboard
   that never opens or closes by itself. A feature that only works with a physical
   keyboard is not finished.
7. **One headless core, many hosts.** The language, the store and the commands need no
   screen, window or disk ([0003](decisions/0003-execute-in-the-browser.md)), so the
   same core can run in a tab, a window, a test and a script runner.

## Where it stands

| Pillar | Built | The gap |
| --- | --- | --- |
| Easy to understand and learn | The guide in the filesystem ([0036](decisions/0036-the-guide-is-in-the-filesystem.md)); completion that reads the line ([0031](decisions/0031-completion-reads-the-line.md)); help under a wrong call ([0038](decisions/0038-a-wrong-call-shows-its-help.md)); did-you-mean, fix chips and explained empty answers ([0042](decisions/0042-a-missing-name-names-the-nearest.md) to [0045](decisions/0045-a-near-value-offers-a-fix.md)); names that read as moves ([0037](decisions/0037-in-out-back-and-read.md)) | A pipeline cannot be taken apart on screen. The guide cannot check your answers. `help` knows commands, not ideas. |
| Not tied to text | Values end to end; real tables, chips and live listings in the DOM ([0004](decisions/0004-dom-not-canvas.md), [0047](decisions/0047-a-live-listing-stays-where-it-was-run.md)); guidance panels ([0041](decisions/0041-guidance-is-drawn-apart-from-output.md)) | Every value kind is drawn as a table, a chip or text. No charts, images, trees you can fold, forms, or actions on a result. |
| Versatile | One core behind the browser, the desktop build and the tests; scripts ([0020](decisions/0020-scripts-and-run.md)); XML and CSV files | One real host. No way in from the web or the device, and no way to run a script outside a tab. |
| Functional scripting | Pipes as composition; failure as a value with `else`, `try` and `??`; atomic lines ([0015](decisions/0015-atomic-lines.md)); pipelines as operands ([0023](decisions/0023-adjacent-function-parenthesis.md)); variables as stages ([0032](decisions/0032-a-stage-may-be-a-value.md)); a predicate is already a value | No user-defined commands, no per-row expressions, no arithmetic, no conditionals. The design doc names all four as non-goals, and [0020](decisions/0020-scripts-and-run.md) left the question open. |
| A more interesting filesystem | Attribute records in the style of BeOS ([0013](decisions/0013-attribute-filesystem.md)); folders as records ([0016](decisions/0016-folders-as-records.md)); queries as places, saved views and live views; content-addressed blobs | Kinds have no schema, records cannot refer to each other, a file's past cannot be read, and contents are text only. |
| Native data | Tables, mixed columns ([0028](decisions/0028-mixed-columns.md)), thirteen table functions, XML ([0011](decisions/0011-real-xml-files.md), [0025](decisions/0025-xml-text-content.md)), CSV, `pick` with CSS selectors ([0049](decisions/0049-pick-selects-elements-with-css-selectors.md)) | Dates are text (`modified` is a text column). No JSON, no sums or averages, no joins, no computed columns, nothing to turn text into rows. |
| Total customisation | Light and dark follow the system; the guide's files follow the seed unless you edit them ([0040](decisions/0040-seeded-files-follow-the-seed.md)) | Nothing else. The theme, the palette keys, the prompt and the commands are fixed. |
| Command replayability | The event log, undo and redo, `history` as a table, scripts, live listings and completion re-running read-only lines ([0030](decisions/0030-undo-takes-the-line-back-on-screen.md), [0031](decisions/0031-completion-reads-the-line.md)) | `history` cannot be run again or kept as a script. The point-in-time view that [0010](decisions/0010-undo-by-event-sourcing.md) promised was never built. |
| A graphics engine | A desktop-only prototype ECS, renderer and interaction layer (about 4,000 lines across six projects); a proposed direction, [0029](decisions/0029-scene-editor-direction.md), with a design and a first milestone | 0029 is Proposed, not Accepted. Nothing is drawn on a canvas in the browser. |

## The pillars

### Easy to understand and learn

The terminal already teaches itself: its guide is files you read with `read`, and when
you get something wrong it says what you probably meant and offers the fix. What it
cannot do yet is show you what happened inside a line, check that you understood, or
answer a question about an idea rather than a command.

Proposed:

- **Take any pipeline apart on screen.** Tap the `|` between two stages of a line you
  ran, and see what flowed through it at that point, drawn like any answer. For a line
  that only read, this re-runs the line up to that pipe, read-only, the way completion
  (0031) and live listings already do. A line that changed something cannot be re-run,
  so its values between stages would be kept when it runs, within a size budget. It
  needs no new language, only the page and the core. An `explain` command could do the
  same in words for a whole line.
- **Guides that check.** A lesson sets a small goal ("list only the folders") and
  recognises when your answer matches, with a predicate over the answer rather than a
  string comparison. It runs in a sandbox (see replay), so a lesson never touches your
  files.
- **Help on ideas.** `help $row`, `help else`, `help views`: the concepts, each with a
  line to try, as the guide's files are now. `help` knows only commands today.
- **Ask in words.** The desktop shell has a thesaurus search that finds `rm` from
  "delete". Its 20 MB dictionary cannot come to the browser as it is. A small curated
  synonym list can, and the command keywords of 0037 are already a start.

You will know it is working when someone new can finish the guide on a phone without
opening the documentation, and never has to guess what a line did.

### Not tied to a text-based output

Every answer is already a value, and the page already draws tables as tables. The
remaining step is to draw every kind of value as what it is, and to let you act on an
answer rather than only read it.

Proposed:

- **Draw by kind.** A value's kind decides how the page shows it. Images are drawn as
  images, markdown as formatted text, and a tag as a tree you can fold. A table can be
  a table or a chart, a number can carry its unit, and a record can be a card. The wire
  already carries each value's kind, so this is the page's work plus one contract in
  the host interface. Which renderer a kind gets should be customisable (see
  customisation).
- **Charts.** `ls | chart bar name size` answers a chart value drawn on a canvas. It
  is the first and cheapest piece of the graphics engine, and useful at once.
- **Actions on values.** A record carries what can be done with it: open, read, delete,
  go in. Choosing one writes the line it stands for, so the user sees the command
  behind the gesture (principle 3). This is the "richer results" section of the
  [scene direction](plan/scene-editor-direction.md#richer-results-in-ordinary-commands).
- **Edit in place.** Change a cell of a listing, and the terminal writes and runs the
  `attr` line that does it. That line is one transaction, undoable, and in `history`.
- **Panels.** Pin a live listing, a chart or a scene beside the scrollback, so an
  answer can stay in view while you work.

Text stays available for every value. The desktop, the tests, the transcripts and
`to-csv` depend on it. "Not tied to text" means text is one view among several, not
that it goes away.

You will know it is working when no kind of value has to be read as text to be
understood, and anything you can do to a result by hand is a line you could have typed.

### Very versatile

The core has one real host. It could have several, and it could take data from more
places.

Proposed:

- **More hosts for the same core.**
  - A command-line runner: `clr run nightly.clr` on a real machine, for batch jobs and
    CI. It is a new host with a projection onto the real disk, which
    [0010](decisions/0010-undo-by-event-sourcing.md) anticipated.
  - An embeddable web component: a `<clr-terminal>` element, so documentation pages and
    the guide can run their examples in place.
  - The desktop hosting the browser client in WebView2, as
    [0029](decisions/0029-scene-editor-direction.md) proposes.
- **More sources, each explicit and permissioned.**
  - `fetch` answers a URL's JSON, XML or CSV as a value, where `download` only writes a
    file.
  - Files dragged onto the page are imported as records.
  - A folder of the device can be mounted through the File System Access API. That
    reopens the design doc's "reaching the device" non-goal, and so needs a record.
- **More domains.** Files today; user-defined record kinds (see the filesystem); and
  scenes (see the graphics engine). All of them are questioned with the same words.

You will know it is working when the same script runs in a tab, in the command-line
runner and in a test, with the same result.

### A functional-style scripting language

This is the pillar that most changes the language. The design doc lists "a
general-purpose programming language" among its non-goals: "no loops, no user-defined
functions and no arithmetic". [0020](decisions/0020-scripts-and-run.md) rejected a
script language for the first release, saying it was "a later decision to take and not
one this forecloses". The vision takes that decision. It should stay small: a
functional language for shaping values, not a second general-purpose language.

The ground is prepared:

- Pipes are function composition.
- Failure is a value that `else`, `try` and `??` handle, and a line is atomic.
- A predicate is already a function of `$row` that the language keeps as a value (a
  query), passes around and runs later.
- Scopes already nest; the specification's conformance notes say "the model is ahead
  of the shell".

Proposed:

- **A pipeline as a value.** This is the one new notation the language needs: a way to
  write a pipeline without running it. Parentheses run their pipeline at once (0023),
  and braces are components (0007). Square brackets are free as arguments and stages;
  inside a tag they hold the original grammar's property assignments, which parse and
  which nothing evaluates (a known deviation in the
  [conformance document](spec/conformance.md#known-deviations)). So
  `[where $row.size gt 100 | sort size desc]` could be a pipeline kept for later, a
  generalisation of the query value the language already has. This is the first
  question to decide.
- **Commands you define, kept as files.** A definition is a record of kind `command`
  in `/commands`, so it is listed, undone, persisted and replayed like any file.
  - `def larger size [where $row.size gt $size | sort size desc]` writes that record in
    one line.
  - `ls | larger 100` then runs it, with `$size` bound in a scope of its own.
  - Its parameters are declared as a built-in's are, so `help`, completion and
    did-you-mean treat it as a command.
  - A definition can do nothing a typed line cannot, and it joins the transaction of
    the line that calls it.
- **Per-row expressions.** `with` adds computed columns, and `each` runs a pipeline
  per row. `ls | with kb=[$row.size over 1024]` and
  `ls | each [read $row.name | lines | count]` both reuse the assignment notation of
  [0017](decisions/0017-assignment-arguments.md).
- **Arithmetic and conditions in words.**
  - Arithmetic: `plus`, `minus`, `times` and `over`, in the spirit of 0007's word
    operators. The alternative is 0007's own option D, symbols inside parentheses, which
    0023 has since given parentheses another meaning.
  - A conditional value needs words too, since `else` is taken by recovery (0014).
  - Every new word must be reserved in one go, because reserving a word later breaks
    lines that used it ([0019](decisions/0019-reserved-words-in-expression-positions.md)).
- **Folds.** `sum`, `min`, `max`, `avg`, and one general `fold`, so loops are never
  needed.
- **Modules.** `use lib.clr` loads the definitions in a script. Lines stay one line
  each, since the design doc's "multi-line syntax" non-goal can stand: a definition is
  one line, and a longer one is a file.

You will know it is working when every example program can be written without a
command the core had to add for it, and a script reads as a description of the result
rather than a list of steps.

### A more interesting filesystem

The filesystem is already a database: every file is a record of typed attributes, a
question is a place, and a saved question is a view. It can become a richer one.

Proposed:

- **Kinds with schemas.** A kind is itself a record in `/kinds` that declares its
  attributes and their types: a `task` has a `due` date and a `done` flag. `save` checks
  a new record against its kind, completion offers the attributes and their values, and
  the page can draw a form. That is one more way of not being text.
- **Links between records.** An attribute that points at another record, followed by a
  member: `$row.project.name`. `links-to readme.txt` finds what refers to a record. The
  filesystem becomes a graph, as a note-taking tool's is, while staying a table for
  every question.
- **A file's past.** Every version of every file is already in the log. `versions
  notes.txt` lists them, `read notes.txt at 12` shows one, and restoring an old version
  is one new, undoable line.
- **Tags and content search.** An attribute that holds several values, `has` over it,
  and a search of what text files say, not only their attributes.
- **More than text.** Contents are text today. Images, and anything dragged in, need
  binary blobs in the content store, and a kind for each. This is also the ground for
  drawing by kind.
- **Other places as places.** A mounted device folder, or a web API that answers
  records, can be gone into with `in` like a folder or a view (see versatility).
- **Entities as records.** If the graphics engine is accepted, a scene's entities
  should be records in this same store (0029's first open question), so `ls`, `where`,
  views and `pick` work on the world with no new vocabulary.

You will know it is working when finding something never needs remembering where it
was put, and a record can say what it belongs to.

### Built-in native data capabilities

Tables are native, and XML and CSV read into them. Nested documents are read with CSS
selectors. What is missing is mostly computation, and the formats of the web.

Proposed:

- **Real types for time.** Dates, times and durations as values:
  `ls | where $row.modified gt 2026-09-01`, and `today`, `ago 3 days`. A date literal
  is a notation question for a record.
- **JSON.** `from-json` and `to-json`. Objects read as tags and arrays as lists, so
  `pick`, `where` and the table rules work on JSON unchanged.
- **Computation.** Sums and averages alone or per group, computed columns (`with`), and
  arithmetic. This shares its notation with the scripting language, and is decided
  once, for both.
- **Joins.** `join $stock on sku`, so two tables become one.
- **Text into rows.** `lines`, `split` and `match` turn a log file or a pasted list
  into a table:
  `read log.txt | lines | where $row.text like "*ERROR*" | count`.
- **Data from the web.** `fetch` (see versatility) answers a value, not a file.
- **Scale.** Tables are lists today, which is fine at hundreds of rows. A budget, say
  ten thousand rows sorted and filtered within a keystroke, should be set and measured
  before an index is built.

You will know it is working when a small data question, such as "which suppliers are
late this month, by how much in total", is one line, and its answer is a chart as
readily as a table.

### Total customisation

Nothing is customisable yet. The way to make everything customisable, without making
the terminal harder to learn, is to keep customisation as data in the filesystem, as
the guide already is ([0036](decisions/0036-the-guide-is-in-the-filesystem.md)). A
setting is then a record, so it is undoable, replayable and persistent, you can
question it, and it can always be reset.

Proposed:

- **`/settings` as records.**
  - The theme: colours, font, density, light or dark. `attr /settings/theme
    accent=teal` restyles the page at once, through the same store event that refreshes
    a live listing.
  - The palette keys: a folder of key records, one per key, each holding the line it
    writes.
  - The prompt and location line, the banner, and completion's choices.
- **Your commands.** Definitions from the scripting pillar live in `/commands`, so an
  alias and a small tool are the same thing: `def ll [ls | select name size modified]`.
- **Renderers by kind.** `attr /kinds/note view=card` chooses how a kind is drawn.
- **A startup script.** `/settings/startup.clr` runs when the page opens. What it may
  do, and whether its lines are recorded each time, needs a record.
- **Share a setup as a script.** A configuration is a script of `attr` and `def` lines,
  so it can be copied, kept and run on another device.
- **A boundary.** Customisation changes data, appearance and vocabulary, never the
  grammar, so the guide and completion stay true for everyone. `reset settings` puts
  back every default without touching your files, and settings nobody has changed
  follow the seed as the guide does ([0040](decisions/0040-seeded-files-follow-the-seed.md)).

You will know it is working when every default you can see on the screen is a record
you can list, change and undo.

### Command replayability

The log records every line that changed anything, with both sides of every change. It
is already the source of truth, and nothing is lost on reload. Replay needs a few
commands and one idea: a moment in the log is a place, as a question is.

Proposed:

- **Run history again.** `history | where $row.source like "attr*" | replay` runs the
  chosen lines again as new lines, each its own transaction, as `run` does with a
  script.
- **Keep a session as a script.** `history | save-script morning.clr` turns what you did
  into a program, leaving out what you undid.
- **Visit the past.** Go `in` a moment (its notation is for its record), and `ls`,
  `read` and `pick` answer the store as it was then, read-only, until `out`. A slider in the page over `history` would replay
  the filesystem, or a scene, as you drag it. This is the point-in-time view
  [0010](decisions/0010-undo-by-event-sourcing.md) promised.
- **Keep every branch.** Undo, then a new line, and today the undone line can still be
  redone, because the log is flat and loses nothing. Drawing that as a tree of
  alternatives, as an editor's undo history does, is a view of what is already there.
- **Sandboxes.** Run a script or a lesson in a copy of the store, then keep the result
  or throw it away.
- **Export and import a log.** A session saved as a file can be replayed elsewhere.
  The design doc's non-goal rules out "synchronising or sharing a log", meaning an
  account, a server copy and a merge. A file you save and open yourself is none of
  those, but the non-goal says otherwise as worded, so a record should narrow it.
- **Say which commands are reproducible.** Replaying events is exact. Re-running
  commands is not, when they read the clock or the network. `download` and `fetch`
  should say so, and `replay` should warn.

You will know it is working when any session can be turned into a script that rebuilds
it, and any moment in it can be looked at again.

### A built-in graphics engine

The entity component system was built so that the command line could grow into an
editor in the manner of a game engine's. [0029](decisions/0029-scene-editor-direction.md)
and its [design direction](plan/scene-editor-direction.md) set out how, with five
rules:

- the canvas is for the scene, and the terminal stays in the DOM;
- one history, split into authored and simulated state;
- today's ECS is a prototype;
- commands reach the scene only through the store;
- one browser renderer serves both hosts.

The vision asks for that engine, so 0029 is the first record to settle.

Proposed, in four rings, each usable before the next begins:

1. **Pictures of values.**
   - Canvas renderers for charts, images and graphs of linked records (see "Not tied
     to text").
   - It proves the renderer pipeline and the batching across the JavaScript boundary
     on something small and useful.
2. **The scene.**
   - 0029's first milestone: 2D, three or four component types, no simulation.
   - Entities are records (see the filesystem), and every edit is one undoable line.
   - The scene persists across a reload, and a view follows edits live.
   - A click in the view selects, and the selection is a query the next line can use.
3. **The living scene.**
   - Play mode: simulation that is never logged, started and stopped from the command
     line.
   - Behaviour is written in the scripting language. A system is a pipeline over entity
     records, run each frame: `[where $row.@tag eq ball | with dy=[$row.dy plus 1]]`.
     The same words that question files then move a world.
4. **Depth.**
   - 3D through WebGL, with three.js or a library like it, loaded only when a 3D view
     is opened.
   - The desktop on WebView2, when 0029's rule 5 is taken up.

The budget is real: the page is 10.7 MB of a 20 MB limit. Canvas2D costs nothing to
ship, and a 3D library must be lazy-loaded and measured. A game editor has no natural
end, which is why each ring is a phase of its own, with its own records.

You will know it is working when a scene is as easy to question, change, undo and
replay as a folder, because it is one.

## What the vision changes in the current design

| Today | The vision | Needs |
| --- | --- | --- |
| Non-goal: a general-purpose programming language | Narrowed: a small functional language for definitions, per-row expressions, arithmetic and folds; still no mutation, no processes, no loops | A record superseding the non-goal |
| Non-goal: multi-line syntax | Kept: definitions are one line, and longer ones are files | Nothing |
| Non-goal: reaching the device | Revisited: explicit, permissioned imports and mounts | A record |
| Non-goal: synchronising or sharing a log | Kept for synchronisation, accounts and servers; narrowed so a log can be exported and imported as a file | A record narrowing it, with the file format |
| Non-goal: being a POSIX shell | Kept | Nothing |
| [0012](decisions/0012-browser-first.md) desktop out of scope | Superseded if 0029 is accepted: the desktop hosts the browser client | 0029 |
| [0020](decisions/0020-scripts-and-run.md) scripts are recordings | Kept for `.clr` files; definitions and modules added beside them | A record |
| [0007](decisions/0007-notation-conflicts.md) word operators | Extended with words for arithmetic and conditions, reserved in one go ([0019](decisions/0019-reserved-words-in-expression-positions.md)) | A record |
| [0029](decisions/0029-scene-editor-direction.md) Proposed | Accepted, with entities as records | The owner's answer |

## A route

A proposed order, not yet a plan. Each phase gets a document in [the plan](plan/README.md)
with a work breakdown and its records written before it is built. A phase that adds
language also gets an example program with golden results written before the code, as
Phases 3 to 6 each did ([principle 8](plan/README.md#principles-the-implementation-must-hold-to)).

| Phase | Goal | First milestone | Depends on |
| --- | --- | --- | --- |
| 12. Replay and time | See any moment; step through any line; run history again | `history \| replay`, `save-script`, a read-only visit to a moment, the pipe you tap shows what flowed through it | The log, as it is |
| 13. Data | Time, JSON, computation, joins, text into rows | Dates as values, `from-json`, `sum` and `avg`, `lines` | A decision on arithmetic's notation, shared with 14 |
| 14. The functional language | Pipelines as values; definitions; `with` and `each` | `def larger size [...]`, `ls \| larger 100`, a definition in `/commands` listed by `help` | A record superseding the non-goal |
| 15. Customisation as records | `/settings` and `/commands`; renderers by kind; a startup script | A theme change that is undone like a file | 14, for user commands |
| 16. Beyond text | Draw by kind; charts; actions on values; edit in place; panels | `chart`, images, a tag drawn as a tree, an `attr` line written by editing a cell | 15, for choosing renderers |
| 17. The scene | 0029's first milestone | `spawn`, `view`, a click that selects, `undo` that moves the box back | 0029 accepted; 16's canvas pipeline |
| 18 and after | Play mode, systems as pipelines, 3D, the desktop on WebView2, the command-line runner, mounts | Each its own record | 17, and 14 for systems |

The order follows from the dependencies:

- Replay comes first because it needs only the log, it makes every later phase easier
  to test and to teach, and it keeps a promise already on the record.
- Data and the language share the arithmetic decision, so they come next to each other.
- The language comes before customisation, because a user's own command is the heart of
  customisation.
- Drawing by kind comes before the scene, because charts are the engine's first ring.

If the engine matters most, 16 and 17 can move up. Charts need none of 13 to 15, and
the scene needs only 0029.

## Tensions to keep in view

- **Customisation against learnability.** A terminal that each person has reshaped is
  one the guide cannot describe. So customisation changes data and appearance, never
  the grammar, and every default can be restored in one line.
- **Power against the phone.** Arithmetic, conditions and definitions tempt towards
  symbols, and every symbol is a keyboard layer away. The words-first rule of 0007
  should hold unless a record argues otherwise.
- **The engine against the payload.** 20 MB is the limit, and a first visit on a phone
  is the test. Canvas2D first; anything heavier is loaded when asked for, and measured.
- **Replay against the outside world.** Events replay exactly; commands that read the
  clock or the network do not. The difference should be visible, not discovered.
- **Scope.** An editor, a language and a database each have no natural end. Every phase
  keeps a first milestone small enough to finish, as 0029 already does.

## Questions for the owner

The decisions to take, the direction first and the notation after:

1. Is [0029](decisions/0029-scene-editor-direction.md) accepted as the graphics
   direction, with option 3 and its five rules? And are entities records in the same
   store, so the filesystem's words work on the world?
2. Is the non-goal "a general-purpose programming language" narrowed as described
   above, to a small functional language with definitions, per-row expressions and
   folds?
3. Do settings and user commands live in the filesystem, as `/settings` and
   `/commands`, and is the grammar the one thing customisation never changes?
4. Is the device reachable through explicit imports and mounts?
5. How is a pipeline written without running it: square brackets, a quoted string, or
   another notation?
6. Is arithmetic written in words (`plus`, `times`, `over`) or in symbols inside a new
   kind of parentheses? And which words make a conditional, given that `else` is
   recovery?
7. Which phase comes first: replay, as proposed, or the engine?
