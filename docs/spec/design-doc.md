# Design doc: a command line that parses to a tree

| Field | Value |
| --- | --- |
| Status | Implemented |
| Scope | Language, execution layer, store, browser terminal |
| Companion documents | [Grammar](lexical-grammar.md), [Semantic tree](semantic-tree.md), [Execution model](execution-model.md), [Commands](command-catalogue.md), [Host interfaces](host-interfaces.md), [Conformance](conformance.md) |

## Context and scope

A conventional shell moves text between programs. Each program re-parses that text, so
the shell's own knowledge of what a word meant is thrown away at every boundary. The
costs are familiar: quoting rules that differ per program, file names with spaces
breaking pipelines, no way to point at a word on screen and ask what it is, and no way
to undo anything.

This project takes the other path. The command line is parsed once into a tree.
Commands are declared functions with typed parameters: each takes an invocation and
returns a value and a list of events describing what it would change, and never changes
anything itself. A pipeline threads values, not bytes. The events of a whole line are
committed together to an append-only log, and the filesystem, the variables and the
current location are what that log adds up to.

The filesystem is not a tree of directories. Every file is a record of typed attributes
with optional content; `folder` is one attribute among them, and a question about
attributes is a place you can stand in, the way BeOS and Haiku let a query stand in for
a folder. A listing is a table, and the same predicate language filters a table, defines
a view and finds a record.

The system has three front ends over one execution layer: a browser terminal compiled to
WebAssembly, which is the reference front end; a Windows desktop shell built on an
entity component system; and the test suites. This document covers the language, the
execution layer, the store and the browser terminal. The desktop shell's scene and
rendering are out of scope ([decision 0012](../decisions/0012-browser-first.md)); it is
required to compile and to run commands, and it renders results as text.

## Goals

- **Parse once.** The text becomes a tree before anything runs, and every later stage
  works on the tree or on values.
- **Preserve provenance.** Every run of characters on screen knows the role the grammar
  gave it, so the display can colour it and a user can interrogate it.
- **Type-preserving pipelines.** A value entering a pipe is the value leaving the
  previous command, not a rendering of it. A listing stays a table until something asks
  for less.
- **Failure is a value.** A command that cannot do what was asked returns a fault; no
  exception crosses a module boundary. A line can hold a fault, test it and recover from
  it without leaving the line.
- **Atomic, reversible lines.** One line is one transaction: it commits whole or not at
  all, and any committed line can be taken back and put back again.
- **Nothing is lost on reload.** The browser keeps the log, and reopening the page
  replays it to exactly the state it left.
- **Queries are places.** A predicate over attributes can be listed, entered, saved and
  watched like a folder.
- **One execution layer, several hosts.** Execution depends on interfaces, never on a
  user interface, so the same commands run in a tab, in a window and in a test.
- **Testable headlessly.** No scene, no window, no browser and no disk is required to
  exercise the language, the commands or the store.

## Non-goals

- **Being a POSIX shell.** No globbing, no redirection, no job control, no environment,
  no processes. Those are deliberate omissions, not a backlog.
- **A general-purpose programming language.** There are no loops, no user-defined
  functions and no arithmetic. Predicates compare, and combine with `and`, `or` and
  `not`; recovery has `else`, `try` and `??`. A script is a file of lines run one at a
  time ([decision 0020](../decisions/0020-scripts-and-run.md)), not a program with
  control flow.
- **Multi-line syntax.** One line is one pipeline. A script is many lines, each its own
  transaction.
- **Reaching the device.** The browser filesystem is the tab's own. Nothing reads or
  writes the device's files, and nothing leaves the page unless the user runs
  `download`.
- **Synchronising or sharing a log.** The log belongs to one origin in one browser.
  There is no account, no server copy and no merge.
- **Desktop parity.** The desktop shell keeps compiling and running commands; its
  interactive listings and its on-disk filesystem are not carried forward.
- **Backwards compatibility with the original GOLD grammar's gaps.** Where that grammar
  parsed something the interpreter could not execute, the gap is closed rather than
  preserved.

## The design

### System context

```
                    ┌──────────────────────────────────────────────┐
   you type ───────▶│ front end: browser page / desktop shell      │
                    └───────┬──────────────────────────────▲───────┘
                            │ source text                  │ DTOs / display strings
                            ▼                              │
                    ┌─────────────────────────────────────┴────────┐
                    │ host adapter (C#): Web.Core TerminalSession,  │
                    │ desktop DesktopSession                        │
                    └───────┬──────────────────────────────▲───────┘
                            │                              │ Response
                            ▼                              │
  ┌─────────────────────────────────────────────────────────┴──────────────┐
  │ Core (F#): Session                                                     │
  │                                                                        │
  │  Parser.FParsec ─▶ tree ─▶ Binder ─▶ Evaluator ─▶ commands ─▶ events    │
  │                                         │    ▲                  │      │
  │                                   IOutput│    │ reads            ▼      │
  │                                   (live) ▼    │            Store: log, │
  │                                                └── Projection ◀─ fold  │
  └───────────────────────────────────────────────────────┬────────────────┘
                                                          │ ILog
                                         ┌────────────────┴───────────────┐
                                         │ IndexedDB (browser) / memory   │
                                         └────────────────────────────────┘
```

F# owns semantics: values, faults, the store, the evaluator, the commands and the
session. C# owns hosting: the Blazor bridge, the ASP.NET host, the WPF shell and the
mapping from values to the page's JSON. `TerminalSession` is the boundary: nothing past
it sees a `Value` or a `Result`, and the desktop adapter reads a response only to print
its display strings ([decision 0006](../decisions/0006-functional-core-in-fsharp.md)).

### Stages

1. **Parse.** `CommandLineParser.Parse` turns the text into a semantic tree or a
   `ParserError`. Errors carry a zero-based line and column and the symbols the grammar
   could have accepted. See [Grammar](lexical-grammar.md).
2. **Tokenise.** Beside the run, the host flattens the tree to `(text, kind)` pairs for
   colouring and inspection, and serialises it back to text to prove the parse lost
   nothing. See [Semantic tree](semantic-tree.md).
3. **Bind.** `Binder` matches written arguments to declared parameters, drawing on the
   piped value and on defaults, and hands a predicate to a parameter declared to take
   one. See [Execution model](execution-model.md#argument-binding).
4. **Evaluate.** `Evaluator` folds the pipeline, resolving each expression to a `Value`.
   Each command reads the current projection and returns a value and the events that
   describe its change. Tags build values without calling a command, and a variable
   standing as a stage is its value
   ([decision 0032](../decisions/0032-a-stage-may-be-a-value.md)). `else`, `try` and
   `??` decide which branch's events survive.
5. **Commit.** When the line has succeeded, its events are appended to the log as one
   transaction, and the projection moves. A line that failed commits nothing, and a line
   that produced no events commits nothing either
   ([decision 0015](../decisions/0015-atomic-lines.md)).
6. **Project.** The files, the variables and the location are the fold of every event
   in the log. Nothing else holds state, so undo, redo, a reload and a test all read the
   same thing.
7. **Render.** The host turns the response into its own presentation: tables, chips and
   text blocks in the browser, text blocks in the desktop scene.

### Key design decisions

**Commands describe, the store applies.** A command is a function from an invocation to
a value and events. It cannot mutate the projection, because it is only ever given a
read of it and a capability to put and get blobs. This is what makes a line atomic
without a rollback: until the commit, nothing has happened.

**Failure is a value.** Every command returns a result that is either a value with
events or a `Fault` with a kind, a message and, where it has them, the stage and the
path. The session boundary turns an unexpected exception into a fault of kind `Internal`,
so a defect is reported rather than killing the terminal. Because a fault is a value, the
language can hold one: `try` makes a stage's failure its result, `else` hands a failure
to the pipeline that recovers, and `??` defaults an empty result
([decision 0014](../decisions/0014-recovery-operator.md)). Stop is not a failure a line
can recover from ([decision 0024](../decisions/0024-stop-is-not-recoverable.md)).

**Undo is a compensating transaction.** Every event carries both sides of its change, so
inverting one needs nothing but the event. `undo` appends the last line's events
inverted and reversed, marked as compensating it; `redo` compensates the compensation
([decision 0010](../decisions/0010-undo-by-event-sourcing.md)). No command knows how to
reverse itself, and none is asked to. The seeded filesystem is recorded but cannot be
undone ([decision 0018](../decisions/0018-the-seed-is-not-a-line-anyone-typed.md)).

**Files are attribute records.** A directory is a record whose `kind` is `folder`, and
the root is implicit ([decisions 0013](../decisions/0013-attribute-filesystem.md) and
[0016](../decisions/0016-folders-as-records.md)). Because the hierarchy is one attribute
among many, `in` on a predicate sets a view, `ls` in a view lists across folders, and
`save-view` stores the question as a file you can go into with `in` later.

**Tables are values, and a tag can be one.** `ls` returns a table. A tag whose children
all have the same type and no children of their own coerces to a table wherever a table is expected,
with missing cells as `None` ([decision 0009](../decisions/0009-table-coercion.md)); XML
reads into the same tree, so a table-shaped document is a table for the same reason
([decision 0011](../decisions/0011-real-xml-files.md)).

**Predicates use words and name the row.** `where $row.qty lt 10 and $row.kind eq part`.
Word operators leave `<`, `>` and `|` to tags and pipes with no lookahead tricks
([decision 0007](../decisions/0007-notation-conflicts.md)), and `$row` is explicit so a
predicate reads the same in `where`, `in` and a saved view
([decision 0008](../decisions/0008-explicit-row-variable.md)). A predicate is a
yes-or-no question about the row, so one that never reads `$row`, or answers a row with
something other than true or false, is a fault rather than an empty table
([decision 0033](../decisions/0033-a-predicate-is-a-question-about-the-row.md)).

**Output is an interface, not a console.** Long-running commands need to say something
before they finish. `IOutput` gives them a line to write and mutate. The browser host
raises an event, coalesced for the page; the desktop host writes into the scene; the
test host records. Commands therefore never reference a loop, a window or a graphics
library, which is what lets them run under WebAssembly.

**The parser is combinators, not tables.** The generated LALR parser left about a third
of the grammar unimplemented, reported every error at column zero when wrapped for
backtracking, and required an external tool and an embedded binary table. Combinators
put the grammar in source, in the same order as the original BNF, and make error
positions faithful ([decision 0002](../decisions/0002-combinator-parser.md)). The old
parser is retained only so the two can be compared.

**The browser executes.** The parser, the core and the store are .NET compiled to
WebAssembly and run in the tab ([decision 0003](../decisions/0003-execute-in-the-browser.md)),
rendering to the document rather than a canvas
([decision 0004](../decisions/0004-dom-not-canvas.md)).

### Data storage

There is no filesystem underneath this. There is a log, a blob store, and a fold.

**The log** is an append-only list of transactions, each one command line's worth of
change: a sequence number, a timestamp, the line as it was typed, the events, whether it
can be undone, and optionally the sequence number of the transaction it compensates.
Nothing is ever edited or removed. `reset` is the one exception and it empties the log
rather than amending it.

**Blobs** hold file content, addressed by the SHA-256 of the text. An event names hashes
rather than carrying text, so keeping every version of a file costs one hash per write,
and undoing a write is pointing at the old hash again. A command reaches the blob store
through a capability that can put and get and cannot append, undo or read the history.

**The projection** is what the log adds up to: the files, the variables and the
location. It is not state that commands mutate — it is `fold apply empty events`, and a
fresh store replaying the same log **must** arrive at exactly the same projection. That
identity is what makes a browser reload a replay rather than a restore.

A file in the projection is a record: an id, a map of attributes and an optional content
hash. A location is a folder and, optionally, a view: the predicate being looked
through. A saved view is a record whose `kind` is `view` and whose content is the
predicate's text ([The filesystem](../filesystem.md)).

Documents are files like any other. XML and CSV are formats for a file's content, not
kinds of storage: `from-xml` parses the text into the tree the tag notation produces,
and `to-xml` and `to-csv` serialise a value and write it with the events `write` would
emit ([decision 0011](../decisions/0011-real-xml-files.md)). The tree has nowhere for an
element's text, so it is read as an attribute called `text`
([decision 0025](../decisions/0025-xml-text-content.md)).

Where the log lives is the host's business. The browser keeps it in IndexedDB for the
page's origin, in a hand-written, versioned JSON shape so that a later build can read an
earlier one's log ([Host interfaces](host-interfaces.md#the-stored-shape)); the tests and
the desktop shell keep it in memory.

The log grows with use and nothing compacts it. Snapshotting the projection and
replaying only the log after it is the obvious answer, and is deliberately deferred
until a log exists that is big enough to notice: a tab's logs are small, and replay is a
fold over events that each touch one record.

### Degree of constraint

The following are fixed and an implementation **must not** vary them: the grammar, the
reserved words, the token kinds, the value kinds and their two string forms, the binding
order, pipeline and recovery semantics, the atomicity of a line, the undo contract, the
stored log format and the wire formats in [Host interfaces](host-interfaces.md).

The following are deliberately left open: how a host renders values, which commands are
registered beyond the catalogue, where the log is kept, how completion ranks its
answers, whether a host offers live listings at all, and how a host schedules the
evaluator with respect to its own input loop.

## Alternatives considered

Each of these is recorded in full, with every option that was on the table, in the
[decision log](../decisions/README.md).

**Keep the GOLD LALR parser and finish its visitor.** Rejected. The unimplemented
productions were a symptom: the table-driven design put the grammar in a binary
artefact produced by an external tool, so a change meant regenerating tables outside the
build. The error-position problem was structural rather than a bug.

**Pass text between commands, as a conventional shell does.** Rejected. It defeats the
premise. Every benefit here, from tapping a token to piping a table of records with
spaces in their names, follows from not flattening.

**Keep the execution layer in C#, with exceptions for failure.** Rejected
([0006](../decisions/0006-functional-core-in-fsharp.md)). An exception is invisible in a
signature, so nothing forced a command to say how it fails, and the language could not
hold a failure as a value. F#'s `Result` and discriminated unions make the error model
and the event model the types themselves.

**Undo by asking each command to reverse itself.** Rejected
([0010](../decisions/0010-undo-by-event-sourcing.md)). It was what the original code
did: every command carried its own inverse, the inverses drifted from the commands, and
nothing could be replayed. Events that carry both sides give undo, redo, history and
persistence from one mechanism.

**Commit per command rather than per line.** Rejected
([0015](../decisions/0015-atomic-lines.md)). A line that failed at its third stage would
leave its first two stages' changes behind, and undo would take back a fragment of a
line nobody typed.

**A directory tree with tags on the side.** Rejected
([0013](../decisions/0013-attribute-filesystem.md)). Two models of where a file is would
disagree, and a query would be a search result rather than a place.

**Symbols for comparison, and an implicit row.** Rejected
([0007](../decisions/0007-notation-conflicts.md),
[0008](../decisions/0008-explicit-row-variable.md)). `<` and `>` already open tags and `|`
already pipes; an implicit row makes a bare name mean a column in one place and a word in
another.

**Spell recovery `or`.** Rejected ([0014](../decisions/0014-recovery-operator.md)). `or`
is a boolean operator inside a predicate, and one word meaning two things at two
precedences is a trap.

**Persist a snapshot of the filesystem in local storage.** Rejected in favour of the log
in IndexedDB. A snapshot would persist the files and lose undo, redo and history across
a reload, local storage is synchronous and small, and a snapshot format would need its
own migrations beside the log's.

**Render the browser terminal on a canvas, mirroring the desktop shell.** Rejected
([0004](../decisions/0004-dom-not-canvas.md)). The document object model gives
scrolling, text selection, the soft keyboard, and accessible text for free, and token
spans keep the per-token interaction that the canvas demonstrated.

**Run the evaluator on the server and stream results to the page.** Rejected for the
default deployment ([0003](../decisions/0003-execute-in-the-browser.md)). It introduces a
session per user, latency per keystroke for colouring, and a data-protection question,
in exchange for nothing the client cannot do. The ASP.NET host remains available for
tooling, and parses only.

## Cross-cutting concerns

### Security and privacy

The browser terminal executes entirely in the page. No command text, result or file is
transmitted. The only network access is `download`, which the user initiates explicitly
and which the browser constrains by the same-origin policy and the remote host's CORS
headers.

The log, and with it every file and every line typed, is kept in IndexedDB for the
page's origin, on the device, and nowhere else. It is readable by anything that can run
script in that origin, and it goes when the user clears the site's data. A private
window has no persistent storage: the terminal runs in memory and the page says
`not persisted` before the first command, so nobody finds out at the end that nothing
was kept. `reset` empties the log and starts again from the seed. Because the log is a
history, `rm` removes a file from the projection but not from the log: a user who needs
a line gone from the device clears the log with `reset` or clears the site's data.

A file in the store is whatever someone wrote into it, so reading one must not be a way
to stop the tab: `from-xml` refuses a document with a DTD rather than expanding its
entities.

The desktop shell has the privileges of the user who ran it, but its filesystem is the
same in-memory projection as the browser's, so no command reaches the disk. `exit`
reaches the host through the session's options, so a host decides what shutting down
means rather than a command reaching for a window.

### Failure handling

A command failure is a `Fault` carrying a kind and a message for the user. The session
returns it in the response, and the line commits nothing. Unexpected exceptions are
caught at the same boundary and reported as a fault of kind `Internal` naming their
type, so a defect surfaces as a message rather than a dead terminal. A failed line
leaves the session usable.

Storage failure is not a command failure. A browser without IndexedDB, or one whose
storage stops answering mid-session, falls back to memory; the session keeps working and
the page reports that it is no longer persisting. A stored transaction this build cannot
decode is skipped and counted rather than refusing to open the session.

### Observability

The parse response carries the token stream and the re-serialised text, so a host can
detect a lossy parse. The execution response carries the output lines, the result, the
error with its structured fault, and the location, which is enough to reconstruct what
happened without inspecting internals. `history` shows every committed transaction and
what it compensated, and the log itself is the audit trail.

### Accessibility

The browser terminal is text in the document, so screen readers, text zoom and text
selection work. Colour is never the only carrier of meaning: an error is a sentence with
its kind as a word beside it, a caught fault is drawn differently from a failed line and
says so, results are structurally distinct from output, and the token inspector names a
role in words. The layout targets a 390 pixel wide viewport without horizontal page
scrolling. The input, the run button, result chips, table cells and the suggestion and
completion chips are at least 44 pixels tall.

### Internationalisation

Identifiers accept a defined set of accented letters. Numbers parse and print with the
invariant culture, so a decimal point means the same thing everywhere, including in a
CSV file. Messages are English only, and the client is published with invariant
globalisation to keep the payload small; localisation would require both decisions to
be revisited.

### Performance

The payload is about 10 MB on a first visit, once the precompressed copies are set
aside, and cached afterwards. Parsing runs on every keystroke for colouring and
completion, which is comfortably fast because the input is one short line. Completion
may also run the stages before the cursor to learn what flows into the one being
written; that run is read-only, cached until the next commit, cancelled by the next
keystroke, and given 150 milliseconds before completion answers without it
([decision 0031](../decisions/0031-completion-reads-the-line.md)). A reload
replays the whole log before the input is enabled; that is a fold over small events and
is not noticeable at the sizes a tab reaches. Live output is coalesced to about one
update every 40 milliseconds so that a fast-updating command does not spend more time
crossing the interoperability boundary than doing its work, and a live listing
re-reads only after a commit and only when nothing is running.
