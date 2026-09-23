# 0034. The word `true` or `false` answers a predicate, and so must every operand

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-23 |

## Context

Decision [0033](0033-a-predicate-is-a-question-about-the-row.md) made a predicate
whose answer is not true or false a fault. Building it in Phase 8 left two cases it
did not settle:

- `attr monday done=true` stores the *word* `true`, because an assignment's value is
  data ([0017](0017-assignment-arguments.md)). So `ls | where $row.done` read text,
  and was built to count it as false, without a fault: the row set with `done=true`
  was dropped, in silence, by the very line 0033 gives as the valid case.
- 0033 checks the predicate's overall answer only. The operands of `and`, `or` and
  `not` were read loosely, anything but `true` counting as false, so
  `where not $row.kind` kept every row and
  `where $row.kind eq folder or $row.kind` answered without a fault.

## Options

For the word:

1. **Count it as the boolean it spells.** `true` and `false`, in any case.
2. **Make it 0033's fault.** `$row.done is text (true), not true or false.`
3. **Leave it false, silently.**

For the operands:

1. **Hold every operand to the same rule** as the whole predicate.
2. **Top level only**, as 0033 was worded.

## Decision

The first option of each, chosen by the owner.

- A value answers a yes-or-no question when it is `true` or `false`, or the word
  `true` or `false` in any case. A gap is `false` ([0009](0009-table-coercion.md)).
  Anything else is 0033's `Invalid` fault.
- That rule holds for the whole predicate and for each operand of `and`, `or` and
  `not`, and the fault names the operand it was about:
  `$row.kind is text (folder), not true or false. Compare it: $row.kind eq folder.`
- `and` and `or` still short circuit, so an operand that is never read is never
  checked.

## Consequences

`where $row.done` works on an attribute set with `attr`, as it reads. A line that used
text as a boolean inside `and`, `or` or `not` now fails where it used to answer, and
`else` recovers from it like any fault. The rule lives in one function, `Expr.truth`,
used by both the row test and the three operators, so the two cannot drift apart.
