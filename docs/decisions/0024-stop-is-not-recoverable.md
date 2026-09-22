# 0024. Stop is not a failure a line can recover from

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-22 |

## Context

Phase 5 gives the language two ways to carry on after a failure: `try` turns a stage's
fault into a value, and `else` runs another pipeline with the fault piped in. A fault of
kind `Cancelled` — "Stopped." — is a fault like any other as far as the railway is
concerned, so without a rule `try progress 100 | set p` would answer the Stop button by
binding `$p` and finishing the line, and `download x else echo gave up` would treat the
person pressing Stop as a download that failed.

## Options

1. **Recover from everything.** One rule, no exceptions. Stop becomes a suggestion on
   any line that happens to have a `try` or an `else` in it, and nothing on the screen
   says which lines those are.
2. **Recover from everything except `Cancelled`.** A cancelled stage stops the line
   whatever surrounds it, and the line commits nothing, the same as it always has.
3. **Let the user choose**, with a flag on `try`. A second thing to learn for a case
   nobody has asked for.

## Decision

Option 2. Stop is the person at the keyboard saying "no further", not a command
reporting that it could not do what it was asked, and the one sure way to honour it is
for nothing in the language to be able to catch it.

## Consequences

`try` and `else` pass a `Cancelled` fault straight through, and `$problem.kind` can
never read `Cancelled`. Every other kind — `Internal` included — is recoverable, so a
defect in a command is still something a script can step round while it is reported.
