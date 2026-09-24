# Phase 10: from a failure to the fix

**Goal.** A failure or an empty answer says what was probably meant, and offers it as
something to tap. Everything the terminal says of its own, rather than what a command
answered, looks unmistakably like the terminal talking.

**Status: in progress; 10.0 is built, streams A, B and C are next.** Chosen by the owner on 2026-09-24 from a list of
suggestions. It builds on Phase 9's `guide` and its distinct panel (R13), and is run
as [running.md](running.md) describes.

## Requests

| # | The owner chose | Where |
| --- | --- | --- |
| G1 | "Did you mean" for files and columns, not only commands | A |
| G2 | An empty answer explains itself | B |
| G3 | A fix named by an error is a chip that puts the corrected line in the input | A, C |
| G4 | Anything that is not the command's intended output is visually very different | C |

## Decision records

Written in 10.0, all Accepted: the owner's choices of 2026-09-24.

- [0041](../decisions/0041-guidance-is-drawn-apart-from-output.md). What the terminal
  says of its own is drawn apart from output. A fault's message loses its
  `Did you mean …?`, which becomes a note; a script never sees a note.
- [0042](../decisions/0042-a-missing-name-names-the-nearest.md). A missing file, folder,
  variable or column names the nearest ones: first a name in the current folder within
  `Nearest`'s distance, then the same name, or a name it starts, anywhere. At most three.
- [0043](../decisions/0043-an-empty-filter-explains-itself.md). When `where`, `find` or a
  view keeps no row of a table that had some, one explanation: a column the predicate
  reads that no row has (`No row has knd; did you mean kind?`), or else the values the
  compared column does have, most frequent first, at most five (`kind is folder or text`).
- [0044](../decisions/0044-a-fault-may-carry-fixes.md). A fix is a whole corrected line,
  offered as a chip that fills the input and does not run it.

## Wire

Built in 10.0, so that A, B and C code against it from the start.

- `Core/Faults.fs`: `Note = { Kind: string; Text: string; Fixes: Fix list }`, kinds
  `suggestion` and `explanation`, made with `Note.suggestion` and `Note.explanation`. A
  `Fix` is `Fix.Line line` or `Fix.Replace(written, corrected)`: whatever finds a
  mistake rarely knows the line, so it says the fix as a replacement of what was
  written, and `Note.resolve source` makes each a whole `Fix.Line` of the typed line,
  dropping one the line has no whole-word place for (`Fix.apply`).
- `Fault` gains `Notes: Note list` (`Fault.withNotes`). `Fault.asValue` strips them, at
  any depth, and the evaluator uses it wherever `try` or `else` makes a fault a value.
- `CommandResult` gains `Notes: Note list` (`Invocation.withNotes` adds to a result);
  the evaluator gathers the notes of the stages whose work stood (a stage a `try` or an
  `else` rolled back takes its notes with it) into `Execution.Notes`.
- `Session.Response` gains `Notes`: a failed line's are its fault's, a successful
  line's are its stages', every fix resolved against `Source`. `Refresh` does the same,
  so a live view carries its explanation.
- `ExecutionResponse` gains `IReadOnlyList<NoteInfo>? Notes`, serialised as `notes`,
  absent when there are none; `NoteInfo(Kind, Text, Fixes)` with whole lines. Phase 9's
  `guide` is unchanged, and is one more thing drawn as guidance.
- `tools/transcript.fsx` prints each note after the answer, as `  suggestion: …` or
  `  explanation: …`, and each fix as `  fix: …`.

## Checkpoint 10.0: foundation

The orchestrator, before anything else: the records and their index rows, the wire
above, `Core.Tests/SuggestionTests.fs` registered with the `Fix` tests, a test in
`ExecutionTests.fs` and one in `Web.Core.Tests` that a line with nothing to say has no
notes, and this document's breakdown. **Done when** every suite passes and the browser
check passes.

## Parallel streams

After 10.0, A, B and C start together from its commit.

### A. Suggestions and fixes (core)

**Closes** G1 for files, folders and variables, G3's core side. **Size** M.

- 0041: `Did you mean …?` leaves the messages of `unknownCommand`, `neverReadsTheRow` and
  `notTrueOrFalse` (and the hint `truth` builds in `Core/Expr.fs`) and becomes a
  `suggestion` note on the fault, with its fixes: `lss` gives `Fix.Replace("lss", "ls")`,
  one per command named; `ls | where kind eq folder` gives the predicate with `$row.`
  before its bare words; a predicate that is not true or false gives the comparison or
  the column it suggests. The messages that are left are the error reference's, less
  the suggestion.
- 0042: a `File does not exist` or `Directory does not exist` fault raised for a path
  someone wrote gets a `suggestion` naming the nearest paths (current folder within
  `Nearest.threshold`, then the same name or a name it starts, anywhere; folders only
  for a folder; at most three), `Did you mean documents/notes.txt?`, with a fix that
  writes each in place of the path written. Paths are written relative to the current
  folder where they are inside it, absolute otherwise. An unknown variable gets the
  nearest variables the same way. Where the projection is needed and the fault
  constructor cannot see it, the session adds the note from the fault's kind and path
  (`Fault.Path` is the absolute path, or `$name`), using the projection it already has.
- `Nearest` gains the path and variable helpers; `Nearest.commands` is unchanged.
- **Tests** in `SuggestionTests.fs`, after `FixTests`: every acceptance line of A's; a
  missing file with nothing near says nothing more; a missing folder offers only
  folders; a nested or `run` mistake is suggested and offers no fix; `try read notes`
  gives a `$problem` with no notes; every existing test that read a `Did you mean` in a
  message now reads it in the note.
- **Owns** `Core/Nearest.fs`, the suggestion-bearing constructors in `Core/Faults.fs`
  (`unknownCommand`, `neverReadsTheRow`, `notTrueOrFalse`, `unknownVariable`,
  `fileDoesNotExist`, `directoryDoesNotExist`), the notes the session adds in
  `Core/Session.fs`, `Core.Tests/SuggestionTests.fs`. **Touches** the call sites of those
  constructors (`Core/Expr.fs` at `truth` and the `neverReadsTheRow` check,
  `Core/Evaluator.fs`, `Core/Commands/Meta.fs`, `Core/Binder.fs`), and the existing tests
  that assert the old messages (`ExecutionTests.fs`, `ExpressionTests.fs`,
  `CommandCompletionTests.fs` and any other), changing only those assertions.

### B. Empty answers (core)

**Closes** G2, and G1 for columns. **Size** M.

- 0043: `Expr.explainEmpty` (or the name B chooses), given the table the filter read
  and the predicate, answers at most one `explanation` note: the columns the predicate
  reads through `$row.` that no row has, nearest columns by `Nearest.names`, text
  `No row has knd; did you mean kind?` (or `No row has knd.` with none near) and a
  `Fix.Replace("$row.knd", "$row.kind")`; else, for an `eq` comparison of an existing
  column with a constant no row has, `kind is folder or text`: the column's distinct
  values, most frequent first, ties in first-seen order, at most five, joined `, … or`.
  B decides and documents the wording for more than two values and for a comparison
  other than `eq`, and keeps a predicate it cannot explain silent.
- `where` in `Core/Commands/Tables.fs`, and `listMatching` in `Core/Commands/Files.fs`
  (which `find` and a view's listing share), add the note with `Invocation.withNotes`
  when the table had rows and none is kept.
- **Tests** in `TableCommandTests.fs` (where) and `ViewTests.fs` (find, a view, a live
  view's refresh), through the session so the fix is a whole line: the two acceptance
  lines; an empty table in says nothing; a filter that keeps a row says nothing; a
  column some rows have is not "no row has"; `try` and `else` see the empty table as
  before.
- **Owns** the explanation in `Core/Expr.fs` (new functions, after `truth`) and the
  `where` in `Core/Commands/Tables.fs`, tests in `TableCommandTests.fs` and
  `ViewTests.fs`. **Touches** `listMatching` in `Core/Commands/Files.fs`, one call.

### C. The page

**Closes** G3's page side and G4. **Size** M.

- Notes (`r.notes`) are drawn under the line they belong to, after the answer or the
  error and before any guide, each as a guidance panel labelled by its kind
  (`did you mean` for a suggestion, `why` for an explanation, or C's own short words),
  with its text and a chip per fix. Tapping a chip puts the line in the input with the
  caret at the end, and does not run it or open the keyboard, as the palette keys do.
  A live listing's refresh redraws its notes with it.
- 0041: one guidance style for notes, the guide under a wrong call, the banner and the
  `copied` note: the Phase 9 `.aside` panel, an accent border and background, a small
  label, the interface face. Output (results, tables, text, the red error) keeps its
  look.
- **Tests** in `tools/browser-check.mjs`: `read notes` draws a note with the chip
  `read documents/notes.txt`, and tapping it fills the input without running;
  `ls | where $row.kind eq foldr` draws the explanation; every note, the guide, the
  banner and `copied` have the guidance style (font family, border) and no result or
  error has it. Until A and B merge, the core sends no notes for these lines, so C tests
  the drawing against a response it stubs in the check, and the orchestrator switches
  the checks to real lines at integration.
- **Owns** `WebClient/wwwroot/index.html`, `tools/browser-check.mjs`.

## Who touches what

| File | 10.0 | A | B | C | 10.9 |
| --- | --- | --- | --- | --- | --- |
| `docs/decisions/` | own | | | | index |
| `Core/Faults.fs` | wire | the constructors named in A | | | |
| `Core/Nearest.fs` | | own | | | |
| `Core/Session.fs` | wire | notes from faults | | | |
| `Core/Expr.fs` | | `truth`, the `$row` check | explanation | | |
| `Core/Commands/Tables.fs` | wire | | `where` | | |
| `Core/Commands/Files.fs` | wire | | `listMatching` | | |
| `Core/Evaluator.fs`, `Core/Binder.fs`, `Core/Commands/Meta.fs` | wire | call sites | | | |
| `Web.Core/` | wire | | | | |
| `Core.Tests/SuggestionTests.fs` | `FixTests` | own | | | |
| `Core.Tests/TableCommandTests.fs`, `ViewTests.fs` | | | own tests | | |
| other `Core.Tests/` files | wire test | message assertions | | | |
| `WebClient/wwwroot/index.html`, `tools/browser-check.mjs` | | | | own | real lines |
| `tools/transcript.fsx` | notes | | | | |
| `docs/` user documents, `guide/`, `docs/spec/` | | | | | own |

## Running it

1. **10.0: the orchestrator.**
2. **A, B and C: three sub-agents, launched together,** each in its own worktree from
   the 10.0 commit. Merge order when several are waiting: A, then B, then C. A and B
   both touch `Core/Expr.fs`, in different functions.
3. **10.9: integration.** The orchestrator switches the browser check to real lines and
   runs the acceptance on the merged head. Then two sub-agents in parallel, as in
   Phase 9: *docs* (the user documents in `docs/` and the guide in `guide/`: notes,
   fixes, explanations, the one guidance style) and *spec* (`docs/spec/`: the error
   reference without `Did you mean`, notes in the execution model and the host
   interface, conformance counts), each pasting output from `tools/transcript.fsx`.
   Then a verifier over [Acceptance](#acceptance), the republish, "As built" and the
   status lines.

## Acceptance

| Line | Does |
| --- | --- |
| `read notes` in `/` | the fault, a suggestion naming `documents/notes.txt`, and a fix chip `read documents/notes.txt` |
| `ls | where $row.knd eq folder` | the empty table, and `No row has knd; did you mean kind?` with the fix `ls | where $row.kind eq folder` |
| `ls | where $row.kind eq foldr` | the empty table, and `kind is folder or text` |
| `lss` | the fault, and a fix chip `ls` |
| `ls | where kind eq folder` | the fault, and a fix chip `ls | where $row.kind eq folder` |
| `echo $fles` after `ls | set files` | the fault, and a fix chip `echo $files` |
| tap a fix chip | the corrected line in the input, not run |
| any note, the guide, the banner | drawn as guidance, not as output |

## Progress

| Work | Done by | State | Commit |
| --- | --- | --- | --- |
| 10.0 Foundation | orchestrator | merged | b3e8f9f |
| A. Suggestions and fixes | stream agent | in progress | |
| B. Empty answers | stream agent | in progress | |
| C. The page | stream agent | merged | 96b42b2 |
| 10.9 docs, spec, verifier, republish | sub-agents, orchestrator | not started | |
