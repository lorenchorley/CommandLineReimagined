# 0002. Parser combinators in F# replace the GOLD table parser

| Field | Value |
| --- | --- |
| Status | Accepted, recorded retrospectively |
| Date | 2026-09-19 |

## Context

The original parser was generated from a GOLD grammar file into a binary table and
driven by a visitor. Roughly a third of the grammar's productions parsed but threw
`NotImplementedException` when visited, every syntax error was reported at column
zero, and changing the grammar meant regenerating tables with an external tool.

## Options

1. **Finish the GOLD visitor.** Implement the missing productions and fix error
   positions. The grammar stays in a binary artefact produced outside the build.
2. **Hand-written recursive descent in C#.** Full control, no dependencies, but every
   production is written twice: once as documentation, once as code.
3. **Parser combinators in F# with FParsec.** The grammar is source, in the same order
   as the original BNF, with faithful error positions. F# enters the solution.

## Decision

Option 3. The GOLD parser is retained only so the two can be compared in tests.

## Consequences

F# is a first-class language in the solution, which makes [0006](0006-functional-core-in-fsharp.md)
a smaller step. Error positions are exact. The bare-word extension in
[0007](0007-notation-conflicts.md) was possible because the grammar is source.
