# 0018. The seeded filesystem is recorded but cannot be undone

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-21 |

## Context

A new session starts with three files to look at, because an empty filesystem makes
`ls` look broken rather than empty. Under [0010](0010-undo-by-event-sourcing.md) the
only way anything gets into the store is as a transaction, so the seed is one.

That made `undo`, typed as the first thing in a fresh session, answer `Undone: seed`
and empty the filesystem. Nothing was wrong with the mechanism — it did exactly what
the record says — but nobody asked for it, and a user who does it has no way of
knowing what just happened beyond a word they have never seen.

## Options

1. **Leave it.** The seed is a transaction like any other and `redo` puts it back. It
   is consistent, and it surprises the one user most likely to press undo out of
   curiosity: a new one.
2. **Do not record the seed.** Apply its events to the projection without appending a
   transaction. Then a replay of the log does not reproduce it, so the store stops
   being a pure fold and [0010](0010-undo-by-event-sourcing.md)'s central property is
   gone.
3. **Record it and mark it.** The seed is appended, replayed and listed in `history`
   like anything else, and carries a flag saying it was not a line anyone typed. Undo
   skips it.

## Decision

Option 3. `Transaction` gains `Undoable`, which is true for every line a user runs and
false for the seed. Undo and redo skip what is not undoable; `history` shows it,
because what the session started with is part of what happened.

The flag says why rather than what — "this is not yours to take back" — so a later
source of transactions that is not a typed line, such as a migration, has somewhere to
say so.

## Consequences

Undo on a fresh session answers `Nothing to undo.`, which is true. One more field in
the transaction, which Phase 2 persists with the rest. Emptying the seeded files is
`rm`, which is what it should have been.
