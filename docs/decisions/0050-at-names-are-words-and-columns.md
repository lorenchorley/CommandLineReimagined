# 0050. An `@` name is a word, and a row answers any `@` column

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

`pick` answers columns named `@tag` and `@children` (decision 0049), and neither could
be written as an argument: a bare word could not start with `@`, so `select @tag` failed
to parse and had to be `select "@tag"`. And decision 0048 made every `@` member other than
`@tag` and `@children` `None`, so a column such as `@id`, from a CSV header, could be
listed and never read.

## Options

1. **Keep both as 0048 and the grammar have them.**
2. **Let an argument start with `@`, and let a row answer any column by its name.**

## Decision

Option 2, chosen by the owner. A bare word may start with `@`, so `select @tag` and
`sort @tag` name the column as it is shown. A row answers any of its columns by name,
`@` or not: `$row.@id` reads a column `@id`. On a tag with no such attribute, `@tag` and
`@children` still read the tag's own parts, and any other `@` member is `None`.

## Consequences

The combinator parser's bare word gains a leading `@`; the GOLD parser is not changed,
and the new form is tested among the FParsec-only cases. This supersedes the sentence of
0048 that made every other `@` member `None` on a row.
