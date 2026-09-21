# 0007. Notation conflicts: lookahead delimiters plus word operators

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-21 |

## Context

Four collisions between notations, three present and one arriving with table
predicates:

1. `/` inside a path against `/>` closing a tag, so a tag attribute cannot be a bare
   path.
2. A leading `-` is a flag, so `echo -5` fails.
3. `set $x 1` reads the variable rather than naming it.
4. `<` and `>` as comparison operators against tags, and `{ }` already meaning
   components, leaving no braces for expression blocks.

## Options

| Option | What it does | Cost |
| --- | --- | --- |
| A. Keep context rules | Paths in tags stay quoted; more rules per feature. | Rules accumulate. Quotes are painful on a phone keyboard. |
| B. Lookahead delimiters | `/>` and `</` are tokens only as those exact pairs, so `path=a/b` parses and `/>` still closes. `<` opens a tag only when followed directly by a name, `$` or `/`; otherwise it is less-than. A `-` followed by a digit is a number. | Resolves 1, 2 and half of 4. Error messages need care for rare ambiguous inputs. |
| C. Word operators | Comparisons are words: `eq ne gt ge lt le like has`, with `and`, `or`, `not`. No `<` or `>` in predicates. | Resolves 4 entirely; needs no shift key. More verbose than symbols. |
| D. Symbols inside parentheses only | `where ($row.size > 100)` permits symbol operators; tags are not permitted bare inside an expression group. | Two ways to write one thing. |
| E. Change the tag delimiters | For example `@person name=ann`. | Removes every collision and discards the XML-like notation the project is built on. |
| F. Path sigil | A bare path in a tag must start with `./`, `../`, `/` or `~`. | Inconsistent with command arguments, which take any word. |

## Decision

B and C together. D may be added later if asked for. E and F rejected. Collision 3
stays as it is: `set x 1` is the intended spelling, and the message already explains
the mistake.

## Consequences

The grammar gains two-character delimiter tokens and a numeric-lookahead rule for `-`.
Tag attributes accept bare words. Predicates have no symbol operators, which is what
makes `or` unavailable as a recovery keyword ([0014](0014-recovery-operator.md)).
Bare-word tests in `Parser.Tests` gain cases for `path=a/b/>` and `-5`.
