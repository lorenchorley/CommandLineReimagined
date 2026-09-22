# Phase 7: consolidation

**Goal.** The documentation and the specification describe the system as it now is,
the conformance suite maps to it, the browser check runs in CI, and everything
temporary is gone.

## Steps

1. **Specification rewrite.** `docs/spec/design-doc.md`: context, goals, the design and
   the alternatives updated for the F# core, the event-sourced store and the attribute
   filesystem; "Data storage" describes the log, blobs and projections; cross-cutting
   concerns cover persistence and privacy of IndexedDB. `execution-model.md`,
   `command-catalogue.md`, `host-interfaces.md`, `lexical-grammar.md`,
   `semantic-tree.md` each re-read against the code. `conformance.md` maps every
   requirement to a class in `Core.Tests`, `Parser.Tests` or `Web.Core.Tests`, with
   counts regenerated from the suites.
2. **User documentation.** Every example re-run and re-pasted. `docs/README.md` index
   updated with `tables.md` and `filesystem.md`. `docs/concepts.md` rewritten around
   parse, bind, evaluate, commit, project. `docs/examples.md` gains a "Programs"
   section reproducing the four example programs with their real output, and the
   getting-started guide points a new user at `run examples/tables.clr` first.
3. **Decision log.** Every record added during the phases reviewed; statuses correct;
   the log's index complete.
4. **Browser check in CI.** A fourth job `browser` on `ubuntu-latest`: sets up Node,
   installs Playwright with Chromium, publishes the client, runs
   `tools/browser-check.mjs`, which runs all four example programs. Fails the workflow
   on any mismatch or console error.
5. **Cleanup.** Remove `workingDirectory` from the wire format; remove any
   compatibility shims marked `Phase 1`; delete `Execution.Tests` and `Commands` if
   still present; confirm the GOLD equivalence tests still pass and their corpus note
   is accurate.
6. **Optional, if time allows.** Snapshots in the log (`Snapshot of Projection` every
   N transactions, replay from the last snapshot); a file-backed `ILog` for the desktop
   shell; `debug` back on the desktop.
7. **Republish** the browser client and hand the owner the payload size, the test
   counts and the list of anything left open.

## Definition of done for the whole plan

- Every decision record from 0006 on is Accepted and implemented, or Superseded with a
  reason.
- `dotnet build` of every non-Windows project produces no warnings.
- Every test project passes on Linux; the Windows job passes.
- The browser check passes in CI and locally.
- The payload is under 20 MB.
- All four example programs run to completion in the hosted build and match their
  golden results.
- The specification's conformance document names a test for every normative area and
  lists no deviation that is not deliberate.

## As built

Every step but the optional one, with these differences and findings, recorded so the
state of the project at the end of the plan is what this says.

- **The browser check already ran in CI**, as the last step of the `wasm` job, from
  Phase 1. It is now the fourth job, `browser`, and takes the site the `wasm` job
  published and measured as an artifact rather than publishing a second time. It runs
  every phase's acceptance lines, the four example programs against their golden
  results, and two reloads.
- **Re-reading the specification against the code found the code wrong** in about
  twenty places, most of them against a decision record. The definition of done forbids
  a deviation that is not deliberate, so they were fixed rather than documented, each
  with a test:
  - `attr` renaming a folder left its contents behind (0016); it now carries every
    descendant, and the location if you are inside, in one undoable line. A folder
    cannot change its kind, a file with content cannot become a folder, and `size`
    cannot be written (0013).
  - Names were checked on creation only: `attr x name=a/b` and `save <n name=a/b/>`
    made records no path could reach. One name rule now holds for creation and change,
    in the store and in each command.
  - `sort name asc` sorted descending, because any value of a switch counted as set;
    switches now take their own word or nothing. `up` was marked read-only and moved
    you. `cp` of a folder made an empty copy; it is refused. `cd` entered a view file
    that held no predicate. `progress 2 -1` hung and `progress 2 1.5` blamed `steps`.
    `download` over a file did not touch `modified`.
  - `history` could not tell an undo or a redo from the line it reversed; it has a
    `compensates` column.
  - A parenthesised stage reported its inner stage number; a tag written with an
    attribute twice displayed it twice.
  - The parser gave `""""` the value `""` and `"""a"""` the value `"a"`, because the
    tree node re-counted the quotes; it now takes the delimiter the grammar matched.
    `<t>[p][/p]</t>` threw when serialised. `<t a=eq/>` lost its explanation and
    reported column 0. The body of `" "` was tokenised as whitespace.
  - The page added a space after every folder completion (the core calls the kind
    `folder` and the page waited for `directory`), inserted a tapped path with a space
    in it as two arguments, never updated `not persisted` after load, and offered
    `echo(a, b)` in its banner, which fails. Its `escape` did not escape `"`, which a
    file name with a quote in it would already have broken.
- **Two records**, because the review found a record and the code disagreeing in a way
  that is a language question: [0027](../decisions/0027-save-takes-a-tag.md) keeps
  `save` tag-only, withdrawing a sentence of 0017's consequences, and
  [0028](../decisions/0028-mixed-columns.md) keeps the `mixed` column type the Phase 3
  plan introduced over 0009's "otherwise the column is text".
- **Records are not edited, so three slips stand in them**: 0014's example
  `$maybe ?? "default"` is not a line (a stage has to be a command, so it is
  `echo $maybe ?? "default"`); 0019 says the parser rejects "eleven" words and lists the
  thirteen it does; 0015's list of meta commands predates `reset`, `run` and the
  internal `UnknownCommand`. The specification states what is true in each case.
- **Warnings**: 213 in the desktop libraries and their tests, all fixed with
  annotations, `new` on members that hid inherited ones, and the removal of fields
  nothing read. No warning is suppressed. `MouseInputHandler`'s click handlers hide the
  virtual methods rather than overriding them, so the desktop's right-click menu has
  probably never run; the fix keeps that dispatch, and changing it is the owner's call.
- **`tools/transcript.fsx`** runs documentation transcripts against the core with the
  clock pinned, which is how every example in `docs/` was re-run and re-pasted.
- **`workingDirectory`** is gone from the wire format and a test pins its absence. There
  were no other shims marked for removal, and `Execution.Tests` and `Commands` were
  already deleted.
- **Not taken**: the optional step. Snapshots are not needed at the sizes a tab's log
  reaches, and the desktop's file-backed log and `debug` are desktop work that decision
  0012 leaves out of scope.

Left open, none of it blocking: the suggestion and completion chips are about 26
pixels tall, below the 44 pixel target; `download` reports a small file as `0.00 MB`;
whether `cd` and `ls` should name a missing folder by its resolved path like every
other command (they name it as written, deliberately, and a test pins it); and the
desktop right-click dispatch above.
