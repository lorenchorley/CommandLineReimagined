# 0044. A fault may carry fixes, which the page offers as chips

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

When the terminal knows what was probably meant, the person still retypes it: reads
`Did you mean $row.kind eq folder?`, and writes the line again with the change. On a
phone, that is the slowest thing on the screen.

## Options

1. **Say it, as now.**
2. **Run the fix.** Fast, and wrong the time the guess is wrong, since it would change
   the store without anyone having asked.
3. **Offer the whole corrected line** as a chip that puts it in the input, with the
   caret at the end, and does not run it.

## Decision

Option 3, chosen by the owner. A fix is a whole corrected line. The unknown command,
the predicate that never reads `$row`, the one that is not true or false, the missing
file or folder with a near name, the unknown variable with a near one, and the column
no row has (0043) each supply one where they can. Tapping it fills the input; the
person runs it, or edits it first.

## Consequences

Whatever finds a mistake rarely knows the line it was written in, so a fix may be said
as a replacement of what was written, and the session makes the whole line of it
against the line the person typed. A mistake with no place in that line, such as one
inside a script `run` ran, is still suggested and offers no fix.
