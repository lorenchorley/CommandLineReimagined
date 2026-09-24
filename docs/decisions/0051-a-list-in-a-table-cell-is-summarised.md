# 0051. A list in a table cell is summarised

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

A table shows a list cell as its items written out, separated by spaces. `pick`'s
`@children` column made that the common case, and the root row of `$d | pick "*"` wrote
out the whole document in one cell.

## Options

1. **Write lists out, as now.**
2. **Summarise a list in a cell**, and leave the list itself as it is.

## Decision

Option 2, chosen by the owner. In a table cell, the `@children` column shows `N children`
(`1 child`, and nothing for none), and any other list shows `N items` (`1 item`, and
nothing for none). What the cell holds is unchanged: `$row.@children` reads the whole
list, and a list shown on its own, outside a table, is still written out.

## Consequences

Only how a table is drawn changes, in the core's display and in the page's cells, which
come from it. A script sees the same values.
