# Implementation plan: functional core, event-sourced store, attribute filesystem, tables

**Status: Phases 1 to 11 complete.** Phases 1 to 11 are built, and each built phase ends
with an "As built" section recording where the result differs from what it planned.
[Phase 8](phase-8-intellisense.md) was added afterwards, from an audit of completion;
unlike the others, it was divided into streams that ran in parallel after one
foundation checkpoint, and it was the first phase run through
[running.md](running.md).

This plan is written for an implementing agent who has this repository and nothing
else. Read this file, then [running.md](running.md), then
[architecture.md](architecture.md), then the phase you are on. Everything the owner
has decided is in the [decision log](../decisions/README.md); this plan does not
reopen those decisions, it executes them.

**To run it,** type `/run-plan` in a Claude Code session on this repository, or ask
for the plan to be run. The session that receives it is the orchestrator:
[running.md](running.md) says how it finds where the plan stands, which work it does
itself, which it hands to sub-agents (one per parallel stream, each in its own git
worktree), how it briefs them, and how it merges and verifies what they return. That
use of sub-agents is the owner's standing instruction, and needs no further
permission.

| Document | Contents |
| --- | --- |
| [running.md](running.md) | How the plan is run: the orchestrator, sub-agents per stream, the brief, merging, verification, owner decisions, resuming. Applies to every phase. |
| [architecture.md](architecture.md) | The target: projects, types, store model, filesystem model, grammar changes, wire formats. Read fully before Phase 1. |
| [phase-1-functional-core.md](phase-1-functional-core.md) | F# core with Result, Option and Fault; event-sourced store as the filesystem; every command ported; parity with today plus undo, redo and history. |
| [phase-2-persistence.md](phase-2-persistence.md) | The log persists in the browser's IndexedDB; reload replays it. |
| [phase-3-tables-and-predicates.md](phase-3-tables-and-predicates.md) | The Table value, expression grammar with word operators, `$row`, table functions, table rendering. |
| [phase-4-views.md](phase-4-views.md) | Queries as views: `cd` a predicate, `find`, `pwd` as a query, live views. |
| [phase-5-error-syntax.md](phase-5-error-syntax.md) | `else`, `try`, `??`, nested pipelines in parentheses. |
| [phase-6-xml.md](phase-6-xml.md) | `from-xml`, `to-xml`, `from-csv`, `to-csv` over real files. |
| [phase-7-consolidation.md](phase-7-consolidation.md) | Specification rewrite, user documentation, conformance, browser check in CI. |
| [phase-8-intellisense.md](phase-8-intellisense.md) | Completion that reads the line: the command, parameter and value at the cursor; value stages; predicate faults; hints and hover on the page. Seven parallel streams after one foundation checkpoint. |
| [phase-9-guidance.md](phase-9-guidance.md) | The owner's requests from using it on a phone: `in`, `out`, `back` and `read`; help on a wrong call; the pipe first; live listings, copying and history buttons on the page. |
| [phase-10-help-towards-understanding.md](phase-10-help-towards-understanding.md) | After Phase 9: did-you-mean for files and columns, empty answers that explain themselves, fixes as chips, and guidance drawn apart from output. |
| [phase-11-reading-trees.md](phase-11-reading-trees.md) | After Phase 10: a tag's name and children with `@`, and `pick`, CSS selectors over nested tags and XML. |
| [examples.md](examples.md) | Four one-screen programs, one per pillar, with golden results. The proof that the whole works as imagined. |
| [scene-editor-direction.md](scene-editor-direction.md) | Not a phase. A proposed direction, pending [0029](../decisions/0029-scene-editor-direction.md): the ECS as a scene the command line edits, rendered on a canvas beside a DOM terminal. |

## What is being built, in one paragraph

The execution layer moves to F#. A command is a function from an invocation to a
result and a list of events; failure is a value, never an exception. The events go
into an append-only log, and the filesystem, the variables and the current location
are projections folded from that log, so undo appends a compensating transaction, redo
compensates the compensation, and a browser reload replays the log from IndexedDB.
The filesystem holds attribute records rather than a directory tree: every file is a
set of typed attributes plus optional content, `folder` is one attribute among them,
and a query over attributes is a first-class value that can be listed like a folder.
Tables are a value; the tag notation coerces to a table when it is table-shaped; XML
documents read and write through the same tree. Predicates use word operators and an
explicit `$row`, recovery is spelled `else`.

## Principles the implementation must hold to

1. **Failure is a value.** No `ConsoleError`, no exception crosses a module boundary.
   Exceptions are for programmer bugs and become a fault of kind `Internal` at the
   session boundary.
2. **Commands describe, the store applies.** A command never mutates anything. It
   reads the current projection, returns a value and events. The evaluator commits.
3. **A line is atomic.** One transaction per command line, committed only when the
   whole line succeeds. A failed stage leaves no trace.
4. **F# owns semantics, C# owns hosting.** Values, faults, store, evaluator, commands,
   session: F#. Blazor bridge, ASP.NET host, WPF shell, DTO mapping: C#.
5. **The desktop shell keeps compiling.** It may degrade to text rendering. The Windows
   CI job is the check; nothing in this plan requires a Windows machine to develop.
6. **Every phase ships.** After each phase: all tests green, CI green, the browser
   client republished and verified at 390 by 844, documentation and specification
   updated in the same commit series.
7. **Every new decision gets a record.** Anything not already in the decision log that
   changes the language or the architecture is written up as a numbered record before
   it is built. The phases below name the records they are expected to add.
8. **The example programs are the proof.** [Four small programs](examples.md) with
   golden results are written before the code. Phases 3 to 6 each complete one; a
   phase is not done while its program's results differ. The programs are never
   edited to fit the implementation without a decision record.

## Working conventions

- Branch: the owner's working branch is `claude/project-overview-38505w`. Commit on
  it; push with `git push -u origin <branch>`. Never open a pull request unless asked.
- Commit as `Claude <noreply@anthropic.com>` with the attribution lines the session
  provides. One commit per checkpoint named in a phase; a commit message explains
  why, not just what.
- Build with the .NET 10 SDK (`global.json`). Tests: every `*.Tests.csproj` and, from
  Phase 1, every `*.Tests.fsproj`. Update the CI globs in
  `.github/workflows/build.yml` when the first F# test project appears.
- Documentation lives in `docs/` (user) and `docs/spec/` (normative). Every phase
  touches both. Examples in user docs are pasted from real output, never typed from
  memory: run the command, copy the result.
- The browser client is published and verified with the procedure in
  [docs/building.md](../building.md) and the script in `tools/browser-check.mjs`
  (added in Phase 1). GitHub Pages republishes on every push; the Artifact is
  republished with `tools/prepare-artifact.sh` and the Artifact tool, as
  [building.md](../building.md#republishing-the-artifact) describes.
- Do not touch the GOLD parser or its `.grm` files except to add comments. The
  equivalence tests compare only inputs both parsers accept; when the grammar grows,
  add new cases to the FParsec-only test classes.

## Sequencing and checkpoints

Phases are sequential. Inside a phase, checkpoints are buildable states; commit at
each. Do not start a phase until the previous phase's acceptance list is fully green.

```
Phase 1  ──▶  Phase 2  ──▶  Phase 3  ──▶  Phase 4  ──▶  Phase 5  ──▶  Phase 6  ──▶  Phase 7  ──▶  Phase 8
core+store    persistence   tables       views         else/try     xml/csv      consolidate  intellisense
```

Inside Phase 8 the checkpoints are not all sequential: after its foundation checkpoint,
seven streams run in parallel worktrees and merge back in a stated order. From Phase 8
on, every phase document carries a work breakdown (serial checkpoints, parallel
streams, file ownership, merge order and a Progress table), and
[running.md](running.md) says how it is carried out.

Phase 1 is the largest and the least divisible: replacing the execution layer and the
filesystem model at once avoids porting per-command undo only to delete it. Its
checkpoints are designed so the build is green at each.

## Verification, every phase

```bash
# build everything that builds on Linux
for p in $(find . \( -name '*.csproj' -o -name '*.fsproj' \) -not -name 'Application.csproj' -not -path '*/bin/*' -not -path '*/obj/*' | sort); do
  dotnet build "$p" -c Release || exit 1
done
# every test project, both languages
for p in $(find . \( -name '*.Tests.csproj' -o -name '*.Tests.fsproj' \) -not -path '*/bin/*' | sort); do
  dotnet test "$p" -c Release || exit 1
done
# the browser client, then the check script against a static server
dotnet publish WebClient/WebClient.csproj -c Release -o publish
node tools/browser-check.mjs publish/wwwroot
```

The check script boots the page in Chromium at phone size, runs a scripted session,
and fails on any console error or any mismatch against expected output. Each phase
adds its acceptance lines to that script, and from Phase 3 it runs the example
programs with `run examples/<name>.clr`.

## Risk register

| Risk | Mitigation |
| --- | --- |
| F# types are awkward from C# | C# never sees `Value` or `Result`. The `Web.Core` adapter maps `Session.Response` to DTOs; the desktop adapter renders display strings. |
| Async in WebAssembly is single-threaded | Use `Async` in F# and expose `Task` at the boundary with `Async.StartAsTask`. Never block. The IndexedDB log is `async` end to end. |
| The desktop shell breaks and cannot be built locally | Keep its adapter mechanical: it calls `Session.Execute` and prints strings. Push, read the Windows job log, fix, push. Budget for two rounds. |
| The grammar change to `/>` breaks equivalence tests | Equivalence cases cover only inputs both parsers accept. New bare-word-in-attribute cases go in `BareWordTests`. |
| Payload growth past the 20 MB CI guard | The core replaces C# with F#, not in addition. Measure after Phase 1; `System.Xml.Linq` is already shipped. |
| Replay time as the log grows | Logs in a tab are small. Snapshots are a Phase 7 option, not a Phase 2 requirement. |
| MSTest in F# | Works with `[<TestClass>]`/`[<TestMethod>]` on a class with a default constructor. The CI glob must include `*.Tests.fsproj`. |
