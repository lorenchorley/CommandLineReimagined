# 0032. A stage may be a value

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-23 |

## Context

A variable can only be read as an argument. `$v` typed alone, `$files | count` and
`$problem.kind` are syntax errors, reported in grammar symbols:
`Syntax error at column 0: expected end of input, identifier, (, <, <$, try, {.`.
So a person who has just run `ls | set files` cannot look at what they kept without
wrapping it in a command. Decision [0014](0014-recovery-operator.md) even gave
`$maybe ?? "default"` as an example line, which the grammar has never accepted.

A tag may already stand as a stage (`Binder.evaluateTag`), so the grammar has a
precedent for a stage that is a value rather than a command.

## Options

1. **Leave it.** Variables are read through commands: `echo $v`, `table $files`.
   No grammar change, and 0014's example stays wrong.
2. **A value command.** Add `get $v` or `show $v`. No grammar change; one more
   command to learn for something that looks as if it should just work.
3. **A variable reference may stand as a stage**, with or without members, the way a
   tag already can.

## Decision

Option 3. `$files`, `$problem.kind` and `$files | where $row.size gt 10` are lines.
The stage's value is the variable's value, so `??`, `else` and `try` apply to it as
they do to any stage, and `$maybe ?? "default"` becomes true. `$row` standing as a
stage outside a predicate is a fault of its own, which says where `$row` exists:
`$row is the row a predicate is testing. It exists only inside where, find, cd and
save-view: ls | where $row.kind eq folder.`

A stop after a variable must be followed by a name, so `$row.` is a syntax error that
says a column name belongs after the stop, rather than a variable followed by a path
called `.`.

## Consequences

The grammar grows a value stage, with its tree node and its token and serialisation
visitor cases. Following the Phase 7 convention, the GOLD parser is not taken along:
the new lines go in the FParsec-only parser tests. The page can draw a variable by
typing its name, which is what the browser check for Phase 8 relies on.

Accepted by the owner on 2026-09-23.
