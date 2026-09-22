# 0026. The inventory program sorts its reorder list before writing it

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-22 |

## Context

The plan's rule is that the four example programs and their golden results are written
before the code, and that a program is never edited to fit the implementation without a
record saying why. Phase 6 found `inventory.clr` disagreeing with itself. Its sixth line
writes the reorder list:

```
from-xml items.xml | where $row.qty lt $row.min | select sku qty | to-csv reorder.csv
```

and the golden result for `cat reorder.csv` two lines later is

```
sku,qty
C3,0
B2,12
```

`where` keeps rows in the order they arrive, which the table functions have promised
since Phase 3, and the document lists B2 before C3. Nothing in that line reorders
anything, so the golden result is not what the line means under any implementation of
the rules already decided. The golden was written as if the line sorted, most likely
because the line before it does.

## Options

1. **Correct the golden** to `B2,12` then `C3,0`. The program stays as written and its
   reorder file lists items in the order the stock file has them.
2. **Add `sort qty` to the line**, so the golden stands. The reorder file lists the
   emptiest shelf first, which is what a reorder list is for.
3. **Make `where` or `to-csv` order rows.** Contradicts `where` keeping order, which
   `sort` being stable depends on, for the sake of one program.

## Decision

Option 2. The golden's exact CSV text is what Phase 6 is held to, and it says something
a person would want: the most urgent row first. The line becomes

```
from-xml items.xml | where $row.qty lt $row.min | sort qty | select sku qty | to-csv reorder.csv
```

## Consequences

The program is one stage longer and every golden result stands as written. The fix is
to the program, not to any rule, so nothing else in the language changes.
