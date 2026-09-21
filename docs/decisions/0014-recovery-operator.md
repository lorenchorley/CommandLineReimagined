# 0014. Error recovery is spelled `else`, not `or`

| Field | Value |
| --- | --- |
| Status | Proposed |
| Date | 2026-09-21 |

## Context

The railway needs a way for a user to recover from a failed stage without leaving the
pipeline. `or` was the first candidate. Word operators
([0007](0007-notation-conflicts.md)) make `or` the boolean disjunction in predicates,
so it cannot also mean recovery.

## Options

1. **`or` for both.** Context decides. Two meanings for one common word.
2. **`else`.** `cat notes.txt else echo "none"`. Reads as prose; unused elsewhere.
3. **A symbol, `|?`.** Short, and one more thing to explain and to type on a phone.

## Decision

Proposed: option 2, with `try` to turn a failure into a value and `??` to unwrap an
option with a default.

```
cat notes.txt else echo "no notes yet"
try (cat notes.txt) | set r
$maybe ?? "default"
```

## Consequences

`else`, `try`, `and`, `or`, `not` and the comparison words become reserved in
expression positions. A command may not be named after any of them.
