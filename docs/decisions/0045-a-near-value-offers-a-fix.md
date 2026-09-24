# 0045. A value no row has offers the nearest one that a row does

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

Decision 0043 explains an empty filter over a column that exists by naming the values
the column does have: `ls | where $row.kind eq foldr` says `kind is folder or text`.
Decision 0044 lists the faults and explanations that carry a fix, and a value one slip
from one the column has was not among them, so the person reads `folder` and retypes
the line.

## Options

1. **Explain only**, as 0043 and 0044 are written.
2. **Offer the nearest value as a fix**, as a missing column already offers the nearest
   column.

## Decision

Option 2, chosen by the owner. When the value compared is within `Nearest`'s distance
of values the column has, the explanation carries a fix for the nearest:
`ls | where $row.kind eq folder`. The text of the explanation is unchanged, and a value
with nothing near has no fix.

## Consequences

This adds one entry to 0044's list of what supplies a fix. The fix replaces the value as
it was written; a value written in a variable has no place in the line and offers none.
