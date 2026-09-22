# 0028. A column whose cells disagree is typed `mixed`, not `text`

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-22 |
| Supersedes | One sentence of [0009](0009-table-coercion.md)'s decision |

## Context

[Decision 0009](0009-table-coercion.md) says a table's attribute values "are typed per
column when every value agrees, otherwise the column is text." The Phase 3 plan gave
the `Table` value a sixth column type, `MixedCol`, and that is what was built: a column
whose non-`None` cells are not all of one kind is typed `mixed`, and each cell keeps its
own kind. The Phase 7 specification review found the record and the code disagreeing,
and the plan never recorded why it departed from the record.

## Options

1. **Text, as 0009 says.** A column of `3`, `x` and `true` is typed text. To mean
   anything, that has to either turn the number and the boolean into text, losing what
   they were, or leave them as they are under a label that describes none of them.
2. **Mixed, as built.** The column says its cells disagree, and each cell is still the
   value it was.

## Decision

Option 2. A column's type is a description of its cells, not a conversion applied to
them. `mixed` says truthfully that there is no one type; `text` would say something
false about the number in it. The core never reads a column's type to compare or sort,
since comparison is decided per pair of values, so in the language nothing depends on
the label but what `columns` shows.

## Consequences

`columns` can answer `mixed`. The page's header re-sort orders a `number` column
numerically and every other column, `mixed` included, by the text on screen, which is
what it would have done for `text`. The
wire format's column types are `text`, `number`, `boolean`, `file`, `object` and
`mixed`, as the host interfaces already list them. A `None` cell never makes a column
mixed, which 0009's gaps rule already implied.
