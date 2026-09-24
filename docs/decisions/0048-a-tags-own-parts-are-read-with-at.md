# 0048. A tag's own parts are read with `@`

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

`set v <thing a=1/>` then `echo $v.a` answers `1`, and nothing answers `thing`: a member
read off a tag looks only at its attributes, so the tag's name and its children cannot
be reached at all. A plain member cannot be given the job. `name`, `type` and `kind` are
among the commonest attribute names (`<note name=monday/>`), and whichever were chosen
would stop meaning the attribute.

## Options

1. **A member named after the part**, such as `$v.type`. Collides with attributes.
2. **A function**, as XPath has `name()` and the DOM `tagName`: `tag($v)`. Safe, and it
   does not chain as a member does.
3. **A sigil for the tag's own parts**, as XPath marks attributes with `@`, here the other
   way round: plain members stay attributes, and `@` marks what is the tag's own.

## Decision

Option 3, chosen by the owner. After the stop, a member may be written with `@` before
its name: `$v.@tag` is the tag's name as text, `thing`, and `$v.@children` its children,
in order, as a list. An attribute name cannot start with `@`, in the tag notation or in
XML, so neither can ever mean an attribute.

A row of a table answers its columns first: where a row has a column named `@tag` or
`@children`, as the rows `pick` answers do (decision 0049), that is what the member
reads. Any other `@` member, and either one on a value that is not a tag, is `None`, as
a missing attribute is.

## Consequences

The grammar's member name gains the `@` form, in the combinator parser only; the GOLD
parser is not changed, and the new form is tested among the FParsec-only cases.
Completion after `$v.` offers `@tag` and `@children` beside the attributes.
