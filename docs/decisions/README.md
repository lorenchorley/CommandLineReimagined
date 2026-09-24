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
| [0009](0009-table-coercion.md) | Table-shaped tags become tables implicitly, missing cells are None | Accepted; one sentence superseded by 0028 |
| [0010](0010-undo-by-event-sourcing.md) | Undo and persistence through an event-sourced store | Accepted |
| [0011](0011-real-xml-files.md) | XML is read and written as real files | Accepted |
| [0012](0012-browser-first.md) | The desktop shell stays out of scope | Accepted |
| [0013](0013-attribute-filesystem.md) | An attribute-and-query filesystem in the style of BeOS and Haiku | Accepted |
| [0014](0014-recovery-operator.md) | Error recovery is spelled `else`, not `or` | Accepted |
| [0015](0015-atomic-lines.md) | A command line is one atomic transaction | Accepted |
| [0016](0016-folders-as-records.md) | Folders are records with a `kind` of `folder`, and the root is implicit | Accepted |
| [0017](0017-assignment-arguments.md) | `name=value` in argument position is data; `name: value` binds a parameter | Accepted; one consequence superseded by 0027 |
| [0018](0018-the-seed-is-not-a-line-anyone-typed.md) | The seeded filesystem is recorded but cannot be undone | Accepted |
| [0019](0019-reserved-words-in-expression-positions.md) | Reserved words are reserved everywhere, not only in expressions | Accepted |
| [0020](0020-scripts-and-run.md) | Scripts are files of command lines, and `run` executes them one line at a time | Accepted |
| [0021](0021-variadic-parameters.md) | A command may declare one parameter that collects the rest of the arguments | Accepted |
| [0022](0022-hyphenated-command-names.md) | A command's name may be several words joined by hyphens | Accepted |
| [0023](0023-adjacent-function-parenthesis.md) | The function form needs its parenthesis against the name; a spaced parenthesis is a nested pipeline | Accepted |
| [0024](0024-stop-is-not-recoverable.md) | Stop is not a failure a line can recover from | Accepted |
| [0025](0025-xml-text-content.md) | XML element text content is read as a `text` attribute | Accepted |
| [0026](0026-inventory-sorts-its-reorder-list.md) | The inventory program sorts its reorder list before writing it | Accepted |
| [0027](0027-save-takes-a-tag.md) | `save` takes its attributes in the tag, not as assignments | Accepted |
| [0028](0028-mixed-columns.md) | A column whose cells disagree is typed `mixed`, not `text` | Accepted |
| [0029](0029-scene-editor-direction.md) | The entity component system becomes a scene the command line edits | Proposed |
| [0030](0030-undo-takes-the-line-back-on-screen.md) | On screen, undo takes the line back rather than adding one | Accepted |
| [0031](0031-completion-reads-the-line.md) | Completion reads the line, and may run what comes before the cursor | Accepted |
| [0032](0032-a-stage-may-be-a-value.md) | A stage may be a value: `$files` and `$problem.kind` are lines | Accepted |
| [0033](0033-a-predicate-is-a-question-about-the-row.md) | A predicate that never reads `$row`, or is not true or false, is a fault | Accepted |
| [0034](0034-what-answers-a-predicate.md) | The word `true` or `false` answers a predicate, and so must every operand of `and`, `or` and `not` | Accepted |
| [0035](0035-a-value-stage-ignores-its-input.md) | A value stage ignores what is piped into it | Accepted |
| [0036](0036-the-guide-is-in-the-filesystem.md) | The guide to the terminal lives in its filesystem, and the banner points to it | Accepted |
| [0037](0037-in-out-back-and-read.md) | Places are entered with `in`, left with `out`, retraced with `back`; files are read with `read` | Accepted |
| [0038](0038-a-wrong-call-shows-its-help.md) | A command called wrongly shows its help | Accepted |
| [0039](0039-the-pipe-comes-first.md) | After a complete stage, completion offers the pipe first | Accepted |
| [0040](0040-seeded-files-follow-the-seed.md) | A seeded file nobody has changed follows the seed | Accepted |
| [0041](0041-guidance-is-drawn-apart-from-output.md) | What the terminal says of its own is drawn apart from output | Accepted |
| [0042](0042-a-missing-name-names-the-nearest.md) | A missing file or column names the nearest ones | Accepted |
| [0043](0043-an-empty-filter-explains-itself.md) | An empty filter explains itself | Accepted |
| [0044](0044-a-fault-may-carry-fixes.md) | A fault may carry fixes, which the page offers as chips | Accepted |
| [0045](0045-a-near-value-offers-a-fix.md) | A value no row has offers the nearest one that a row does | Accepted |
| [0046](0046-restore-messages-are-guidance.md) | What the page says about restoring a session is guidance | Accepted |
| [0047](0047-a-live-listing-stays-where-it-was-run.md) | A live listing stays where it was run | Accepted |

Retrospective records document decisions taken before the log existed, so that the
log is the one place to look. Their alternatives are the ones weighed at the time, as
recorded in the [design doc](../spec/design-doc.md#alternatives-considered).
