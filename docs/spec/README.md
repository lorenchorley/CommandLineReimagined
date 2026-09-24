# CommandLineReimagined specification

The normative definition of the command language and its runtime. The
[user documentation](../README.md) describes what the current implementation does; this
specification says what any implementation must do.

## Documents

| Document | Contents |
| --- | --- |
| [Design doc](design-doc.md) | Context, goals, the design itself, alternatives and cross-cutting concerns. |
| [Lexical structure and grammar](lexical-grammar.md) | Character sets, tokens, EBNF, error reporting. |
| [Semantic tree](semantic-tree.md) | Node catalogue, visitors, token kinds, round-trip requirement. |
| [Execution model](execution-model.md) | Values, argument binding, pipelines, tags, undo, cancellation, and the notes beside an answer. |
| [Command catalogue](command-catalogue.md) | The contract of each built-in command. |
| [Host interfaces](host-interfaces.md) | Output, lifetime, session and browser bridge, with wire formats. |
| [Conformance](conformance.md) | What an implementation must satisfy, and the tests that prove it. |

## About the format

These documents follow Google's design-document conventions: a single design doc
carrying context and scope, goals and non-goals, the design, alternatives considered
and cross-cutting concerns, with the detailed normative material split into companion
documents. Prose follows the Google developer documentation style guide: second person,
present tense, active voice, sentence case headings, and code font for anything you
would type.

## Requirement keywords

The key words **must**, **must not**, **required**, **shall**, **should**,
**should not**, **may** and **optional** are to be interpreted as described in
[RFC 2119](https://www.rfc-editor.org/rfc/rfc2119). They appear in bold when used
normatively; the same words in ordinary prose carry their ordinary meaning.

## Status

| Field | Value |
| --- | --- |
| Status | Implemented, and matching the code at the time of writing |
| Applies to | The `Parser.FParsec`, `Parser.Tree`, `CommandLineReimagined.Core`, `Terminal`, `Web.Core` and `WebClient` assemblies |
| Reference implementation | This repository |
| Conformance suite | `Parser.Tests` (396), `Core.Tests` (959), `Web.Core.Tests` (150), `Terminal.Tests` (32) |

Where the specification and the implementation disagree, one of them is wrong; the
[conformance](conformance.md) document names the test that decides. Why the design is
the way it is, and what was rejected, lives in the [decision log](../decisions/README.md).
