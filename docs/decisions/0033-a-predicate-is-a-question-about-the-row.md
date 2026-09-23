# 0033. A predicate is a yes-or-no question about the row

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-23 |

## Context

Two predicates that cannot be what anyone meant pass in silence today:

- `ls | where kind eq folder` compares the text `kind` with the text `folder`. It
  never reads `$row` ([0008](0008-explicit-row-variable.md)), so it is the same for
  every row, and the answer is an empty table.
- `ls | where $row.kind` asks for rows whose kind is true. A kind is text, never true
  or false, and the answer is again an empty table.

An empty table is a correct answer to a real question, so these two look like
correct answers to real questions. They are the likeliest predicates for a newcomer
to write.

## Options

1. **Leave it.** The rules are consistent, and the empty table is what they say.
2. **A warning.** The table is answered and a note says why it may be empty. The
   line still succeeds, so a script carries on with the wrong answer.
3. **Faults.** Both are failures of the line, with a message that names the fix.

## Decision

Option 3.

- A predicate that never reads `$row` is a `Binding` fault when it is bound, naming
  the bare words it compared as the likely columns:
  `kind eq folder never reads $row, so it is the same for every row. Did you mean
  $row.kind eq folder?`
- A predicate whose value for a row is not a boolean is an `Invalid` fault on the
  first such row, naming the value and the fix:
  `$row.kind is text (folder), not true or false. Compare it: $row.kind eq folder.`

A boolean attribute read bare, `where $row.done`, stays valid. `cd` with a plain
operand is a path ([0013](0013-attribute-filesystem.md)) and is not affected. The
static check lives where the binder builds the query, so `where`, `find`, `cd` and
`save-view` all get it.

## Consequences

Lines that used to answer an empty table now fail, and `else` and `try` can recover
from them like any other fault. The example programs and every documented transcript
are re-run to prove that nothing real depended on the old behaviour. If the owner
later prefers a warning, only the fault constructors change.

Accepted by the owner on 2026-09-23.
