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
