# Design doc: a command line that parses to a tree

| Field | Value |
| --- | --- |
| Status | Implemented |
| Scope | Language, execution layer, browser terminal |
| Companion documents | [Grammar](lexical-grammar.md), [Semantic tree](semantic-tree.md), [Execution model](execution-model.md), [Commands](command-catalogue.md), [Host interfaces](host-interfaces.md), [Conformance](conformance.md) |

## Context and scope

A conventional shell moves text between programs. Each program re-parses that text, so
the shell's own knowledge of what a word meant is thrown away at every boundary. The
costs are familiar: quoting rules that differ per program, file names with spaces
breaking pipelines, no way to point at a word on screen and ask what it is, and no way
to undo anything.

This project takes the other path. The command line is parsed once into a tree.
Commands are declared objects with typed parameters, they return values rather than
printing text, and each knows how to reverse itself. A pipeline threads values, not
bytes.

The system has three front ends over one execution layer: a Windows desktop shell built
on an entity component system, a browser terminal compiled to WebAssembly, and a test
harness. This document covers the language and the execution layer, and the browser
terminal as the reference front end. The desktop shell's rendering and its event-sourced
scene are out of scope.

## Goals

- **Parse once.** The text becomes a tree before anything runs, and every later stage
  works on the tree or on values.
- **Preserve provenance.** Every run of characters on screen knows the role the grammar
  gave it, so the display can colour it and a user can interrogate it.
- **Type-preserving pipelines.** A value entering a pipe is the value leaving the
  previous command, not a rendering of it.
- **Reversible commands.** Executing a command records enough to undo it.
- **One execution layer, several hosts.** Execution depends on interfaces, never on a
  user interface, so the same commands run in a window, in a browser tab and in a test.
- **Testable headlessly.** No scene, no window and no graphics stack is required to
  exercise the language or the commands.

## Non-goals

- **Being a POSIX shell.** No globbing, no redirection, no job control, no scripting
  constructs. Those are deliberate omissions, not a backlog.
- **A persistent filesystem in the browser.** The in-tab filesystem is a demonstration
  surface, not storage.
- **Multi-line programs.** One line is one pipeline.
- **A general-purpose expression language.** Values come from commands and tags; there
  is no arithmetic, no conditionals and no loops.
- **Backwards compatibility with the original GOLD grammar's gaps.** Where that grammar
  parsed something the interpreter could not execute, the gap is closed rather than
  preserved.

## The design

### System context

```
                    ┌──────────────────────────────────────────────┐
   you type ───────▶│ front end: browser terminal / desktop shell  │
                    └───────┬──────────────────────────────┬───────┘
                            │ source text                  │ rendered result
                            ▼                              ▲
                    ┌───────────────┐   semantic tree   ┌───┴──────────────┐
                    │ Parser.FParsec├──────────────────▶│    Evaluator     │
                    └───────────────┘                   └───┬──────────────┘
                            │                               │
                            │ tokens                        ├─▶ Binder
                            ▼                               ├─▶ Scope (variables)
                    ┌───────────────┐                       ├─▶ IOutput (live)
                    │ colouring,    │                       └─▶ Commands ─▶ events
                    │ inspection    │                               │
                    └───────────────┘                               ▼
                                                            ┌──────────────┐
                                                            │ Store ─▶ log │
                                                            └───┬──────────┘
                                                                │ fold
                                                                ▼
                                                          Projection: files,
                                                          variables, location
```

### Stages

1. **Parse.** `CommandLineParser.Parse<RootNode>` returns either a tree or a
   `ParserError`. Errors carry a zero-based line and column and the symbols the grammar
   could have accepted. See [Grammar](lexical-grammar.md).
2. **Tokenise.** `TokenStreamVisitor` flattens the tree to `(text, kind)` pairs.
   `SerialisationVisitor` flattens it back to text; the host compares that with the
   input to prove the parse lost nothing. See [Semantic tree](semantic-tree.md).
3. **Bind.** `ArgumentBinder` matches written arguments to declared parameters, drawing
   on the piped value and on defaults. See
   [Execution model](execution-model.md#argument-binding).
4. **Evaluate.** `Evaluator` folds the pipeline, resolving each expression to a
   `Value`. Tags build values without calling a command. No stage changes anything: a
   command returns the events describing what it would change.
5. **Commit.** When every stage has succeeded, the events are appended to the log as
   one transaction, which is also when the projection moves. A line that failed
   commits nothing; a line that produced no events commits nothing either.
6. **Render.** The front end turns the value into its own presentation: entities and
   buttons in the desktop scene, chips and text blocks in the browser.

### Key design decisions

**Commands return values.** The alternative, writing to the console, was what the
original code did; it made `ls` produce buttons directly and left nothing to pipe. A
returned value can be piped, rendered differently per host, and asserted on in a test.

**Output is an interface, not a console.** Long-running commands need to say something
before they finish. `ICommandOutput` gives them a line to write and mutate. Each host
implements it: the desktop sink asks the render loop for a frame on every write, the
browser sink raises an event, the test sink records. Commands therefore never reference
a loop, a window or a graphics library, which is what allows them to run under
WebAssembly.

**Undo is a compensating transaction, not a replay.** A command does not know how to
reverse itself and is never asked to. Every event carries both sides of its change, so
inverting one needs nothing but the event; undoing a line appends its events inverted
and reversed, marked as compensating it. Redo compensates the compensation, so it is
the same mechanism seen from the other side rather than a second one.

**The parser is combinators, not tables.** The generated LALR parser left about a third
of the grammar unimplemented, reported every error at column zero when wrapped for
backtracking, and required an external tool and an embedded binary table. Combinators
put the grammar in source, in the same order as the original BNF, and make error
positions faithful. The old parser is retained only so the two can be compared.

### Data storage

There is no filesystem underneath this. There is a log, a blob store, and a fold.

**The log** is an append-only list of transactions, each one command line's worth of
change: a sequence number, a timestamp, the line as it was typed, the events, and
optionally the sequence number of the transaction it compensates. Nothing is ever
edited or removed. `reset` is the one exception and it empties the log rather than
amending it.

**Blobs** hold file content, addressed by the SHA-256 of the text. An event names
hashes rather than carrying text, so keeping every version of a file costs one hash
per write, and undoing a write is pointing at the old hash again. A command reaches
the blob store through a capability that can put and get and cannot append, undo or
read the history.

**The projection** is what the log adds up to: the files, the variables and the
location. It is not state that commands mutate — it is `fold apply empty events`, and
a fresh store replaying the same log **must** arrive at exactly the same projection.
That identity is what makes a browser reload a replay rather than a restore.

A file in the projection is a record: an id, a map of attributes and an optional
content hash. There is no directory type; a directory is a record whose `kind` is
`folder`, and the root is implicit. Because the hierarchy is one attribute among many,
a query over attributes is a location in the same sense a directory is
([The filesystem](../filesystem.md)).

Where the log lives is the host's business. The browser keeps it in IndexedDB for the
page's origin, in a hand-written versioned shape so that a later build can read an
earlier one's log; tests and the desktop shell keep it in memory. Nothing is sent
anywhere.

The log grows with use and nothing compacts it. Snapshotting the projection and
keeping only the log after it is the obvious answer, and is deliberately deferred
until a log exists that is big enough to notice.

### Degree of constraint

The following are fixed and an implementation **must not** vary them: the grammar, the
token kinds, the value kinds and their two string forms, the binding order, pipeline
semantics, the undo contract, and the wire formats in
[Host interfaces](host-interfaces.md).

The following are deliberately left open: how a host renders values, which commands are
registered, where the log is kept, how completion ranks its answers, whether a host
offers a live refresh at all, and how a host schedules the evaluator with respect to
its own input loop.

## Alternatives considered

**Keep the GOLD LALR parser and finish its visitor.** Rejected. The unimplemented
productions were a symptom: the table-driven design put the grammar in a binary
artefact produced by an external tool, so a change meant regenerating tables outside the
build. The error-position problem was structural rather than a bug.

**Pass text between commands, as a conventional shell does.** Rejected. It defeats the
premise. Every benefit here, from tapping a token to piping a list of paths with spaces
in them, follows from not flattening.

**Make commands write to the console and have the shell scrape it.** Rejected, for the
same reason, and because it makes testing a command require a console.

**Render the browser terminal on a canvas, mirroring the desktop shell.** Rejected. The
document object model gives scrolling, text selection, the soft keyboard, and accessible
text for free, and token spans keep the per-token interaction that the canvas
demonstrated.

**Persist the browser filesystem in local storage.** Deferred. It changes the privacy
story, needs a migration strategy for the seeded tree, and is not required to exercise
the execution layer.

**Run the evaluator on the server and stream results to the page.** Rejected for the
default deployment. It introduces a session per user, latency per keystroke for
colouring, and a data-protection question, in exchange for nothing the client cannot do.
The ASP.NET host remains available for tooling.

## Cross-cutting concerns

### Security and privacy

The browser terminal executes entirely in the page. No command text, result or file is
transmitted. The only network access is `download`, which the user initiates explicitly
and which the browser constrains by the same-origin policy and the remote host's CORS
headers.

The filesystem is the page's in-memory one, so a command cannot reach the device. The
desktop shell has the privileges of the user who ran it; commands there operate on real
files and the usual care applies.

`exit` reaches the host through `IApplicationLifetime`, so a host decides what shutting
down means rather than a command reaching for a window.

### Failure handling

A command failure is a `Fault` carrying a kind and a message for the user; the session
catches it and returns it as the response's error. Unexpected exceptions are caught at
the same boundary and reported with their type, so a defect surfaces as a message
rather than a dead terminal. A failed command leaves the session usable.

A fault is also a value the language can hold. `try` turns a stage's failure into its
result and `else` hands a failure to the pipeline that recovers, so a script can step
round a missing file without the line failing
([decision 0014](../decisions/0014-recovery-operator.md)). Both put back whatever the
failed part had done, which keeps a line atomic, and neither catches Stop
([decision 0024](../decisions/0024-stop-is-not-recoverable.md)).

### Observability

The parse response carries the token stream and the re-serialised text, so a host can
detect a lossy parse. The execution response carries the output lines, the result, any
error and the working directory, which is enough to reconstruct what happened without
inspecting internals.

### Accessibility

The browser terminal is text in the document, so screen readers, text zoom and text
selection work. Colour is never the only carrier of meaning: errors are also prefixed
by their message, results are structurally distinct from output, and the token inspector
names a role in words. The layout targets a 390 pixel wide viewport without horizontal
scrolling. The input and the run button are 46 pixels tall; the suggestion and
completion chips are 26, which is below the usual 44 pixel target and **should** be
raised.

### Internationalisation

Identifiers accept a defined set of accented letters. Numbers parse with the invariant
culture, so a decimal point means the same thing everywhere. Messages are English only,
and the client is published with invariant globalisation to keep the payload small;
localisation would require both decisions to be revisited.

### Performance

The payload is about 15 MB on a first visit and cached afterwards. Parsing runs on
every keystroke for colouring and completion, which is comfortably fast because the
input is one short line. Live output is coalesced to about one update every 40
milliseconds so that a fast-updating command does not spend more time crossing the
interoperability boundary than doing its work.
