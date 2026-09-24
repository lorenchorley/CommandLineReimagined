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
test methods, read from the suites by counting each `TestMethod` and `DataTestMethod`
attribute in the class; data-driven methods expand to more cases at run time, which the
second table counts from `dotnet test`.

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
| Value stages, the stop that must be followed by a name, the operator with nothing to compare with | `Parser.Tests/ValueStageTests` | 15 |
| Agreement with the retained GOLD parser | `Parser.Tests/ParserEquivalenceTests` | 4 |
| The two string forms of every value, and number formatting | `Core.Tests/ValueTests` | 17 |
| The Table value: coercion, columns, types, gaps, rows, display | `Core.Tests/TableTests` | 23 |
| Evaluating a predicate: members, comparison, gaps, boolean words | `Core.Tests/ExpressionTests` | 21 |
| Folding events, and that every event inverts back to where it started | `Core.Tests/ProjectionTests` | 13 |
| Path resolution over the projection, and kind inference | `Core.Tests/FilesTests` | 16 |
| Committing, undo, redo, history, replay determinism, blobs, the store's own checks on names and folders | `Core.Tests/StoreTests` | 28 |
| Binding, pipes, command forms, variables, tags, atomic lines, value stages, `$row` outside a predicate | `Core.Tests/ExecutionTests` | 51 |
| The help a wrong call carries and when it carries none (decision 0038), an old name leading to the new one, and a line with nothing to say carrying no notes | `Core.Tests/GuidanceTests` | 15 |
| File commands, attributes, saving tags, the name rule, renaming a folder with what it holds, and their undo | `Core.Tests/FileCommandTests` | 55 |
| Variables and their undo | `Core.Tests/VariableCommandTests` | 14 |
| Asynchronous commands, live output, cancellation | `Core.Tests/AsyncCommandTests` | 14 |
| `undo`, `redo`, `history` and `help` as commands, `help <command>`, and the nearest names an unknown command's note gives | `Core.Tests/MetaCommandTests` | 28 |
| What a line says it did to the log: the lines it committed, undid and redid | `Core.Tests/LogChangesTests` | 11 |
| The table functions, as whole command lines, the two predicate faults of decision 0033, what answers a predicate (decision 0034), and the explanation of an empty `where`: a column no row has, the values a column has, most frequent first and at most five, a span of numbers, a near value's fix, the part of an `and` that kept nothing, and when it is silent (decisions 0043, 0045) | `Core.Tests/TableCommandTests` | 64 |
| Views: `in` on a predicate, `ls` across folders, `out`, `find`, `save-view`, refreshing, and the bound check in each, a view read back from its record included; the explanation of an empty `find`, a view's listing and a live view's refresh | `Core.Tests/ViewTests` | 53 |
| `back` and the trail (decision 0037): each step further, views, after `out`, the start and the end of the trail, undo and redo, a folder that is gone, across a reload, the fold | `Core.Tests/BackTests` | 21 |
| A fix made a whole line: at a whole word only, dropped when it changes nothing, one line once, and a fault as a value without notes (decisions 0041, 0044) | `Core.Tests/FixTests` | 5 |
| An unknown command's suggestion, a fix for each command named, and a name nothing is near | `Core.Tests/CommandSuggestionTests` | 3 |
| The suggestions of the two predicate faults, worded alike, and an operand written twice offering no fix | `Core.Tests/PredicateSuggestionTests` | 5 |
| The nearest paths for every missing path (decision 0042): files, folders only for a folder, `rm`, a target directory, written from the current folder, a path under a missing folder, the fix in place, at most three, a script, a nested pipeline, `try`, a refresh | `Core.Tests/PathSuggestionTests` | 13 |
| The nearest variables: a slip, a start or another case, nothing near, and never `$row` | `Core.Tests/VariableSuggestionTests` | 4 |
| Recovery: `else`, `try`, `??`, nested pipelines, fault values and their members, `is-fault`, what a refresh refuses, recovery around a value stage | `Core.Tests/RecoveryTests` | 37 |
| XML documents: reading, text content, namespaces, refusals, writing, round trips, and the two commands | `Core.Tests/XmlTests` | 41 |
| CSV files: RFC 4180 reading, column typing, gaps, faults naming the line, writing, round trips, and the two commands | `Core.Tests/CsvTests` | 33 |
| The example programs, against their golden results, and `run`; the guide's examples and its chain of readmes | `Core.Tests/ExampleProgramTests` | 24 |
| The lexical rules: completion over the projection, the operators, the columns, the places and the keywords | `Core.Tests/CompletionTests` | 25 |
| Where the cursor is: the place for every line of the Phase 8 finding table | `Core.Tests/ContextTests` | 4 |
| Command names: after a pipe, not a path command after a table, by keyword, a short word only by a first keyword nobody else has, by edit distance, with descriptions | `Core.Tests/CommandCompletionTests` | 25 |
| Variables, `$row` only in a predicate, members by what a variable holds, tag types and attributes, the summaries | `Core.Tests/VariableCompletionTests` | 18 |
| Every parameter of every command by what it takes, flags, assignments, quoted paths, the signature, the pipe first after a complete stage (decision 0039) | `Core.Tests/ArgumentCompletionTests` | 51 |
| Inside a predicate: operands, operators, a column's values, `and` and `or`, and the pipe after a place and a value stage | `Core.Tests/PredicateCompletionTests` | 24 |
| What flows into a stage: the upstream run, refusals, the budget, the cache | `Core.Tests/ShapeTests` | 23 |
| What a tapped token is: variables, members, commands, operators, arguments | `Core.Tests/HoverTests` | 12 |
| The phase's acceptance list, from a fresh session | `Core.Tests/AcceptanceTests` | 10 |
| Replaying a log, seeding once, `reset`, giving an older log the guide once, and bringing the seeded files nobody changed to the seed (decision 0040) | `Core.Tests/PersistenceTests` | 29 |
| The stored shape of a transaction, every event and value case, the trail's events, versioning up to 3 | `Web.Core.Tests/LogFormatTests` | 27 |
| The browser's IndexedDB module, including a browser without it | `tools/store-check.mjs` | 20 |
| DTO shapes including tables, views, refreshing, caught faults, documents, streaming, cancellation, completion, tokens, the guide, and no notes on a line with nothing to say | `Web.Core.Tests/TerminalSessionTests` | 55 |
| A parse error in words: the phrase table, the explanations, the sentence carried with the parse | `Web.Core.Tests/ParseWordingTests` | 14 |
| The hover record as the page receives it | `Web.Core.Tests/DescribeTests` | 7 |
| Path and naming helpers | `Terminal.Tests/ValidCommandTests` | 2 |
| The published page, in a browser at phone size | `tools/browser-check.mjs` | 1 session |

Cases actually run, which is what the suite reports:

| Project | Cases |
| --- | --- |
| `Parser.Tests` | 375 |
| `Core.Tests` | 881 |
| `Web.Core.Tests` | 143 |
| `Terminal.Tests` | 32 |
| Total | 1431 |

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
- [ ] A variable reference, with or without members, may stand as a stage, and takes
      no arguments.
- [ ] A stop after a variable is followed by a name, or the parse fails there and says
      a column name belongs after it.
- [ ] A comparison operator with nothing after it fails where the operand should be,
      and says what the operator needs.

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
- [ ] An unknown name is reported as a fault of kind `UnknownCommand`,
      `Unknown command : <name>`, and the commands near it, at most three, are named in
      a suggestion with a fix for each, not in the message.
- [ ] A value stage answers the variable's value, ignores its input, and counts as
      read-only for a refresh.
- [ ] `$row` read outside a predicate is a `NotFound` fault that says where it exists;
      any other unknown variable is `Unknown variable: $name`.
- [ ] A predicate that wrote an operator and never reads `$row` is a `Binding` fault
      when it is bound, whose note suggests its bare words as columns.
- [ ] A predicate's value for an item is `true`, `false` or absent, or the stage fails
      with an `Invalid` fault on the first item that answers anything else.
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
- [ ] `in` on an argument that used an operator sets the view and leaves the folder
      alone; on a plain name it enters a folder, or the view a `view` record holds.
- [ ] `ls` in a view lists across folders, and its columns are the matching records'
      attributes rather than the whole store's.
- [ ] `out` clears the view before it moves, and undo restores the whole location.
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
- [ ] Every move `in` or `out` makes pushes the place it leaves on the trail before the
      move; a move to where you are emits nothing.
- [ ] `back` walks the trail from the top, passes over where you are and a place whose
      folder is gone, pops every place it passed and the one it takes, and with nowhere
      to go answers `Nowhere further back: you are in <where>` and commits nothing.
- [ ] `TrailPushed` and `TrailPopped` invert to each other, and folding a pop of a
      place that is not on the trail changes nothing.
- [ ] An unknown name that is a keyword names the commands it is a keyword of before
      any slip is looked for, and a name of one or two letters is a keyword only when it
      is a command's first and no other command's.
- [ ] A line that fails with a `Binding` fault whose message begins with its stage's
      command name carries that command's help as its guide, and no other line does.
- [ ] On load, a seeded file no undoable transaction has named, whose content differs
      from the seed's, is given the seed's content, all of them in one `seed update`
      transaction nobody can undo, and a load with nothing to change commits nothing.
- [ ] No fault's message holds a suggestion. A note is never part of a value: a fault
      that `try` or `else` makes a value has no notes at any depth, a stored fault has
      none, and `else`, `try`, `??` and pipes behave as they would without them.
- [ ] A line that succeeded carries the notes of the stages whose work stood, in order,
      and none from a stage or branch that `try` or `else` rolled back; a failed line
      carries its fault's notes only.
- [ ] A missing file, record, folder or target folder, and an unknown variable other
      than `$row`, are named with the nearest, first within the distance in the folder
      looked in, then the same name or a name it begins anywhere, folders only for a
      folder, at most three, each path written from the current folder.
- [ ] Every fix leaves the session as a whole line of the typed line: a replacement at
      the first place that holds it as a whole word, dropped when there is none, when
      it changes nothing, and when it repeats an earlier line; a fault's replacement
      whose text is in the line more than once is dropped.
- [ ] When `where`, `find` or a view's listing keeps no row of a table that had some, it
      carries one explanation: the first column read through `$row.` that no row has,
      with the nearest; or else the first `and` operand that keeps nothing on its own,
      `<column> is <values>` for `eq` and `like`, at most five then `or <n> more`, most
      frequent first, `runs from … to …` for an ordering of numbers; and nothing for
      `ne`, `has`, `or`, `not`, an empty table in, or a filter that keeps a row.
- [ ] An `eq` explanation whose value, written in the line, is near a value the column
      has offers the nearest as a fix; a value in a variable offers none.

**Terminal**

- [ ] One command at a time, with the documented refusal message.
- [ ] Cancellation reports `Stopped.` and keeps the command's own output.
- [ ] Output changes are raised with the complete line set and an execution id.
- [ ] Completion reads the place by parsing, answers each place by its own rule,
      replaces from `start` to `end`, and a newer request cancels the one before it.
- [ ] After a stage with every required argument written, and an empty word, the
      first item is `|`, detail `send the result on`, and the only one when the stage
      has nothing left to take; a stage still missing a required argument offers no
      pipe.
- [ ] What flows into a stage is learned by previewing the upstream read-only, under
      the budget, and a miss answers the current folder's columns rather than an error.
- [ ] A tapped token is described by `Describe`, and a parse error carries a sentence
      that names no grammar label.
- [ ] Wire formats match [Host interfaces](host-interfaces.md#wire-formats).
- [ ] A stored location carries its view, so a replay comes back into the query it was
      in.
- [ ] The stored log carries a version, 3 today, and versions 1 to 3 are read; an
      unknown version is refused by name, a transaction that cannot be decoded is
      skipped and counted, and a host without storage runs in memory and says so before
      the first command.
- [ ] A failed line's `guide` is drawn under its error, set apart from output, and a
      line without one draws no panel.
- [ ] A response carries `notes`, each with a `kind`, a `text` and `fixes` that are
      whole lines, null when there are none, and a refresh carries them as an execution
      does.
- [ ] Each note is drawn under its line, after the answer or error and before the guide,
      labelled by its kind, with a chip per fix that fills the input with the caret at
      its end and runs nothing; a live listing redraws its notes with its table.
- [ ] Notes, the guide, the banner, `copied` and the restore messages share one guidance
      style that no result or error has.
- [ ] The page marks `body` with `data-ready` once it can run lines, keeps any number
      of listings live, copies a selection made in the scrollback, and walks the
      history with ↑ and ↓ as the keys do.

## Known deviations

Where the implementation and this specification do not meet, with the decision in each
case.

| Deviation | Status |
| --- | --- |
| Property assignments parse but raise `Cannot evaluate a child PropertyAssignment.` | Intended for now. The grammar keeps them because the original did; evaluation has no meaning to give them yet. |
| `in` and `ls` name a missing folder as it was written (`Directory does not exist : nowhere`), where other commands name the resolved path. | Intended. What you need to see after a failed `in` is your own spelling; `FilesTests.AMissingFolderIsNamedAsItWasWritten` pins it. |
| `run` shows its last line's result twice: once as that line's output, and once as the result of `run`. | Intended. The output is the script's transcript, and the result is what `run` answers, which a pipe after it receives. |
| `??` defaults a stage that answered nothing, not one that failed, so `echo $missing ?? x` is a `NotFound` fault. | Intended. A missing variable is a failure, and recovering from failure is `else`'s job ([decision 0014](../decisions/0014-recovery-operator.md)). |
| A reserved word in argument position is described as an operator even when it is `try` or `else`, as in `'try' is an operator; write "try" to pass it as text`. | Cosmetic. The advice is right for all thirteen words. |
| The retained GOLD parser reports column 12 where the combinator parser reports 11 on one truncated input. | Documented in `ParserEquivalenceTests`. The combinator position is correct. |
| A pipeline in parentheses inside a predicate runs once, and the predicate keeps its value, so a view saved with one does not re-run it. | Intended. A view is a question about records, and a question that changed its own terms each time it was asked would be a different question. |
| `vars` holds each value itself in its `value` column, not the one-line summary completion gives the same variable. | Intended. The owner kept `vars` as a table of the values themselves on 2026-09-23; the summaries are completion's and hover's. |
| `Scope` supports nesting, but no host creates a child scope outside a predicate. | Intended. The model is ahead of the shell. |
| `attr` is not marked read-only, so a live listing cannot be an `attr`. | Intended. The same command reads with no assignments and writes with them, and a refresh is decided by name before it runs. |
| XML element text is read as an attribute `text` and written back as content, so mixed content comes back with its text gathered before the children, trimmed; an XML attribute called `text` on an element with content is replaced by it; comments and processing instructions are dropped. | Intended for this release; see [decision 0025](../decisions/0025-xml-text-content.md). |
| A document with a DTD is refused rather than read. | Intended. A file in the store is anyone's, and entity expansion is a way to stop a tab. |
| Messages are English only and the client is published with invariant globalisation. | Intended for now; see [Design doc](design-doc.md#internationalisation). |
| A response with no notes carries `"notes":null`, as it carries `"guide":null`. `TerminalSession` passes null (`Web.Core/TerminalSession.cs`), and the bridge serialises with the web defaults, which write nulls. | Intended. A page reading `r.notes \|\| []` sees no difference, and `notes` follows `guide`; the Phase 10 plan's wire now says null. |

## Changing this specification

A change to any **must** in these documents is a language change. It needs a test in
the suite above that fails before the change and passes after, and an entry in the
table of known deviations if the implementation lags.
