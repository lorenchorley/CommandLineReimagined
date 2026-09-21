# Conformance

What an implementation has to satisfy, how the reference implementation proves it, and
where the implementation currently differs from this specification.

## Conformance classes

| Class | Must satisfy |
| --- | --- |
| **Parser** | [Lexical structure and grammar](lexical-grammar.md) and [Semantic tree](semantic-tree.md). |
| **Runtime** | Parser, plus [Execution model](execution-model.md). |
| **Terminal** | Runtime, plus [Command catalogue](command-catalogue.md) and [Host interfaces](host-interfaces.md). |

A Parser implementation is useful on its own: it is what an editor or a syntax
highlighter needs.

## Requirements and the tests that prove them

This table maps each normative area to the test class that covers it. The counts are
test methods; data-driven methods expand to more cases at run time.

| Requirement | Test class | Methods |
| --- | --- | --- |
| Lexical rules: identifiers, accents, flags, string forms, variables | `Parser.Tests/LexicalTests` | 15 |
| Bare words in argument positions, and their exclusion from attributes | `Parser.Tests/BareWordTests` | 9 |
| The three command forms, arguments, pipes | `Parser.Tests/CommandFormTests` | 19 |
| Object and component tags, nesting, closing forms, variable tags | `Parser.Tests/ObjectInstanceTests` | 17 |
| Productions the original grammar never implemented | `Parser.Tests/CompletedGrammarTests` | 17 |
| Error positions and expected symbols | `Parser.Tests/SyntaxErrorTests` | 5 |
| Round-trip serialisation | `Parser.Tests/SerialisationTests` | 7 |
| Agreement with the retained GOLD parser | `Parser.Tests/ParserEquivalenceTests` | 4 |
| Binding, pipes, command forms, variables, tags, undo | `Execution.Tests/ExecutionTests` | 36 |
| File and variable commands, and their undo | `Execution.Tests/FileAndVariableCommandTests` | 26 |
| Asynchronous commands, live output, cancellation | `Execution.Tests/AsyncCommandTests` | 7 |
| Session responses, streaming, cancellation, completion, undo, tokens | `Web.Core.Tests/TerminalSessionTests` | 25 |
| Path and naming helpers | `Terminal.Tests/ValidCommandTests` | 2 |

Cases actually run, which is what the suite reports:

| Project | Methods | Cases |
| --- | --- | --- |
| `Parser.Tests` | 93 | 199 |
| `Execution.Tests` | 69 | 69 |
| `Web.Core.Tests` | 25 | 25 |
| `Terminal.Tests` | 2 | 32 |
| Total | 189 | 325 |

Run them with:

```bash
for p in $(find . -name '*.Tests.csproj' | sort); do dotnet test "$p" -c Release; done
```

## Checklist for a new implementation

**Parser**

- [ ] Whitespace is space and tab only; a newline ends the program.
- [ ] String delimiters are matched longest first, and the delimiter count survives a
      round trip.
- [ ] Bare words are accepted in argument positions and rejected in attribute values.
- [ ] `-flag` is a flag and `--flag` is a syntax error.
- [ ] The empty program is decided before the command list, so error positions are
      faithful.
- [ ] Errors carry a zero-based line and offset, and a mismatched closing tag is
      reported as a message.
- [ ] Serialising a parsed tree reproduces the input.
- [ ] Token kinds match the table in [Semantic tree](semantic-tree.md#tokenisation),
      including the function form's name as `command`.

**Runtime**

- [ ] Values expose distinct display and argument strings, and paths differ between
      them.
- [ ] Binding follows the four steps, including optional parameters filling
      positionally.
- [ ] Piped input reaches only parameters that accept it and that were not written out.
- [ ] Arity and missing-argument messages match the wording in
      [Execution model](execution-model.md#argument-binding).
- [ ] An unknown name is reported through `UnknownCommand`.
- [ ] A pipeline threads values and stops at the first failure.
- [ ] Tags evaluate as values, as pipeline stages and as arguments, and bind their
      variable in every position.
- [ ] Each execution gets its own command instance, so undo is per invocation.
- [ ] Undo returns the name of what it reversed, and reversing a read-only command
      succeeds.
- [ ] An asynchronous command observes cancellation and reports through `FailedInvoke`.

**Terminal**

- [ ] One command at a time, with the documented refusal message.
- [ ] Cancellation reports `Stopped.` and keeps the command's own output.
- [ ] Output changes are raised with the complete line set and an execution id.
- [ ] Completion follows the context table, offsets included.
- [ ] Wire formats match [Host interfaces](host-interfaces.md#wire-formats).

## Known deviations

Where the implementation and this specification do not meet, with the decision in each
case.

| Deviation | Status |
| --- | --- |
| Property assignments parse but raise `Cannot evaluate a child PropertyAssignment.` | Intended for now. The grammar keeps them because the original did; evaluation has no meaning to give them yet. |
| A component child of an object is evaluated but not attached to the object's children. | Intended. Its purpose is the variable binding; the entity component model does not yet exist at runtime. |
| `<a/>` and `<a></a>` are distinct in the tree but evaluate alike. | Intended. The tree is a faithful record of what was typed. |
| The retained GOLD parser reports column 12 where the combinator parser reports 11 on one truncated input. | Documented in `ParserEquivalenceTests`. The combinator position is correct. |
| `pwd` at the filesystem root produces a result whose display name is empty. | Cosmetic. The path is still correct in the response and in the prompt. |
| Suggestion and completion chips are 26 pixels tall, below the usual 44 pixel touch target. | Known. Worth raising; the input and the run button already meet it. |
| `Scope` supports nesting, but no host creates a child scope. | Intended. The model is ahead of the shell. |
| Messages are English only and the client is published with invariant globalisation. | Intended for now; see [Design doc](design-doc.md#internationalisation). |

## Changing this specification

A change to any **must** in these documents is a language change. It needs a test in
the suite above that fails before the change and passes after, and an entry in the
table of known deviations if the implementation lags.
