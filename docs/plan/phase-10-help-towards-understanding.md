# Phase 10: from a failure to the fix

**Goal.** A failure or an empty answer says what was probably meant, and offers it as
something to tap. Everything the terminal says of its own, rather than what a command
answered, looks unmistakably like the terminal talking.

**Status: planned, after Phase 9.** Chosen by the owner on 2026-09-24 from a list of
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

- **0040. What the terminal says of its own is drawn apart from output.** Output is
  what a command answered: a table, a file's text, a value. Everything else (a fault's
  suggestions, a fix, the help under a wrong call, an explanation of an empty answer,
  the banner, `copied`) is guidance, drawn in one style of its own: an accent-bordered
  panel with a label, in the interface face rather than the terminal's monospace. A
  script never sees guidance: it is not in a value, and `else`, `try` and pipes are
  unaffected.
- **0041. A missing file or column names the nearest ones.** `read notes` fails as it
  does now, and the response names the nearest paths anywhere in the filesystem:
  first a name in the current folder within the same edit distance `Nearest` uses for
  commands, then the same name or a name it starts in any folder. `$row.knd` in a
  predicate, where no row has `knd`, is explained with the nearest columns (see 0042).
- **0042. An empty filter explains itself.** When `where`, `find` or a view keeps no
  row of a table that had some, the response carries an explanation: a column the
  predicate read that no row has (`No row has knd; did you mean kind?`), or, for a
  comparison with a column that exists, the values that column does have, most
  frequent first, at most five (`kind is folder or text`). The answer is still the
  empty table; the explanation is guidance.
- **0043. A fault may carry fixes, which the page offers as chips.** A fix is a whole
  corrected line. The unknown command, the predicate that never reads `$row`, the one
  that is not true or false, the missing file with a near name, and the unknown
  variable each supply one where they can. Tapping one puts it in the input without
  running it.

## Wire

In 10.0: `Session.Response` gains `Notes: Note list`, a note being
`{ Kind: string; Text: string; Fixes: string list }` (kinds `suggestion`, `explanation`),
and `ExecutionResponse` gains `notes`. Phase 9's `guide` stays as it is: it is one more
thing drawn as guidance.

## Streams

After 10.0, three in parallel.

- **A. Suggestions and fixes (core).** G1, G3's core side. The nearest paths for a
  missing file and a missing folder; the nearest variables for an unknown one; the
  fixes of 0043 as notes. **Owns** `Core/Nearest.fs` (paths), the fault constructors
  in `Core/Faults.fs` that gain suggestions, and the notes the session builds from
  them. Tests in a new `SuggestionTests.fs`.
- **B. Empty answers (core).** G2. The explanation of 0042 from `where`, `find` and
  views, built from the table and the predicate's tree. **Owns** the explanation in
  `Core/Commands/Tables.fs` and `Core/Expr.fs`. Tests in `TableCommandTests.fs` and
  `ViewTests.fs`.
- **C. The page.** G3's page side and G4: notes drawn as guidance panels under the
  line they belong to, each fix a chip that fills the input with the caret at the end;
  one guidance style (0040) for notes, the guide, the banner and `copied`, and a
  browser check that no guidance element is styled as output. **Owns**
  `WebClient/wwwroot/index.html`, `tools/browser-check.mjs`.

Then 10.9: the user documents, the guide in `guide/` and the specification, by two
sub-agents as in Phase 9, the verifier and the republish.

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
| 10.0 Foundation | orchestrator | not started | |
| A. Suggestions and fixes | stream agent | not started | |
| B. Empty answers | stream agent | not started | |
| C. The page | stream agent | not started | |
| 10.9 docs, spec, verifier, republish | sub-agents, orchestrator | not started | |
