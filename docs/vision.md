# Vision

**Status.** The owner's aims for the project, as stated on 2026-09-24, and a proposed
route to them, written from the [decision log](decisions/README.md) as it stood at
decision 0053. It sets aims and proposes a route. It decides only what the owner has
already decided and the log records; every other change it proposes is a decision to
be written first, **Proposed** until the owner accepts it
([0001](decisions/0001-record-decisions.md)). Command syntax here is illustrative:
where a command does not exist yet, its form is for the decision that introduces it.

**Two words used here.** A *decision* is a numbered entry in the
[decision log](decisions/README.md): a short file saying what was asked, the options,
what was chosen and why. A *file record* is an entry in the terminal's filesystem: a
name, a kind and typed attributes, with optional content. "This needs a decision" means
someone writes a numbered decision and the owner accepts it before it is built.

## The vision

A terminal that is easy to understand and learn, and that is not tied to text: what a
command answers is a value, drawn as what it is. It is versatile, and it is a real
programming language, functional first. Its filesystem is more interesting than a tree
of names. It has native data capabilities, and it can be customised completely. Every
command can be replayed, and it has a graphics engine built in.

The project already stands on much of the ground this needs. A line is parsed into a
tree before anything runs, and pipes carry typed values. Failure is a value, and every
line is one undoable transaction. The filesystem is typed file records whose queries
are places, and tables, XML and CSV are native. The whole terminal runs in a phone's
browser, and nothing leaves the page. The work ahead is a widening: the ideas that now
serve files are to serve everything the terminal holds.

## The ideas under every pillar

Seven principles, each already true of part of the system and each to become true of
all of it. When a proposal below is in doubt, these decide.

1. **Everything is a value.** A command answers a value, never a printed string: a
   table, a record, a tree, a fault. Text is one way of showing a value, not what the
   value is. (Design goals; [0006](decisions/0006-functional-core-in-fsharp.md),
   [0009](decisions/0009-table-coercion.md).)
2. **One log, several domains.** Files and variables are both folded from one
   append-only log ([0010](decisions/0010-undo-by-event-sourcing.md)), which is why undo,
   persistence and history come free. Settings, the functions you define and scenes
   should be kept in that same log, each as a domain of its own with its own commands,
   not dressed up as files. Then undo, persistence and replay come free for them too,
   and the filesystem stays what it is for: your files.
3. **Every action is a line.** A tap on a chip writes text into the input, and a fix
   fills it in ([0044](decisions/0044-a-fault-may-carry-fixes.md)); the undo and `out`
   buttons run commands. Anything the screen lets you do by hand should be, or should
   write, a command line. That is what makes the screen teach the language, a session
   replayable, and any gesture scriptable.
4. **One query language for everything.** `where`, `$row`, `pick` and views work on
   files, tables and XML today. Every domain answers tables, so the same words question
   history, settings, functions and scene entities.
5. **A fixed grammar with extension points.** How a line splits into commands,
   arguments, values and operators never changes at run time. New abilities arrive
   through slots the grammar already has: commands, functions, typed tags, value kinds
   and hooks. The guide, completion and colouring then stay true for everyone.
6. **Failure and guidance are apart from output.** A fault is a value a line can
   recover from ([0014](decisions/0014-recovery-operator.md)), and what the terminal says
   of its own is drawn as guidance, never as an answer
   ([0041](decisions/0041-guidance-is-drawn-apart-from-output.md)).
7. **The phone decides, and the core is headless.** Words rather than shifted symbols
   ([0007](decisions/0007-notation-conflicts.md)), 44-pixel targets, and a keyboard
   that never opens or closes by itself. The language, the store and the commands need
   no screen, window or disk ([0003](decisions/0003-execute-in-the-browser.md)), so one
   core serves a tab, a window, a test and a script runner.

## Where it stands

| Pillar | Built | The gap |
| --- | --- | --- |
| Easy to understand and learn | The guide in the filesystem ([0036](decisions/0036-the-guide-is-in-the-filesystem.md)); completion that reads the line ([0031](decisions/0031-completion-reads-the-line.md)); help under a wrong call ([0038](decisions/0038-a-wrong-call-shows-its-help.md)); did-you-mean, fix chips and explained empty answers ([0042](decisions/0042-a-missing-name-names-the-nearest.md) to [0045](decisions/0045-a-near-value-offers-a-fix.md)); names that read as moves ([0037](decisions/0037-in-out-back-and-read.md)) | A pipeline cannot be seen or taken apart. The guide cannot check your answers. `help` knows commands, not ideas. |
| Not tied to text | Values end to end; real tables, chips and live listings in the DOM ([0004](decisions/0004-dom-not-canvas.md), [0047](decisions/0047-a-live-listing-stays-where-it-was-run.md)); guidance panels ([0041](decisions/0041-guidance-is-drawn-apart-from-output.md)) | Every value kind is drawn as a table, a chip or text. No charts, images, trees you can fold, forms, or actions on a result. |
| Versatile | One core behind the browser, the desktop build and the tests; scripts ([0020](decisions/0020-scripts-and-run.md)); XML and CSV files | One real host. No way in from the web or the device, and no way to run a script outside a tab. |
| A programming language, functional first | Decided by [0053](decisions/0053-a-programming-language-functional-first.md). Built so far: pipes as composition; failure as a value with `else`, `try` and `??`; atomic lines ([0015](decisions/0015-atomic-lines.md)); pipelines as operands ([0023](decisions/0023-adjacent-function-parenthesis.md)); variables as stages ([0032](decisions/0032-a-stage-may-be-a-value.md)); a predicate is already a value | No arithmetic, no functions you define, no loops, no conditions, no way to keep a pipeline to run later. Only a line can be written, and only a chain. |
| A more interesting filesystem | File records in the style of BeOS ([0013](decisions/0013-attribute-filesystem.md)); folders as records ([0016](decisions/0016-folders-as-records.md)); queries as places, saved views and live views; content-addressed blobs | Kinds have no schema, file records cannot refer to each other, a file's past cannot be read, and contents are text only. |
| Native data | Tables, mixed columns ([0028](decisions/0028-mixed-columns.md)), thirteen table functions, XML ([0011](decisions/0011-real-xml-files.md), [0025](decisions/0025-xml-text-content.md)), CSV, `pick` with CSS selectors ([0049](decisions/0049-pick-selects-elements-with-css-selectors.md)) | Dates are text (`modified` is a text column). No JSON, no sums or averages, no joins, no computed columns, nothing to turn text into rows. |
| Total customisation | Light and dark follow the system; the guide's files follow the seed unless you edit them ([0040](decisions/0040-seeded-files-follow-the-seed.md)) | Nothing else. The theme, the palette keys, the prompt and the commands are fixed, and there is no way to extend the terminal. |
| Command replayability | The event log, undo and redo, `history` as a table, scripts, live listings and completion re-running read-only lines ([0030](decisions/0030-undo-takes-the-line-back-on-screen.md), [0031](decisions/0031-completion-reads-the-line.md)) | `history` cannot be run again or kept as a script. The point-in-time view that [0010](decisions/0010-undo-by-event-sourcing.md) promised was never built. |
| A graphics engine | A desktop-only prototype ECS, renderer and interaction layer (about 4,000 lines across six projects); a proposed direction, [0029](decisions/0029-scene-editor-direction.md), with a design and a first milestone | 0029 is Proposed, not Accepted. Nothing is drawn on a canvas in the browser. |

## The pillars

### Easy to understand and learn

The terminal already teaches itself: its guide is files you read with `read`, and when
you get something wrong it says what you probably meant and offers the fix. What it
cannot do yet is show you what happened inside a line, check that you understood, or
answer a question about an idea rather than a command.

Proposed:

- **See inside a pipeline.** The pipeline view, described in
  [Seeing and shaping pipelines](#seeing-and-shaping-pipelines), shows each stage's
  input, output, type and values. It is the single most useful thing for learning, and
  it needs no new language.
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
  a table or a chart, a number can carry its unit, and a file record can be a card. The
  wire already carries each value's kind, so this is the page's work plus one contract
  in the host interface. Which renderer a kind gets is a setting (see customisation).
- **Charts.** `ls | chart bar name size` answers a chart value drawn on a canvas. It
  is the first and cheapest piece of the graphics engine, and useful at once.
- **Actions on values.** A file record carries what can be done with it: open, read,
  delete, go in. Choosing one writes the line it stands for, so the user sees the
  command behind the gesture. This is the "richer results" section of the
  [scene direction](plan/scene-editor-direction.md#richer-results-in-ordinary-commands).
- **Edit in place.** Change a cell of a listing, and the terminal writes and runs the
  `attr` line that does it: one transaction, undoable, and in `history`.
- **Panels.** Pin a live listing, a chart, a pipeline view or a scene beside the
  scrollback, so it stays in view while you work.

Text stays available for every value, because the desktop, the tests, the transcripts
and `to-csv` depend on it. "Not tied to text" means text is one view among several.

You will know it is working when no kind of value has to be read as text to be
understood, and anything you can do to a result by hand is a line you could have typed.

### Very versatile

The core has one real host. It could have several, and take data from more places.

Proposed:

- **More hosts for the same core.**
  - A command-line runner: `clr run nightly.clr` on a real machine, for batch jobs and
    CI, with a projection onto the real disk, which
    [0010](decisions/0010-undo-by-event-sourcing.md) anticipated.
  - An embeddable web component: a `<clr-terminal>` element, so documentation pages and
    the guide can run their examples in place.
  - The desktop hosting the browser client in WebView2, as
    [0029](decisions/0029-scene-editor-direction.md) proposes.
- **More sources, each explicit and permissioned.**
  - `fetch` answers a URL's JSON, XML or CSV as a value, where `download` only writes a
    file.
  - Files dragged onto the page are imported as file records.
  - A folder of the device can be mounted through the File System Access API. The
    design doc lists "reaching the device" as a non-goal, so this needs a decision that
    changes it.
- **More domains.** Files today, then settings, functions and scenes in the same log
  (principle 2), all questioned with the same words.

You will know it is working when the same script runs in a tab, in the command-line
runner and in a test, with the same result.

### A programming language, functional first

[Decision 0053](decisions/0053-a-programming-language-functional-first.md) settles the
direction: arithmetic, user-defined functions and loops are part of the language, and
functional constructs come first among them, because they are how pipelines are built.
What remains to decide is the notation, one decision at a time, keeping the rules
already decided: word operators (0007), reserved words chosen in one go (0019), failure
as a value (0006), and one transaction per line (0015).

The ground is prepared:

- Pipes are function composition, and failure is a value that `else`, `try` and `??`
  handle.
- A predicate is already a function of `$row` that the language keeps as a value (a
  query), passes around and runs later.
- Scopes already nest; the specification's conformance notes say "the model is ahead of
  the shell".

Proposed:

- **A pipeline as a value.** This is the notation everything else rests on: a way to
  write a pipeline without running it, so it can be named, passed, mapped and composed.
  Parentheses run their pipeline at once (0023), and braces are components (0007).
  Square brackets are free as arguments and stages; inside a tag they hold the original
  grammar's property assignments, which parse and which nothing evaluates (a known
  deviation in the [conformance document](spec/conformance.md#known-deviations)). So
  `[where $row.size gt 100 | sort size desc]` could be a pipeline kept for later, a
  generalisation of the query value the language already has.
- **Functions you define.**
  - `def larger size [where $row.size gt $size | sort size desc]` defines one, and
    `ls | larger 100` runs it, with `$size` bound in a scope of its own.
  - Its parameters are declared as a built-in's are, so `help`, completion and
    did-you-mean treat it as a command.
  - A definition is an event in the log's functions domain (principle 2), not a file:
    `defs` lists them as a table, `undo` takes one back, and they persist and replay.
  - A function called inside a line joins that line's transaction.
- **Functional constructs first.**
  - `each` runs a pipeline per row or item, and `with` adds computed columns:
    `ls | with kb=[$row.size over 1024]`, reusing the assignment notation of
    [0017](decisions/0017-assignment-arguments.md).
  - `fold` with `sum`, `min`, `max` and `avg` as its common cases.
  - Functions as values, composition, and partial application, so a pipeline can be
    built from named pieces.
- **Arithmetic, conditions and loops, in words.**
  - Arithmetic: `plus`, `minus`, `times` and `over`, in the spirit of 0007's word
    operators. The alternative is 0007's own option D, symbols inside parentheses, which
    0023 has since given parentheses another meaning.
  - Conditions need words too, since `else` is taken by recovery (0014).
  - Loops: `repeat`, and `while` over a condition, beside `each` over a collection.
    Most loops in this language will be `each` and `fold`; the others are for the cases
    they do not fit.
  - Every new word is reserved in one go, because reserving a word later breaks lines
    that used it ([0019](decisions/0019-reserved-words-in-expression-positions.md)).
- **Types you can see.** Each function has a signature, input to output, shown by
  completion, hover and the pipeline view. It is inferred where it can be, and declared
  where it cannot.
- **Modules.** `use lib.clr` loads the definitions in a script, so a library is a
  script you run.
- **More than one line when needed.** A function's body may need several lines. The
  design doc's "multi-line syntax" non-goal is revisited by the decision that defines
  functions, as 0053 says.

You will know it is working when every example program can be written without a
command the core had to add for it, and a script reads as a description of the result
rather than a list of steps.

### Seeing and shaping pipelines

The owner asked for a way to see a pipeline: its inputs, outputs, types and the values
passing between its functions. And they asked whether it could become more than a
line: a graph, perhaps edited with nodes. There are three steps here, each useful
without the next, and one alternative that may fit better than a node editor.

**1. The pipeline view.** Every entry can open a strip of cards, one per stage, showing:

- the command and the arguments it was given;
- the type that went in and the type that came out, `table (5 rows)` → `table (4 rows)`
  → `number`;
- a sample of the value at that point, and all of it when tapped;
- where a fault started, and what an `else` or a `try` did with it.

For a line that only read, the view re-runs the line stage by stage, read-only, the
way completion (0031) and live listings already do. A line that changed something
cannot be re-run, so its values between stages are kept when it runs, within a size
budget. While you type, the same view shows the line as it would run, one stage behind
the cursor. This is the step to build first: it needs no new language, it works on a
phone, and it teaches more than any document.

**2. Pipelines that branch: a graph.** A chain is one input, one output. Real work
often wants more:

- one value used twice, for example a listing both counted and charted;
- two inputs joined into one, such as a stock table and a price table;
- one input split into two branches that come back together.

Once the language can name a value and use it twice (0053's functions and variables do
this), a program is a graph of named steps rather than a chain, and the graph can be
drawn. The text stays the canonical form, because text is what undo, replay, scripts,
diffs and the phone keyboard all work on. The graph is a view of the text, never a
second language beside it.

**3. A node editor, as a view that edits.** On a large screen, the graph can be edited
directly: move a node, draw a wire, change an argument. Every edit rewrites the text,
so it lands in the scrollback as a line (principle 3) and can be undone. Blender's and
Houdini's node editors, Unreal's Blueprints, Node-RED and n8n show how capable this is.
They also show its costs:

- wiring by hand is slow for anything a line says quickly;
- big graphs become tangles;
- graphs are hard to compare and version;
- on a phone, drawing wires with a finger is miserable.

So the node editor should come after the view, and be optional.

**An alternative that may fit better: a reactive notebook.** The scrollback is already
a column of lines with their answers, and a live listing already recomputes when what
it reads changes. Take that one step further:

- An entry can be given a name, and a later line can refer to it.
- When the named entry changes, everything that refers to it recomputes, as a
  spreadsheet does, or an Observable notebook.
- The graph is then not wired by hand: it is drawn from which entry refers to which.

It stays typed text on a phone, it is a graph on a big screen, and it grows out of what
the terminal already is. [Enso](https://enso.org) is a precedent for a functional
language that is text and graph at once, and worth studying before either is built.

The recommendation: build the pipeline view first. Then take the notebook model as the
way pipelines become graphs, with a graph view drawn from it. Add node editing on
larger screens once the graph exists. The graph view is also the graphics engine's
first real scene: nodes and wires on a canvas.

### A more interesting filesystem

The filesystem is already a database: every file is a record of typed attributes, a
question is a place, and a saved question is a view. It can be a richer one, while
staying the place for your files and nothing else (principle 2).

Proposed:

- **Kinds with schemas.** A kind declares its attributes and their types: a `task` has a
  `due` date and a `done` flag. `save` checks a new file record against its kind,
  completion offers the attributes and their values, and the page can draw a form. A
  kind is a definition in the log (like a function), not a file.
- **Links between file records.** An attribute that points at another file record,
  followed by a member: `$row.project.name`. `links-to readme.txt` finds what refers to
  a file. The filesystem becomes a graph, as a note-taking tool's is, while staying a
  table for every question.
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

You will know it is working when finding something never needs remembering where it
was put, and a file can say what it belongs to.

### Built-in native data capabilities

Tables are native, and XML and CSV read into them. Nested documents are read with CSS
selectors. What is missing is mostly computation, and the formats of the web.

Proposed:

- **Real types for time.** Dates, times and durations as values:
  `ls | where $row.modified gt 2026-09-01`, and `today`, `ago 3 days`. How a date is
  written is a decision of its own.
- **JSON.** `from-json` and `to-json`. Objects read as tags and arrays as lists, so
  `pick`, `where` and the table rules work on JSON unchanged.
- **Computation.** Sums and averages alone or per group, computed columns (`with`), and
  arithmetic, sharing their notation with the language.
- **Joins.** `join $stock on sku`, so two tables become one.
- **Text into rows.** `lines`, `split` and `match` turn a log file or a pasted list
  into a table: `read log.txt | lines | where $row.text like "*ERROR*" | count`.
- **Data from the web.** `fetch` (see versatility) answers a value, not a file.
- **Scale.** Tables are lists today, which is fine at hundreds of rows. A budget, say
  ten thousand rows sorted and filtered within a keystroke, should be set and measured
  before an index is built.

You will know it is working when a small data question, such as "which suppliers are
late this month, by how much in total", is one line, and its answer is a chart as
readily as a table.

### Total customisation

Nothing is customisable yet. Customisation should not depend on the filesystem: files
are yours, and settings are the terminal's. They belong in the same log as a domain of
their own (principle 2). So a setting is undoable, replayable and persistent, you can
question it with the same words, and it can always be reset. What you add to the
terminal comes in through the extension points (principle 5), never by changing the
grammar.

Proposed:

- **Settings as a domain of the log.**
  - `settings` answers a table: the theme's colours, font and density; light or dark;
    the palette keys; the prompt and location line; the banner; completion's choices;
    the renderer for each kind.
  - `config theme accent=teal` changes one: an event in the log, so the page restyles
    at once through the same store event that refreshes a live listing, and `undo` puts
    it back.
  - `settings | where $row.changed` shows what you have changed, and `reset settings`
    puts back every default without touching your files.
  - A setup is exported as a script of `config` and `def` lines, so it can be kept and
    run on another device.
- **Your own commands** are the functions of 0053, listed by `defs`: an alias and a
  small tool are the same thing, `def ll [ls | select name size modified]`.
- **Extension points.** Everything else is added through the slots in
  [A fixed grammar with extension points](#a-fixed-grammar-with-extension-points):
  commands, functions, typed tags, value kinds and their renderers, and hooks, such as
  a function run when the page opens.

You will know it is working when every default you can see on the screen is a setting
you can list, change and undo, and the terminal can be extended without anyone editing
its grammar.

### A fixed grammar with extension points

The owner wants the base grammar to stay fixed and still be extensible. That is the
right choice for this terminal. A grammar that each person can change would break the
guide, completion, colouring, fix chips and the phone-first word operators, since each
of those reads the grammar. The way to have both is to fix the grammar and give it
named slots, each of which already exists or nearly does.

The slots, in the order they would be built:

| Slot | What plugs in | Already there |
| --- | --- | --- |
| Commands | New commands, from the core, a host (the scene engine) or a user's `def` | Every command is a `CommandSpec`; `help` and completion read them |
| Functions | Operations used inside expressions, written in the function form `name(args)` | The function form exists ([0023](decisions/0023-adjacent-function-parenthesis.md)); new operations need no new operators |
| Typed tags | A kind registers how a tag of its type reads and draws: `<date value=2026-09-01/>`, `<colour hex=336699/>` | Tags are the notation for structured values ([0009](decisions/0009-table-coercion.md)) |
| Value kinds | A kind's renderer, its `@` members, its completion and hover | `@tag` and `@children` ([0048](decisions/0048-a-tags-own-parts-are-read-with-at.md)); kinds reach the page |
| Hooks | Functions run on an event: the page opening, a line committing, a fault, a store change, a scene frame | The store raises `storeChanged` after every commit |

Hooks are the powerful slot and the risky one: a hook can make the terminal do things
nobody typed. Three rules keep them honest:

- **Visible.** A hook's effect is a line in the scrollback, or a note drawn as guidance,
  never silent.
- **Undoable.** What a hook changes is a transaction of its own in the log, taken back
  like any other.
- **Not during replay.** Replaying the log applies events, and runs no hooks, so a
  replay cannot set off a new chain of effects.

`hooks` lists them, and any one can be switched off.

What is deliberately not a slot: user-defined syntax. Macros, new operators and reader
extensions would each make one person's terminal unreadable to the guide and to
everyone else. New operations are functions; new literals are typed tags; new words are
commands.

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
- **Visit the past.** Go `in` a moment, and `ls`, `read` and `pick` answer the store as
  it was then, read-only, until `out`; how a moment is written is for its decision. A
  slider in the page over `history` would replay the filesystem, or a scene, as you
  drag it. This is the point-in-time view
  [0010](decisions/0010-undo-by-event-sourcing.md) promised.
- **Keep every branch.** Undo, then a new line, and today the undone line can still be
  redone, because the log is flat and loses nothing. Drawing that as a tree of
  alternatives, as an editor's undo history does, is a view of what is already there.
- **Sandboxes.** Run a script or a lesson in a copy of the store, then keep the result
  or throw it away.
- **Export and import a log.** A session saved as a file can be replayed elsewhere.
  The design doc's non-goal "synchronising or sharing a log" is about accounts, server
  copies and merging, and a file you save and open yourself is none of those. The
  non-goal's wording still forbids it, so a decision should narrow it.
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

The vision asks for that engine, so 0029 is the first decision to settle. Its first
open question, whether entities are file records, is answered by principle 2: a scene
is a domain of the log of its own, listed by its own commands as tables, so `where`,
views and `pick` work on the world without the world being files.

Proposed, in four rings, each usable before the next begins:

1. **Pictures of values.**
   - Canvas renderers for charts, images, the pipeline graph and graphs of linked
     files.
   - They prove the renderer and the batching across the JavaScript boundary on
     something small and useful.
2. **The scene.**
   - 0029's first milestone: 2D, three or four component types, no simulation.
   - Every edit is one undoable line. The scene persists across a reload, and a view
     follows edits live.
   - A click in the view selects, and the selection is a query the next line can use.
3. **The living scene.**
   - Play mode: simulation that is never logged, started and stopped from the command
     line.
   - Behaviour is written in the language. A system is a function over entities run
     each frame through the scene-frame hook:
     `[where $row.@tag eq ball | with dy=[$row.dy plus 1]]`. The same words that
     question files then move a world.
4. **Depth.**
   - 3D through WebGL, with three.js or a library like it, loaded only when a 3D view
     is opened.
   - The desktop on WebView2, when 0029's rule 5 is taken up.

The budget is real: the page is 10.7 MB of a 20 MB limit. Canvas2D costs nothing to
ship, and a 3D library must be lazy-loaded and measured. A game editor has no natural
end, which is why each ring is a phase of its own, with its own decisions.

You will know it is working when a scene is as easy to question, change, undo and
replay as a folder.

## What the vision changes in the current design

| Today | The vision | Needs |
| --- | --- | --- |
| Non-goal: a general-purpose programming language | Withdrawn: arithmetic, functions and loops, functional first | Done: [0053](decisions/0053-a-programming-language-functional-first.md) |
| Non-goal: multi-line syntax | Revisited when functions are defined | The decision that defines functions |
| Non-goal: reaching the device | Revisited: explicit, permissioned imports and mounts | A decision |
| Non-goal: synchronising or sharing a log | Kept for accounts, servers and merging; narrowed so a log can be exported and imported as a file | A decision, with the file format |
| Non-goal: being a POSIX shell | Kept | Nothing |
| [0012](decisions/0012-browser-first.md) the desktop gets no new presentation | Superseded if 0029 is accepted: the desktop hosts the browser client | 0029 |
| [0020](decisions/0020-scripts-and-run.md) scripts are recordings | Kept for `.clr` files; one consequence superseded by 0053 | Done |
| [0007](decisions/0007-notation-conflicts.md) word operators | Extended with words for arithmetic, conditions and loops, reserved in one go ([0019](decisions/0019-reserved-words-in-expression-positions.md)) | A decision |
| [0029](decisions/0029-scene-editor-direction.md) Proposed | Accepted, with the scene as its own domain of the log | The owner's answer |

## A route

A proposed order, not yet a plan. Each phase gets a document in [the plan](plan/README.md)
with a work breakdown, and its decisions written before it is built. A phase that adds
language also gets an example program with golden results written before the code, as
Phases 3 to 6 each did ([principle 8](plan/README.md#principles-the-implementation-must-hold-to)).

| Phase | Goal | First milestone | Depends on |
| --- | --- | --- | --- |
| 12. See and replay | The pipeline view; `history` run again and kept as a script; a read-only visit to a moment | A card per stage with its types and values; `history \| replay`; `save-script` | The log, as it is |
| 13. The language, part one | A pipeline as a value; arithmetic; functions; `each`, `with`, `fold` | `def larger size [...]`, `ls \| larger 100`, `defs`, `ls \| with kb=[$row.size over 1024]` | Decisions on the notations |
| 14. Data | Time, JSON, joins, text into rows | Dates as values, `from-json`, `join`, `lines` | 13, for arithmetic |
| 15. Settings and extension points | Settings as a domain; typed tags; value kinds; hooks | `config theme accent=teal`, undone like any line; a hook run when the page opens | 13, for functions as hooks |
| 16. Beyond text | Draw by kind; charts; actions on values; edit in place; panels; the pipeline graph | `chart`, images, a tag drawn as a tree, an `attr` line written by editing a cell | 15, for renderers |
| 17. The scene | 0029's first milestone | `spawn`, `view`, a click that selects, `undo` that moves the box back | 0029 accepted; 16's canvas |
| 18 and after | The language, part two (conditions, loops, modules); the reactive notebook and node editing; play mode; 3D; the desktop on WebView2; the command-line runner; mounts | Each its own decision | As each needs |

The order follows from the dependencies:

- Seeing and replaying come first: they need only the log, they make every later phase
  easier to test and to teach, and they keep a promise already on the record.
- The language comes next because data, settings, hooks and scene behaviour are all
  written in it.
- Settings and the extension points come before drawing by kind, because a renderer is
  something a kind plugs in.
- Drawing by kind comes before the scene, because charts and the pipeline graph are the
  engine's first ring.

If the engine matters most, 16 and 17 can move up. Charts need only a canvas, and the
scene needs only 0029.

## Tensions to keep in view

- **Customisation against learnability.** A terminal each person has reshaped is one the
  guide cannot describe. So customisation changes settings and adds through extension
  points, never the grammar, and every default can be restored in one line.
- **A real language against the phone.** Arithmetic, conditions, loops and functions
  tempt towards symbols, and every symbol is a keyboard layer away. The words-first
  rule of 0007 should hold unless a decision argues otherwise.
- **Graphs against text.** A node editor is appealing and slow to use for what a line
  says quickly. Text stays canonical; graphs are views of it.
- **Hooks against predictability.** Every hook is visible, undoable and silent during
  replay, or it is not a hook this terminal has.
- **The engine against the payload.** 20 MB is the limit, and a first visit on a phone
  is the test. Canvas2D first; anything heavier is loaded when asked for, and measured.
- **Replay against the outside world.** Events replay exactly; commands that read the
  clock or the network do not. The difference should be visible, not discovered.
- **Scope.** An editor, a language and a database each have no natural end. Every phase
  keeps a first milestone small enough to finish, as 0029 already does.

## Questions for the owner

The decisions to take, the direction first and the notation after:

1. Is [0029](decisions/0029-scene-editor-direction.md) accepted as the graphics
   direction, with option 3 and its five rules, and with the scene as its own domain of
   the log rather than file records?
2. Are settings and the functions you define kept in the log as domains of their own,
   with their own commands (`settings`, `config`, `defs`), rather than as files?
3. Are the extension points the five slots above (commands, functions, typed tags,
   value kinds and hooks), with hooks held to the three rules, and user-defined syntax
   left out?
4. For pipelines beyond a line: the reactive notebook first, with a graph drawn from
   it, or a node editor first?
5. How is a pipeline written without running it: square brackets, a quoted string, or
   another notation?
6. Is arithmetic written in words (`plus`, `times`, `over`) or in symbols inside a new
   kind of parentheses? Which words make a condition and a loop, given that `else` is
   recovery?
7. Is the device reachable through explicit imports and mounts?
8. Which phase comes first: seeing and replaying, as proposed, or the engine?
