# 0001. Keep a decision log

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-21 |

## Context

The project has made several choices that shape everything after them: which parser,
where execution happens, how the browser renders, and now a functional core, a table
model and a notation policy. Each came with alternatives that were weighed and set
aside. Without a record, those alternatives are re-argued every time someone new
reads the code, and the reasons for the chosen one are lost.

The owner asked for a record of every option considered and which one was chosen.

## Options

1. **Comments in the code where each decision bites.** Close to the code, but scattered,
   and there is nowhere to record the options that were rejected.
2. **A section in the design doc.** One place, but a design doc describes the current
   design; it does not hold history, and it grows unreadable as decisions accumulate.
3. **One record per decision in a numbered log.** Each record is short, immutable once
   accepted, and superseded rather than edited. The design doc links to it.

## Decision

Option 3. Records live in `docs/decisions/`, numbered in the order taken, with a fixed
shape: context, options, decision, consequences. Decisions taken before the log
existed are recorded retrospectively so the log is complete.

## Consequences

Every future language or architecture change starts with a record, in **Proposed**
status until the owner accepts it. The specification's conformance document points at
the record that justifies any deviation.
