# 0052. `pick` answers each element once, when the documents overlap

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

`pick` reads a list of tags, or a table whose rows have `@tag`, as that many documents
(decision 0049). The rows of `pick "*"` overlap: a book is a row of its own and also
inside the library's row. So `$d | pick "*" | pick "[year^=19]"` answered `dune` twice.

## Options

1. **Search each document on its own**, and repeat what they share.
2. **Answer each element once**, as `querySelectorAll` does over a set of elements.

## Decision

Option 2, chosen by the owner. A document given to `pick` that sits inside another one
given is not searched again, so each element is answered once, in the order of the first
document it was found in. Two documents that are equal but separate, such as two
`<author name=x/>` typed twice, are still two.

## Consequences

`pick` needs to know which documents sit inside which: the element a row stands for, not
only a copy of its text.
