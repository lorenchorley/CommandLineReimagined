# Phase 1: functional core and event-sourced store

**Goal.** Replace the C# execution layer with an F# core in which failure is a value,
commands return events, and the filesystem is an attribute-record projection of an
append-only log. At the end, the browser terminal does everything it does today, plus
`undo`, `redo` and `history` as real commands, and attribute records under the hood.

**Decisions executed.** [0006](../decisions/0006-functional-core-in-fsharp.md),
[0007](../decisions/0007-notation-conflicts.md) (part B),
[0010](../decisions/0010-undo-by-event-sourcing.md),
[0013](../decisions/0013-attribute-filesystem.md) (folders as attribute, no queries yet).

**Records to add first.** 0015 atomic lines, 0016 folders as records, 0017 `name=value`
is data. Write them, mark Accepted (the owner accepted the underlying direction), then
build.

## Checkpoint 1.1: grammar, part B

Files: `Parser.FParsec/Grammar.fs`, `Parser.Tests/BareWordTests.cs`,
`Parser.Tests/LexicalTests.cs`, `Parser.Tree/SemanticTree/` (one new node),
`Web.Core/TokenStreamVisitor.cs`, `Parser.Tree/Serialisation/SerialisationVisitor.cs`.

1. Words end before `/>` and `/}`: in `bareWordText`, a `/` is a word character only
   when the character after it is not `>` or `}`. Implement with `many1Satisfy2L` over
   a lookahead-aware predicate, or with a custom parser that consumes `/` conditionally.
2. Tag attribute values accept `argumentSimpleValue` (bare words) instead of
   `simpleValue`.
3. A `-` immediately followed by a digit begins a word, not a flag. `flagText` fails
   in that case; `bareWordText` accepts a leading `-` only before a digit.
4. `<` opens a tag only when followed by an identifier character, `$` or `/`. Encode
   this in `objectHeader` and `variableTag` with `followedBy`.
5. New node `AssignmentArgument { Name: Identifier; Value: Value }` in `Parser.Tree`,
   produced by `Identifier "=" ArgumentValue` with no whitespace, in `commandArgument`
   before `argumentValue`. Serialise as `name=value`. Tokenise the name as `attribute`
   and `=` as punctuation.

Tests to add (`BareWordTests`): `<file path=documents/notes.txt/>` parses with the
attribute `documents/notes.txt`; `<t path=a/>` parses with attribute `a` and closes;
`echo -5` is a single number word; `echo -x` is still a flag;
`attr notes.txt tag=work due=2026-10-01` yields one value and two assignments;
`< thing` (space after the bracket) is a syntax error, since `<` opens a tag only
when followed by a name; round trips for each. Giving `<` its second meaning as
less-than is Phase 3 and is not tested here.

Update `docs/spec/lexical-grammar.md` and `docs/language.md` ("Tags: attributes may be
bare words"; remove the quoting caveat).

Commit: "Grammar: lookahead delimiters, bare words in attributes, negative numbers,
assignments".

## Checkpoint 1.2: Core project, values, faults, railway

Create `Core/Core.fsproj` (net10.0, references `Parser.Tree`, `Parser.FParsec`; FSharp
compile order matters, list files explicitly). Add to the solution. Add to
`Directory.Packages.props` nothing new; `FSharp.Core` is implicit.

Files, in compile order:

| File | Contents |
| --- | --- |
| `Values.fs` | `Value`, `Tag`, `FileRef`, `Value.display`, `Value.argument`, `Value.kind`, number formatting `0.###` invariant |
| `Faults.fs` | `FaultKind`, `Fault`, `Outcome`, `Outcome` module, `outcome` builder, `Fault.create kind message`, constructors for every message in `docs/errors.md` |
| `Expr.fs` | `Expr` type with only `Const` and `Column` cases for now (Phase 3 fills it); needed because `Location.View` references it |
| `Events.fs` | `FileId`, `Hash`, `FileRecord`, `Location`, `Event`, `Transaction`, `ILog`, `InMemoryLog`, `Hash.ofText` (SHA-256 via `System.Security.Cryptography`) |
| `Projection.fs` | `Projection`, `apply`, `invert`, `Files` module (`resolve`, `resolveFolder`, `inFolder`, `inferKind`, `pathOf`, path normalisation with `.` and `..`) |
| `Store.fs` | `Store` class: `Initialize`, `Current`, `Commit` with validation, `Undo`, `Redo`, `History`, `Changed` |
| `Scope.fs` | `Scope` with parent chain: `tryFind`, `bind`, `unbind`, `all`; the variables projection is the global scope's backing map |

Tests (`Core.Tests/Core.Tests.fsproj`, MSTest 4, references `Core`; add
`*.Tests.fsproj` to the CI loops now):

- `ValueTests`: display and argument strings for every case; number formatting.
- `ProjectionTests`: every event applies and inverts to the starting projection;
  `apply (apply p e) (invert e) = p` for generated events.
- `StoreTests`: commit then undo restores `Current`; undo, redo, undo; undo with an
  empty log returns `Ok None`; `Commit` of a duplicate name is `Conflict`; `History`
  marks compensated transactions; replaying a log into a fresh store yields an equal
  projection (determinism); blobs de-duplicate by hash.
- `FilesTests`: resolution of absolute, relative, `.`, `..`, root; kind inference.

Commit: "Core: values, faults, railway, events, projection, store".

## Checkpoint 1.3: commands, binder, evaluator, session

Files:

| File | Contents |
| --- | --- |
| `Commands/Spec.fs` | `ParamKind`, `Parameter`, `CommandSpec`, `IOutput` family, `Invocation`, `CommandResult`, `Command`, helpers `param`, `optional`, `piped` |
| `Commands/Files.fs` | `ls`, `cd`, `up`, `pwd`, `mkdir`, `cp`, `cat`, `write`, `rm`, `attr`, `save` |
| `Commands/Values.fs` | `echo`, `set`, `vars` |
| `Commands/Async.fs` | `progress`, `download` (uses `System.Net.Http.HttpClient` passed in) |
| `Commands/Meta.fs` | `undo`, `redo`, `history`, `exit`, `unknown` |
| `Binder.fs` | The binding algorithm from the specification plus `Assignments`; `evaluate: Scope -> Parser value -> Outcome<Value>` including tags |
| `Evaluator.fs` | Pipeline fold, working projection, atomic commit, meta commands outside the transaction, cancellation between stages |
| `Completion.fs` | Port of `TerminalSession.Complete`, over the projection: commands, `$` variables, names in the current folder |
| `Session.fs` | `Session`, `Response`, seeding, `OutputChanged`, `StoreChanged` |

Command contracts, exactly as the [command catalogue](../spec/command-catalogue.md)
states them, re-expressed over records:

- `ls [path]` returns `List` of `File`; parent entry first when not at the root, then
  folders, then files, each group ordered by name (ordinal). This ordering is now
  normative; the old "follows the filesystem" wording goes.
- `cd path` emits `LocationChanged`; `up` likewise; `pwd` returns `Text` of the folder
  path.
- `mkdir name` emits `FileCreated` of a folder record; `Conflict` if the name exists.
- `write path text` emits `FileCreated` + `ContentChanged` for a new file, or
  `ContentChanged` (+ `AttributesChanged` for `modified`) for an existing one. Content
  goes to the log's blob store first; the event carries hashes.
- `cat path` returns `Text` of the blob.
- `rm path` emits `FileDeleted`; folders must be empty; the current folder cannot be
  deleted.
- `cp source folder` emits `FileCreated` with the same content hash.
- `attr path [name=value ...]` with no assignments returns `List` of `Text`
  `"name = value"` (a table in Phase 3); with assignments emits `AttributesChanged`.
  Reserved names (`name`, `kind`, `folder`, `created`, `modified`) are `Invalid`
  except `kind` and `name`, which are allowed and validated.
- `save <tag/>` creates a file from an object tag: type name is `kind`, the `name`
  attribute is required, other attributes are copied. `Conflict` on an existing name.
- `set`, `vars`, `echo` as today; `set` emits `VariableChanged`.
- `undo`, `redo` are meta and return `Text "Undone: <source>"` or
  `Text "Nothing to undo."` as a fault of kind `Invalid`? No: `Nothing to undo.` is a
  plain `Text` result, not a fault, because nothing went wrong.
- `history` returns `List` of `Text` lines `"<seq>  <time>  <source>"` with a
  trailing `(undone)` where applicable.
- `download [url] [into]` as today; `progress [steps] [delay]` as today.
- `exit` calls an `IApplicationLifetime`-equivalent `unit -> unit` passed to the
  session.

Tests: port every case in `Execution.Tests/ExecutionTests.cs`,
`FileAndVariableCommandTests.cs` and `AsyncCommandTests.cs` into `Core.Tests`
(`ExecutionTests.fs`, `FileCommandTests.fs`, `VariableCommandTests.fs`,
`AsyncCommandTests.fs`), asserting on `Outcome` and on the projection rather than on
the disk. Add: `mkdir a | cd nowhere` leaves no folder (atomic line); `undo` after
`write` restores the previous content; `history` shows the undone line; `attr` and
`save` behaviours; `Meta` commands do not appear in `History`.

Commit: "Core: commands, binder, evaluator, session".

## Checkpoint 1.4: adapters

**`Web.Core`** (C#): `TerminalSession` becomes a thin wrapper over `Core.Session`.
Keep `ExecutionResponse`, `ResultItem`, `CommandSummary`, `Completion`,
`VariableSummary`; add `FaultInfo` and `location`; keep `workingDirectory` as an alias
for this phase. Map `Value` to items with a `Describe` that handles every case in the
[wire format](architecture.md#wire-formats-c-adapter-in-webcore). `Undo()` is removed
from the adapter; the page's `undo` word is removed and `undo` reaches the evaluator
like any command. `Initialize()` added; `Execute` before `Initialize` is a fault.

**`WebClient`**: `TerminalBridge.Initialize` (async, awaited by the page before the
input is enabled), `Undo` removed. `Program.cs` unchanged otherwise. Page: remove the
`undo` special word and its bridge call; render `fault.kind` as a small tag before the
message; chips to 44 pixels tall (`padding: 12px 12px`); `history` output renders as
output lines.

**Desktop** (`CommandLine`, `CommandLineReimagined`): delete `CommandLine/Execution/`,
`CommandLine/Commands/` and the `Commands` project. The desktop-only `debug` command
is dropped in this phase, because an F# `Command` record is awkward to construct from
C#; say so in the commit message. Phase 7 brings it back if the owner wants it. `Shell.ExecuteAsync` calls
`Session.Execute(commandText, id, token)` and renders `Response.Output` lines and
`Value.display` of the result, or the fault message, as text into the block through
`CliBlock`, which now implements `Core.IOutput`. `KeyInputHandler` undo calls
`Session.Execute("undo", ...)`. `CommandSearch`, `CommandRegistry` and `Prompt` take
`IReadOnlyList<CommandSpec>` instead of `IEnumerable<ICommandAction>`; keywords come
from `CommandSpec.Keywords`. `ServiceExtensions` registers one `Session` over an
`InMemoryLog` seeded like the browser. Delete `Execution.Tests` once `Core.Tests` is
green and covers its list.

CI: `build.yml` loops include `*.fsproj` and `*.Tests.fsproj`; the Windows job builds
the solution, so the solution file must list `Core` and `Core.Tests` and no longer
list `Commands` or `Execution.Tests`.

Commit: "Hosts on the core: browser, desktop, CI".

## Checkpoint 1.5: browser check script and documentation

Add `tools/browser-check.mjs`: Playwright, Chromium at 390 by 844, `deviceScaleFactor`
3, serves the given folder on a local port (spawn `python3 -m http.server`), waits for
the `wasm` status, runs a table of `[command, expected]` lines and compares the
rendered entry (chips, text blocks, output lines, error line), fails on console
errors. Document `PLAYWRIGHT_CHROMIUM` for the executable path. Seed it with the
acceptance lines below.

Documentation in the same series: `docs/concepts.md` (events and projections replace
the undo section; atomic lines), `docs/commands.md` (`attr`, `save`, `undo`, `redo`,
`history`; `ls` ordering), `docs/errors.md` (fault kinds column), `docs/web-terminal.md`
(`undo` is a command; restoring status), `docs/spec/execution-model.md` (values with
`None` and `Fault`; events; the store; atomic lines), `docs/spec/command-catalogue.md`,
`docs/spec/host-interfaces.md` (`Initialize`, `fault`, `location`),
`docs/spec/conformance.md` (test class mapping to `Core.Tests`), `docs/building.md`
(F# test projects, the check script).

Commit: "Phase 1 documentation and browser check".

## Acceptance

All of these in `Core.Tests` and in `tools/browser-check.mjs`, from a fresh session:

```
ls                                  -> up documents projects readme.txt
mkdir a | cd nowhere                -> Directory does not exist : nowhere    ; ls shows no a
mkdir alpha                         -> alpha
undo                                -> Undone: mkdir alpha
redo                                -> Redone: mkdir alpha
history                             -> two lines, the first marked (undone) is absent after redo
write note.txt first                -> note.txt
write note.txt second               -> note.txt
undo                                -> Undone: write note.txt second
cat note.txt                        -> first
attr note.txt tag=work              -> note.txt
attr note.txt                       -> name = note.txt / kind = text / folder = / / tag = work / created = ... / modified = ...
save <note name=todo due=2026-10-01/>  -> todo
cat todo                            -> (empty text block)
<file path=documents/notes.txt/>    -> <file path=documents/notes.txt/>
echo -5                             -> -5
set v 1 ; set v 2 ; undo ; undo     -> $v unbound
progress 3 1                        -> 100
```

Plus: every project builds, all test projects pass, Windows CI green, payload under
20 MB, no console errors in the browser check.
