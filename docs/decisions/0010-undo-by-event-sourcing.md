# 0010. Undo and persistence through an event-sourced store

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-21 |

## Context

Undo is per command: each command keeps what it needs to reverse itself in its own
fields and implements an undo method. That produced a defect where a shared instance
replayed the wrong state, and it means every new command reimplements undo. The
functional core ([0006](0006-functional-core-in-fsharp.md)) wants commands that
describe what they did rather than mutate and remember. The browser filesystem is lost
on reload.

## Options

1. **Per-command undo methods**, as now. Every command carries its own reversal logic
   and state.
2. **An effect log.** Commands return the effects they performed; the runtime applies
   them, records inverses, and undo replays inverses. State still lives in a mutable
   filesystem.
3. **An event-sourced store.** The store is an append-only log of events; the
   filesystem, variables and history are projections folded from it. Undo appends
   compensating events, so history stays append-only and redo is possible. Snapshots
   keep replay fast. In the browser the log persists in IndexedDB, so a reload replays
   it and nothing is lost. File contents are content-addressed blobs referenced by
   hash, so a write's before-image costs nothing to keep.

## Decision

Option 3, proposed by the owner and endorsed. It subsumes option 2: the effects a
command returns are the events appended. Purpose-built and small, not a general
database: typed events, two or three projections, one log.

## Consequences

Commands become pure functions from an invocation to a result and a list of events.
Undo, redo, a `history` command and a point-in-time view fall out of the log.
Persistence in the tab arrives without uploading anything. The desktop shell would
need a projection onto the real disk, which [0012](0012-browser-first.md) defers.
