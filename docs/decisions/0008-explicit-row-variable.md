# 0008. Predicates name the row explicitly

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-21 |

## Context

Table functions such as `where`, `select` and `sort` take an expression evaluated per
row. The expression has to refer to the row's columns.

## Options

1. **Bare column names.** `where size gt 100`. Shortest. A column named like a
   variable or a command is ambiguous, and a reader cannot tell a column from a word.
2. **An explicit row variable.** `where $row.size gt 100`. One extra token per
   reference; no ambiguity; the same `$` sigil the language already uses for values.
3. **Bare names with `$row` as the escape hatch.** Short by default, explicit when
   needed. Two spellings for one thing.

## Decision

Option 2. Inside a predicate, `$row` is bound to the current row and `.name` reads a
column. Nested scopes exist to make that binding local to the predicate.

## Consequences

Predicates are unambiguous and colour cleanly: `$row.size` tokenises as a variable
followed by a member access. Scope nesting, which the model supported and no host
used, is exercised.
