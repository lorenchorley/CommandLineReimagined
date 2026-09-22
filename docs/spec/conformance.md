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
| Lexical rules: identifiers, accents, flags, string forms, variables | `Parser.Tests/LexicalTests` | 16 |
| Bare words, in arguments and in tag attributes; assignments; negative numbers | `Parser.Tests/BareWordTests` | 19 |
| The three command forms, arguments, pipes, hyphenated command names | `Parser.Tests/CommandFormTests` | 26 |
| Object and component tags, nesting, closing forms, variable tags | `Parser.Tests/ObjectInstanceTests` | 17 |
| Productions the original grammar never implemented | `Parser.Tests/CompletedGrammarTests` | 17 |
| Error positions and expected symbols | `Parser.Tests/SyntaxErrorTests` | 5 |
| Round-trip serialisation | `Parser.Tests/SerialisationTests` | 9 |
| Word operators, precedence, member access, reserved words, nested pipelines, the expression entry point | `Parser.Tests/ExpressionTests` | 21 |
| `else`, `try`, `??`, pipelines in parentheses as stages and operands, the adjacent function parenthesis, reserved command names | `Parser.Tests/RecoveryTests` | 23 |
| Agreement with the retained GOLD parser | `Parser.Tests/ParserEquivalenceTests` | 4 |
| The two string forms of every value, and number formatting | `Core.Tests/ValueTests` | 17 |
| The Table value: coercion, columns, types, gaps, rows, display | `Core.Tests/TableTests` | 23 |
| Evaluating a predicate: members, comparison, gaps, boolean words | `Core.Tests/ExpressionTests` | 21 |
| Folding events, and that every event inverts back to where it started | `Core.Tests/ProjectionTests` | 13 |
| Path resolution over the projection, and kind inference | `Core.Tests/FilesTests` | 16 |
| Committing, undo, redo, history, replay determinism, blobs, the store's own checks on names and folders | `Core.Tests/StoreTests` | 28 |
| Binding, pipes, command forms, variables, tags, atomic lines | `Core.Tests/ExecutionTests` | 43 |
| File commands, attributes, saving tags, the name rule, renaming a folder with what it holds, and their undo | `Core.Tests/FileCommandTests` | 55 |
| Variables and their undo | `Core.Tests/VariableCommandTests` | 14 |
| Asynchronous commands, live output, cancellation | `Core.Tests/AsyncCommandTests` | 14 |
| `undo`, `redo` and `history` as commands, including what each compensation reverses | `Core.Tests/MetaCommandTests` | 17 |
| The table functions, as whole command lines | `Core.Tests/TableCommandTests` | 32 |
| Views: `cd` on a predicate, `ls` across folders, `up`, `find`, `save-view`, refreshing | `Core.Tests/ViewTests` | 40 |
| Recovery: `else`, `try`, `??`, nested pipelines, fault values and their members, `is-fault`, what a refresh refuses | `Core.Tests/RecoveryTests` | 33 |
| XML documents: reading, text content, namespaces, refusals, writing, round trips, and the two commands | `Core.Tests/XmlTests` | 41 |
| CSV files: RFC 4180 reading, column typing, gaps, faults naming the line, writing, round trips, and the two commands | `Core.Tests/CsvTests` | 33 |
| The example programs, against their golden results, and `run` | `Core.Tests/ExampleProgramTests` | 22 |
| Completion over the projection, the operators, the columns, the places and the keywords | `Core.Tests/CompletionTests` | 25 |
| The phase's acceptance list, from a fresh session | `Core.Tests/AcceptanceTests` | 10 |
| Replaying a log, seeding once, and `reset` | `Core.Tests/PersistenceTests` | 13 |
| The stored shape of a transaction, every event and value case, versioning | `Web.Core.Tests/LogFormatTests` | 24 |
| The browser's IndexedDB module, including a browser without it | `tools/store-check.mjs` | 20 |
| DTO shapes including tables, views, refreshing, caught faults, documents, streaming, cancellation, completion, tokens | `Web.Core.Tests/TerminalSessionTests` | 50 |
| Path and naming helpers | `Terminal.Tests/ValidCommandTests` | 2 |
| The published page, in a browser at phone size | `tools/browser-check.mjs` | 1 session |

Cases actually run, which is what the suite reports:

| Project | Cases |
| --- | --- |
| `Parser.Tests` | 342 |
| `Core.Tests` | 561 |
| `Web.Core.Tests` | 74 |
| `Terminal.Tests` | 32 |
| Total | 1009 |

Run them with:

```bash
for p in $(find . \( -name '*.Tests.csproj' -o -name '*.Tests.fsproj' \) | sort); do
  dotnet test "$p" -c Release
done
```

`Core.Tests` absorbed `Execution.Tests`, which is deleted, as is the C# `Commands`
project it tested. Every case it had is here, asserting on the projection rather than on
a temporary directory.

`EntityComponentSystem.Tests`, `Rendering.Tests`, `Utils.Tests` and
`SourceGenerators.Tests` cover the desktop shell's libraries, which this specification
does not govern. They run in CI with the rest.

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
- [ ] An unknown name is reported as a fault of kind `UnknownCommand`.
- [ ] A pipeline threads values and stops at the first failure.
- [ ] Tags evaluate as values, as pipeline stages and as arguments, and bind their
      variable in every position.
- [ ] A command returns a value and events and changes nothing itself; a later stage
      reads a projection with the earlier stages' events folded in.
- [ ] A line commits one transaction when it succeeds, nothing when it fails, and
      nothing when it produced no events.
- [ ] Every event inverts to where it started; `undo` appends the last undoable line's
      events inverted and reversed, and names the line; `redo` compensates the
      compensation; the seed is recorded and cannot be undone.
- [ ] Replaying a log into an empty store arrives at exactly the projection it left.
- [ ] Content is stored by the SHA-256 of its text, and a command reaches it only
      through put and get.
- [ ] An asynchronous command observes cancellation, and a cancelled line reports
      `Stopped.` and commits nothing.
- [ ] `else` runs its right side only when the left failed, with the fault piped in;
      `try` makes a stage's fault its value; `??` defaults a stage that answered `None`
      or nothing; neither `else` nor `try` catches Stop, and a failed branch leaves no
      events.
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
- [ ] `from-xml` reads an element's attributes by the number-or-text rule and its
      text into an attribute `text`, refuses a DTD, and names the line and position of
      a document that does not parse.
- [ ] `from-csv` types a column as numbers only when every cell present is one, reads
      an unquoted empty field as `None` and `""` as empty text, and names the line of
      a record whose width differs from the header's.
- [ ] `to-xml` and `to-csv` emit the events `write` would, set the kind of a file they
      create, write numbers exactly, and end every line in `\n`.

**Terminal**

- [ ] One command at a time, with the documented refusal message.
- [ ] Cancellation reports `Stopped.` and keeps the command's own output.
- [ ] Output changes are raised with the complete line set and an execution id.
- [ ] Completion follows the context table, offsets included.
- [ ] Wire formats match [Host interfaces](host-interfaces.md#wire-formats).
- [ ] A stored location carries its view, so a replay comes back into the query it was
      in.
- [ ] The stored log carries a version; an unknown version is refused by name, a
      transaction that cannot be decoded is skipped and counted, and a host without
      storage runs in memory and says so before the first command.

## Known deviations

Where the implementation and this specification do not meet, with the decision in each
case.

| Deviation | Status |
| --- | --- |
| Property assignments parse but raise `Cannot evaluate a child PropertyAssignment.` | Intended for now. The grammar keeps them because the original did; evaluation has no meaning to give them yet. |
| `cd` and `ls` name a missing folder as it was written (`Directory does not exist : nowhere`), where other commands name the resolved path. | Intended. What you need to see after a failed `cd` is your own spelling; `FilesTests.AMissingFolderIsNamedAsItWasWritten` pins it. |
| `run` shows its last line's result twice: once as that line's output, and once as the result of `run`. | Intended. The output is the script's transcript, and the result is what `run` answers, which a pipe after it receives. |
| `??` defaults a stage that answered nothing, not one that failed, so `echo $missing ?? x` is a `NotFound` fault. | Intended. A missing variable is a failure, and recovering from failure is `else`'s job ([decision 0014](../decisions/0014-recovery-operator.md)). |
| A reserved word in argument position is described as an operator even when it is `try` or `else`, as in `'try' is an operator; write "try" to pass it as text`. | Cosmetic. The advice is right for all thirteen words. |
| The retained GOLD parser reports column 12 where the combinator parser reports 11 on one truncated input. | Documented in `ParserEquivalenceTests`. The combinator position is correct. |
| A pipeline in parentheses inside a predicate runs once, and the predicate keeps its value, so a view saved with one does not re-run it. | Intended. A view is a question about records, and a question that changed its own terms each time it was asked would be a different question. |
| Suggestion and completion chips are 26 pixels tall, below the usual 44 pixel touch target. | Known. Worth raising; the input, the run button and a table's cells already meet it. |
| `Scope` supports nesting, but no host creates a child scope outside a predicate. | Intended. The model is ahead of the shell. |
| `attr` is not marked read-only, so a live listing cannot be an `attr`. | Intended. The same command reads with no assignments and writes with them, and a refresh is decided by name before it runs. |
| XML element text is read as an attribute `text` and written back as content, so mixed content comes back with its text gathered before the children, trimmed; an XML attribute called `text` on an element with content is replaced by it; comments and processing instructions are dropped. | Intended for this release; see [decision 0025](../decisions/0025-xml-text-content.md). |
| A document with a DTD is refused rather than read. | Intended. A file in the store is anyone's, and entity expansion is a way to stop a tab. |
| Messages are English only and the client is published with invariant globalisation. | Intended for now; see [Design doc](design-doc.md#internationalisation). |

## Changing this specification

A change to any **must** in these documents is a language change. It needs a test in
the suite above that fails before the change and passes after, and an entry in the
table of known deviations if the implementation lags.
