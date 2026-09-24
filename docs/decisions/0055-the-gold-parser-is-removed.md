# 0055. The GOLD parser is removed

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |
| Supersedes | The sentence of [0002](0002-combinator-parser.md) that retained the GOLD parser |

## Context

[0002](0002-combinator-parser.md) replaced the GOLD table parser with parser combinators
in F#, and kept the old one "only so the two can be compared in tests". Every change to
the grammar since has had to stay out of the comparison, because the GOLD grammar was
frozen: bare words, word operators, members, reserved words, `else`, `try`, `??`,
pipelines in parentheses, hyphenated names, value stages and `@` members all live in
FParsec-only test classes. The comparison covered a shrinking corner of the language,
and the GOLD parser lived in the desktop shell, which
[0054](0054-the-windows-front-end-is-removed.md) removes.

## Options

1. **Keep it, in a project of its own.** A frozen parser kept alive for one test class.
2. **Remove it, and keep its test inputs** as tests of the one parser.

## Decision

Option 2, chosen by the owner. The GOLD engine, its grammar files and the equivalence
tests are removed. Every input the comparison held is kept in `Parser.Tests`: each one
that both parsers accepted must still parse and serialise stably, each one both rejected
must still be rejected, and the error columns they agreed on are pinned.

## Consequences

The plan's rule "do not touch the GOLD parser or its `.grm` files" and the
specification's references to equivalence tests no longer apply. The one known deviation
about the GOLD parser's error column goes with it.
