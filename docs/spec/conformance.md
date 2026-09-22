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
| Bare words, in arguments and in tag attributes; assignments; negative numbers | `Parser.Tests/BareWordTests` | 18 |
| The three command forms, arguments, pipes, hyphenated command names | `Parser.Tests/CommandFormTests` | 26 |
| Object and component tags, nesting, closing forms, variable tags | `Parser.Tests/ObjectInstanceTests` | 17 |
| Productions the original grammar never implemented | `Parser.Tests/CompletedGrammarTests` | 17 |
| Error positions and expected symbols | `Parser.Tests/SyntaxErrorTests` | 5 |
| Round-trip serialisation | `Parser.Tests/SerialisationTests` | 7 |
| Word operators, precedence, member access, reserved words, nested pipelines, the expression entry point | `Parser.Tests/ExpressionTests` | 21 |
| Agreement with the retained GOLD parser | `Parser.Tests/ParserEquivalenceTests` | 4 |
| The two string forms of every value, and number formatting | `Core.Tests/ValueTests` | 16 |
| The Table value: coercion, columns, types, gaps, rows, display | `Core.Tests/TableTests` | 23 |
| Evaluating a predicate: members, comparison, gaps, boolean words | `Core.Tests/ExpressionTests` | 21 |
| Folding events, and that every event inverts back to where it started | `Core.Tests/ProjectionTests` | 13 |
| Path resolution over the projection, and kind inference | `Core.Tests/FilesTests` | 16 |
| Committing, undo, redo, history, replay determinism, blobs | `Core.Tests/StoreTests` | 26 |
| Binding, pipes, command forms, variables, tags, atomic lines | `Core.Tests/ExecutionTests` | 42 |
| File commands, attributes, saving tags, and their undo | `Core.Tests/FileCommandTests` | 42 |
| Variables and their undo | `Core.Tests/VariableCommandTests` | 14 |
| Asynchronous commands, live output, cancellation | `Core.Tests/AsyncCommandTests` | 10 |
| `undo`, `redo` and `history` as commands | `Core.Tests/MetaCommandTests` | 16 |
| The table functions, as whole command lines | `Core.Tests/TableCommandTests` | 29 |
| Views: `cd` on a predicate, `ls` across folders, `up`, `find`, `save-view`, refreshing | `Core.Tests/ViewTests` | 38 |
| The example programs, against their golden results, and `run` | `Core.Tests/ExampleProgramTests` | 14 |
| Completion over the projection, the operators, the columns and the places | `Core.Tests/CompletionTests` | 22 |
| The phase's acceptance list, from a fresh session | `Core.Tests/AcceptanceTests` | 10 |
| Replaying a log, seeding once, and `reset` | `Core.Tests/PersistenceTests` | 13 |
| The stored shape of a transaction, every event and value case, versioning | `Web.Core.Tests/LogFormatTests` | 22 |
| The browser's IndexedDB module, including a browser without it | `tools/store-check.mjs` | 20 |
| DTO shapes including tables, views, refreshing, streaming, cancellation, completion, tokens | `Web.Core.Tests/TerminalSessionTests` | 43 |
| Path and naming helpers | `Terminal.Tests/ValidCommandTests` | 2 |
| The published page, in a browser at phone size | `tools/browser-check.mjs` | 1 session |

Cases actually run, which is what the suite reports:

| Project | Cases |
| --- | --- |
| `Parser.Tests` | 293 |
| `Core.Tests` | 413 |
| `Web.Core.Tests` | 65 |
| `Terminal.Tests` | 32 |
| Total | 803 |

Run them with:

```bash
for p in $(find . \( -name '*.Tests.csproj' -o -name '*.Tests.fsproj' \) | sort); do
  dotnet test "$p" -c Release
done
```

`Core.Tests` absorbed `Execution.Tests`, which is now deleted. Every case it had is
here, asserting on the projection rather than on a temporary directory.

## Checklist for a new implementation

**Parser**

- [ ] Whitespace is space and tab only; a newline ends the program.
- [ ] String delimiters are matched longest first, and the delimiter count survives a
      round trip.
- [ ] Bare words are accepted in argument positions and in attribute values.
- [ ] The thirteen reserved words are rejected as bare words everywhere, exactly, with
      a message saying how to write one as text.
- [ ] `-flag` is a flag and `--flag` is a syntax error.
- [ ] A command's name may join identifiers with hyphens, and only where the hyphen is
      adjacent on both sides, so `ls -l` and `echo -5` are unchanged.
- [ ] The empty program is decided before the command list, so error positions are
      faithful.
- [ ] Errors carry a zero-based line and offset, and a mismatched closing tag is
      reported as a message.
- [ ] Serialising a parsed tree reproduces the input.
- [ ] Token kinds match the table in [Semantic tree](semantic-tree.md#tokenisation),
      including the function form's name as `command`, the operator words as `operator`
      and a member with its stop as `member`.
- [ ] `and` binds tighter than `or`, both are left associative, and `not` takes the
      whole comparison after it.
- [ ] An operand with no operator around it produces the operand's own node, not a
      wrapper.

**Runtime**

- [ ] Values expose distinct display and argument strings, and paths differ between
      them.
- [ ] Binding follows the four steps, including optional parameters filling
      positionally, and a parameter that collects the rest never takes the pipe.
- [ ] An expression binds only to a parameter declared as a predicate, and any other
      parameter refuses it.
- [ ] A predicate is evaluated per row in a child scope with `$row` bound, leaving any
      outer `row` alone.
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
- [ ] A table-shaped tag reads as a table wherever one is expected, gaps are `None`,
      and a tag that is not table-shaped names the child that broke the shape.
- [ ] Columns are typed from their cells, and a `None` cell does not make a column
      mixed.
- [ ] Comparisons are numeric when both sides read as numbers and ordinal otherwise, a
      comparison touching a gap is false, and two gaps are `eq`.
- [ ] `sort` is stable in both directions.
- [ ] `run` commits one transaction per line, skips blank and commented lines while
      counting them, and names the script and the line in a fault.
- [ ] `cd` on an argument that used an operator sets the view and leaves the folder
      alone; on a plain name it enters a folder, or the view a `view` record holds.
- [ ] `ls` in a view lists across folders, and its columns are the matching records'
      attributes rather than the whole store's.
- [ ] `up` clears the view before it moves, and undo restores the whole location.
- [ ] A record created while a view is set is created in the folder.
- [ ] A refresh is refused, before running, unless every stage names a read-only
      command, and commits nothing when it does run.

**Terminal**

- [ ] One command at a time, with the documented refusal message.
- [ ] Cancellation reports `Stopped.` and keeps the command's own output.
- [ ] Output changes are raised with the complete line set and an execution id.
- [ ] Completion follows the context table, offsets included.
- [ ] Wire formats match [Host interfaces](host-interfaces.md#wire-formats).
- [ ] A stored location carries its view, so a replay comes back into the query it was
      in.

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
| A pipeline in parentheses parses as an operand and evaluates to `A pipeline in parentheses is not a value yet`. | Intended for now. An operand is where it belongs, and what running one means is Phase 5's question. |
| Suggestion and completion chips are 26 pixels tall, below the usual 44 pixel touch target. | Known. Worth raising; the input, the run button and a table's cells already meet it. |
| `Scope` supports nesting, but no host creates a child scope outside a predicate. | Intended. The model is ahead of the shell. |
| `attr` is not marked read-only, so a live listing cannot be an `attr`. | Intended. The same command reads with no assignments and writes with them, and a refresh is decided by name before it runs. |
| Messages are English only and the client is published with invariant globalisation. | Intended for now; see [Design doc](design-doc.md#internationalisation). |

## Changing this specification

A change to any **must** in these documents is a language change. It needs a test in
the suite above that fails before the change and passes after, and an entry in the
table of known deviations if the implementation lags.
