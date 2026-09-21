# 0006. The execution layer moves to F#, with Result and Option as the error model

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-21 |

## Context

The owner wants a functional paradigm and native, first-class error handling without
exceptions. Today a command reports failure by throwing `ConsoleError`, which the
session catches at its boundary and turns into a message. Undo state lives in command
instance fields. The parser is already F#.

## Options

1. **Stay in C#, keep exceptions, tidy the boundary.** Least work; nothing changes for
   the user. Errors remain control flow, not values.
2. **C# with hand-rolled Result and Option types.** Commands return `Result<Value>`;
   the evaluator binds. Every helper the railway needs is written by hand and reads
   awkwardly in C#.
3. **Move the execution layer to F#.** Result, Option and railway composition are
   native. Commands may stay in C# behind a thin facade, or move too.

## Decision

Option 3. The evaluator, binder, values, faults, scope, history and command contract
are F#. Commands are rewritten in F# as they are touched; a C# facade keeps any that
are not.

Failure is a value:

```
Result = Ok of value | Fail of fault
Option = Some of value | None
Fault  = { kind; message; stage; path option; cause option }
```

The pipe is bind. Exceptions are reserved for programmer bugs and are converted to a
fault of kind `internal` at the boundary, so a defect surfaces as a message and never
ends the session.

## Consequences

`ConsoleError` disappears. Every fault message in the documentation stays the same;
its shape gains structure. A fault can be bound to a variable, inspected and matched
in the language ([0014](0014-recovery-operator.md)). Undo no longer depends on
per-instance state ([0010](0010-undo-by-event-sourcing.md)).
