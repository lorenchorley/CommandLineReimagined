# 0035. A value stage ignores what is piped into it

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-23 |

## Context

Decision [0032](0032-a-stage-may-be-a-value.md) lets a variable stand as a stage:
`$files`, `$files | count`. It did not say what happens when a value stage is not
first, as in `ls | $v`. The stage before it produces a value, and a value stage has
nothing to do with it.

## Options

1. **Ignore it.** The stage's value is the variable's, whatever came before, as a tag
   standing alone already behaves.
2. **Make it a fault.** `ls | $v` says a value stage takes nothing from the pipe.

## Decision

Option 1, chosen by the owner. It is what 0032's wording already says, that the
stage's value is the variable's value, and it is how a tag standing as a stage
behaves, so the two kinds of value stage agree.

## Consequences

`ls | $v` answers `$v`, and the stages before it still run, so a line that writes
before a value stage still writes. Nothing new to implement: this records the
behaviour Phase 8 built, so that it is decided rather than incidental.
