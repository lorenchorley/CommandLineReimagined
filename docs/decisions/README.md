# Decision log

Architecture decisions, one file each, in the order they were taken. Each record
states the question, every option that was on the table, which one was chosen and
why, and what that choice costs. A record is never edited once accepted; a later
record supersedes it.

Statuses: **Proposed** (awaiting the owner's answer), **Accepted**, **Superseded by**
a later record, **Rejected**.

| # | Decision | Status |
| --- | --- | --- |
| [0001](0001-record-decisions.md) | Keep a decision log | Accepted |
| [0002](0002-combinator-parser.md) | Parser combinators in F# replace the GOLD table parser | Accepted, retrospective |
| [0003](0003-execute-in-the-browser.md) | The browser executes commands itself; no server round trip | Accepted, retrospective |
| [0004](0004-dom-not-canvas.md) | The browser terminal renders to the DOM, not a canvas | Accepted, retrospective |
| [0005](0005-dotnet-10.md) | .NET 10 LTS, with dependencies managed centrally | Accepted, retrospective |
| [0006](0006-functional-core-in-fsharp.md) | The execution layer moves to F#, with Result and Option as the error model | Accepted |
| [0007](0007-notation-conflicts.md) | Notation conflicts: lookahead delimiters plus word operators | Accepted |
| [0008](0008-explicit-row-variable.md) | Predicates name the row explicitly | Accepted |
| [0009](0009-table-coercion.md) | Table-shaped tags become tables implicitly, missing cells are None | Accepted |
| [0010](0010-undo-by-event-sourcing.md) | Undo and persistence through an event-sourced store | Accepted |
| [0011](0011-real-xml-files.md) | XML is read and written as real files | Accepted |
| [0012](0012-browser-first.md) | The desktop shell stays out of scope | Accepted |
| [0013](0013-attribute-filesystem.md) | An attribute-and-query filesystem in the style of BeOS and Haiku | Accepted |
| [0014](0014-recovery-operator.md) | Error recovery is spelled `else`, not `or` | Accepted |

Retrospective records document decisions taken before the log existed, so that the
log is the one place to look. Their alternatives are the ones weighed at the time, as
recorded in the [design doc](../spec/design-doc.md#alternatives-considered).
