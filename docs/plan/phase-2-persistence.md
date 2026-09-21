# Phase 2: the log persists in the browser

**Goal.** A reload of the tab replays the log from IndexedDB, so files, variables and
the current folder come back. Nothing leaves the tab. Decision
[0010](../decisions/0010-undo-by-event-sourcing.md).

## Steps

1. **JavaScript log module** in `WebClient/wwwroot/index.html` (or a separate
   `store.js` published alongside): a small IndexedDB wrapper with one object store
   `transactions` keyed by `seq` and one `blobs` keyed by `hash`. Functions:
   `appendTransaction(json)`, `readAllTransactions() -> json[]`, `putBlob(hash, text)`,
   `getBlob(hash) -> text | null`, `clear()`. Every function returns a Promise. Wrap
   every IndexedDB call in try/catch: private windows and cleared site data must
   degrade to an in-memory log with a visible status `not persisted`, never to a
   broken page.
2. **`IndexedDbLog`** in `WebClient` (C#), implementing `Core.ILog` through
   `IJSRuntime`. Serialise `Transaction` and `Event` with a hand-written mapper to a
   stable JSON shape (do not serialise F# unions directly; the on-disk format is a
   contract). Version the shape: `{ "v": 1, ... }`. Add a reader for version 1 and a
   test that a stored sample decodes.
3. **`TerminalBridge.Initialize`** constructs the session with `IndexedDbLog`, awaits
   `Session.Initialize`, and reports how many transactions were replayed. The page
   shows `restoring…` until then.
4. **Seeding** happens only when the replayed log is empty. The seed is one
   transaction with `Source = "seed"` so it shows in `history` and can be undone like
   anything else.
5. **`reset` command** (meta): clears the log and blobs and re-seeds, after which the
   page reloads the projection. This is the escape hatch for a corrupted store. It is
   not undoable; say so in its description.
6. **Compaction is out of scope.** Note in `docs/concepts.md` that the log grows with
   use and that a snapshot mechanism is a later option.

## Tests

- `Core.Tests`: `Session.Initialize` over a log with recorded transactions reproduces
  the projection; seeding does not run on a non-empty log; `reset` empties then seeds.
- `Web.Core.Tests`: the JSON mapper round-trips every `Event` case and a `Transaction`
  with `Compensates`.
- Browser check: run `mkdir persisted`, reload the page, `ls` shows `persisted`; run
  `reset`, `ls` shows only the seed.

## Documentation

`docs/getting-started.md` and `docs/web-terminal.md`: the filesystem now survives a
reload and lives in the browser's IndexedDB for this origin; clearing site data
removes it; `reset` starts over. `docs/troubleshooting.md`: replace "My files
disappeared" with the new behaviour and the `not persisted` status.
`docs/spec/host-interfaces.md`: the `ILog` contract and the on-disk JSON shape,
versioned.

## Acceptance

Reload keeps state; `reset` clears it; a private window shows `not persisted` and still
works; all suites green; payload under 20 MB.
