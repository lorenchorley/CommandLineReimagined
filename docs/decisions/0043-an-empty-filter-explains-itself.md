# 0043. An empty filter explains itself

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

`ls | where $row.knd eq folder` and `ls | where $row.kind eq foldr` both answer an
empty table. The answer is right: no row has a `knd` of `folder`, and none has a
`kind` of `foldr`. It is also indistinguishable from a question whose honest answer is
"none", and the person is left guessing which it was.

## Options

1. **Leave it.** Correct and silent.
2. **Make a column no row has a fault.** Breaks the tag notation's promise that a
   missing cell is `None` (decision 0009), and a filter over mixed records must be able
   to read a column only some of them have.
3. **Answer the empty table, and explain it.** The answer is unchanged; beside it the
   terminal says why nothing was kept.

## Decision

Option 3, chosen by the owner. When `where`, `find` or a view keeps no row of a table
that had some, the response carries an explanation note, and at most one:

- a column the predicate reads that no row has: `No row has knd; did you mean kind?`,
  with the nearest columns (0042) and a fix for the first (0044);
- otherwise, for a comparison of a column that exists with a value no row has, the
  values that column does have, most frequent first, at most five:
  `kind is folder or text`.

An empty table in, or a filter that keeps a row, says nothing.

## Consequences

The answer is still the empty table, so a script and a pipe see what they saw before;
the explanation is guidance (0041). A live view re-run with no rows carries it too.
