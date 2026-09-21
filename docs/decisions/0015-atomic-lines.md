# 0015. A command line is one atomic transaction

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-21 |

## Context

With commands returning events rather than mutating
([0010](0010-undo-by-event-sourcing.md)), something has to decide when those events are
appended to the log. A pipeline has several stages, each producing events, and a stage
can fail after an earlier one has already described a change. `mkdir a | cd nowhere`
is the case that forces the question: the `mkdir` succeeded, the `cd` did not.

Today each command mutates as it runs, so the half-finished state survives the failure
and the folder `a` is left behind. Undo then has to be invoked once per command to get
back, and the user has to know how many commands ran before the failure.

## Options

1. **A transaction per stage.** Each command commits its own events as it finishes.
   Simple to implement; a failed line leaves debris, and undo walks back stage by
   stage, which means the user must count stages.
2. **A transaction per line.** The evaluator accumulates events across the stages in a
   working projection, so a later stage sees an earlier one's effect, and commits once
   when the whole line succeeds. A fault commits nothing.
3. **An explicit transaction command.** `begin` and `commit` as words in the language.
   Maximum control, and a burden on every user for a case that is rare.

## Decision

Option 2. One line is one transaction, named by its source text. A stage that faults
discards the whole line's events, so the store never holds a partial line. A line
whose stages produce no events commits nothing at all, so a read-only line such as
`ls` leaves no transaction and `undo` reaches past it to the last line that changed
something.

Meta commands — `undo`, `redo`, `history`, `help`, `exit` — run outside the
transaction, because their business is the log itself and a transaction that recorded
an undo would have to be undone in turn.

## Consequences

`undo` is per line rather than per command, which is what a user typing a line
expects. A stage sees the working projection, not the committed one, so
`mkdir a | cd a` works within one line. The evaluator, not the command, owns the
store. A failed line is invisible in `history`, so history is a list of lines that
actually changed something.
