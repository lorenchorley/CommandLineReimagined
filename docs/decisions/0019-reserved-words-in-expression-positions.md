# 0019. Reserved words are reserved everywhere, not only in expressions

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-22 |

## Context

[0007](0007-notation-conflicts.md) chose word operators — `eq ne gt ge lt le like has`
with `and`, `or`, `not` — and [0014](0014-recovery-operator.md) added `else`, with `try`
following in Phase 5. Every one of those words is also a perfectly ordinary bare word,
and bare words are what command arguments are made of. `echo eq` and
`where $row.kind eq eq` have to mean something, and the grammar has to be able to say
what.

The parser has no idea which position it is in: `where` is not a keyword, it is a
command name like any other, and its `predicate` parameter is discovered by the binder
long after the line has been parsed.

## Options

| Option | What it does | Cost |
| --- | --- | --- |
| A. Contextual keywords | A word is an operator only where an operator can appear, and a bare word everywhere else. | The parser would have to know which parameters are predicates, which it learns after parsing. Each new operator changes what old lines mean. |
| B. Reserved everywhere | The ten operator words and `else` are never bare words, in any position. `echo eq` is a syntax error; `echo "eq"` is the text. | Eleven English words need quotes to be passed as data. Reserving one more word later is a breaking change. |
| C. A sigil on operators | `$row.size :gt: 100`, so no English word is ever taken. | Noise on every predicate, on a keyboard where `:` is a shift away, to protect words nobody passes as data. |
| D. Reserved only as whole arguments | `eq` alone is an operator; `eq.txt` or `eqs` is a word. | Already true of B, since a reserved word is only reserved when it is the whole word. Stated separately it reads like a second rule. |

## Decision

Option B, with D's clarification folded into it: a reserved word is a word that is
*exactly* one of `and or not eq ne gt ge lt le like has else try`. `eq.txt`, `equals`
and `Eq` are ordinary words — the match is exact and case-sensitive, because the
operators are written in lower case and a capital is a good enough escape hatch for a
one-off.

Writing one as data is `echo "eq"`. A command may not be named after a reserved word,
and none is.

## Consequences

The word parser rejects eleven strings, and the syntax error names them: a line that
says `echo eq` is told that `eq` is an operator and how to quote it. Reserving a word
in a later phase is a breaking change, so the full list is reserved now, `try` and
`else` included, before Phase 5 needs them. The tokeniser gains an `operator` kind, and
the page colours the words that are no longer ordinary.
