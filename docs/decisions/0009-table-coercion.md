# 0009. Table-shaped tags become tables implicitly, missing cells are None

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-21 |

## Context

The tag notation is already table-like: a parent whose children share a type, each
child carrying attributes, is a table with the attributes as columns. The owner asked
for native table support from that notation, but only when the data is properly
structured.

## Options

1. **Explicit conversion only.** A `table` command turns a tag into a table; nothing
   converts by itself. Predictable, and one more thing to type in every pipeline.
2. **Implicit where a table is expected.** A table function receiving a table-shaped
   tag treats it as a table; a tag that is not table-shaped stays a tree and the
   function reports why.
3. **Strict rows.** Every row must carry exactly the same attributes, or the tag is
   not a table.
4. **Union rows.** Columns are the union of every row's attributes; a missing cell is
   `None`.

## Decision

Options 2 and 4. A tag is table-shaped when every child has the same type and no
child has children of its own. Attribute values are typed per column when every value
agrees, otherwise the column is text.

## Consequences

`Option` has a job from day one: a sparse row reads back with `None` in its gaps
rather than failing. A tree that is not table-shaped produces a fault that names the
first child that broke the shape.
