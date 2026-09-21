# 0012. The desktop shell stays out of scope

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-21 |

## Context

The Windows desktop shell shares the execution layer. Tables, the event store and the
attribute filesystem each need presentation or projection work there.

## Options

1. **Keep both hosts at parity.** Every feature is built twice.
2. **Browser first.** The desktop shell keeps compiling, on the Windows CI job, and
   receives no new presentation until asked for.

## Decision

Option 2, continuing the owner's earlier instruction to focus on the web front end.

## Consequences

The desktop shell may render new value kinds as text until it is revisited. Its build
is still protected by CI so the shared layer cannot break it silently.
