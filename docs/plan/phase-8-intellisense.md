# Phase 8: intellisense

**Goal.** While a line is being typed, the terminal knows what the word under the
cursor can be and says so. It knows which command and parameter the word belongs to,
the value that will flow into that stage, and what a variable holds. A line that goes
wrong says how to put it right, in words rather than grammar symbols.

**Status: planned, ready to run.** Written from an audit of the page and the core on
2026-09-22. The audit ran about fifty partial lines through `Session.Complete` and
through the session itself. What they did is recorded against each finding below.
Its decisions are accepted, and nothing in it waits on the owner. It is run as
[running.md](running.md) describes, with the layout in [Running it](#running-it) and
the state in [Progress](#progress).

## Why

Most of what follows comes from one design choice. `Core/Completion.fs` guesses from
the text: it takes the last word, checks whether there is a `$` earlier in the stage,
and otherwise offers the files in the current folder. It never asks the parser or the
binder where the cursor is, although both already know. So `ls | sort ` offers file
names, `$v.` offers a listing's columns for a number, and `$row` is offered where it
can only fail.

This phase replaces the guess with a reading of the line (decision 0031, below). It
then gives each kind of place its own provider, so that the providers can be built in
parallel.

## Findings

Every finding is numbered, and each workstream names the ones it closes. "Now" is
what the audit saw.

| # | Line | Now | Wanted | Stream |
| --- | --- | --- | --- | --- |
| 1 | `$` | Chips show names only | Each chip says what it holds: `$files · table, 4 rows` | C |
| 2 | `echo $` | `$row` offered | `$row` only inside a predicate argument | C |
| 3 | `$v`, `$row` run alone | `Syntax error at column 0: expected end of input, identifier, (, <, <$, try, {.` | `$v` shows its value (0032); `$row` outside a predicate is explained | B |
| 4 | `echo $row` | `Unknown variable : $row` | Says where `$row` exists, with an example | B |
| 5 | `$v.`, `$problem.`, `$files.` | The current folder's listing columns | Members of the value held: a table's columns, a fault's fields, nothing for a number | C |
| 6 | `ls documents \| where $row.`, `ls \| select name \| where $row.` | The current folder's columns | The columns of what flows into the stage | F |
| 7 | `wh` | `where`, name only | Name, signature and description | A |
| 8 | `ls \| ` | Nothing | The commands that take a table from the pipe | A |
| 9 | `lss`, `delete` | `Unknown command : lss` | `Did you mean ls?`; `delete` finds `rm` by its keywords | A |
| 10 | `help where` | `'help' takes 0 arguments, but 1 were given.` | That command's parameters and what each is for | A |
| 11 | `help ` | Files | Command names | A |
| 12 | `ls \| sort `, `select `, `group `, `distinct ` | Files | Columns | D |
| 13 | `ls \| sort name d` | `documents/` | `desc` | D |
| 14 | `take `, `skip `, `progress ` | Files | Nothing to pick, and a hint naming what is wanted | D |
| 15 | `set ` | Files | The existing variable names, for replacing one | D |
| 16 | `where `, `find `, `save-view x ` | Files | `$row.` and `not` | D |
| 17 | `attr readme.txt ` | Files | That file's attributes, as `name=` | D |
| 18 | `ls \| sort name -` | Nothing | The command's flags: `-desc` | D |
| 19 | Any argument | No signature shown | `sort <column> [desc] [table]`, with the current parameter marked | D |
| 20 | `cat "doc` | Nothing | `"documents/"` | D |
| 21 | `save <` | Nothing | Tag types in use; after `save <note `, that type's attributes | C |
| 22 | `ls \| where $row.kind ` | All 11 word operators | The 8 comparison operators only | E |
| 23 | `ls \| where $row.kind eq folder ` | All 11 word operators, `eq` included | `and`, `or` | E |
| 24 | `ls \| where $row.kind eq ` | File names; `eq f` offers nothing | The column's values: `folder`, `text` | E, F |
| 25 | `ls \| where $row.kind` run | An empty table, silently | A fault: the predicate is not true or false (0033) | E |
| 26 | `ls \| where kind eq folder` run | An empty table, silently | A fault: it never reads `$row`; did you mean `$row.kind`? (0033) | E |
| 27 | `ls \| where $row.` run | `'where' needs a table, not text.` The stop is read as a second argument, a path called `.` | A syntax error: a column name belongs after the stop | B |
| 28 | `ls \| where $row.kind eq` run | `expected argument, ", "", """, $, (, <, <$, {.` | `eq needs a value to compare with, such as folder.` | B |
| 29 | Any syntax error | Grammar labels (`identifier`, `<$`, `{`) | Phrases (`a command name`, `a variable`, `a tag`) | B |
| 30 | While typing | A parse error only turns the highlighting off | Underline the error once it is behind the word being typed; the sentence in the detail line | G |
| 31 | Tapping a token | Its grammar role only | A variable's value, a command's signature, a column's type | G |
| 32 | Cursor mid-line | Completes the end of the line | Completes the word at the cursor, and leaves the rest | G |
| 33 | Keyboard | Up and Down walk history; Tab fills the common prefix only | Tab steps through the chips; Escape puts the word back | G |
| 34 | More than 12 chips | The rest are dropped silently | A `+N` chip that shows them | G |
| 35 | Chip size | About 26 pixels tall (left open by Phase 7) | 44 pixels, the touch target | G |

## Decision records

All three are written and Accepted, on 2026-09-23. The owner accepted 0032, which
stream B builds, and 0033, which stream E builds. 0031 changes no language, so it was
recorded as Accepted when the phase was planned. No stream waits on a decision.

- **[0031](../decisions/0031-completion-reads-the-line.md). Completion reads the line, and may run what comes before the cursor.**
  Completion parses the line with a placeholder in place of the word being typed (see
  [How the cursor is read](#how-the-cursor-is-read)). That tells it the stage, the
  command, the parameter and the expression state. To learn what flows into the stage,
  it runs the stages before it the way a live view re-runs a line (`Session.Refresh`):
  - read-only commands only;
  - nothing committed, nothing in the history, nothing written to the screen;
  - a time budget of 150 ms, abandoned when a newer keystroke arrives.

  If the budget runs out or a stage is not read-only, completion falls back to what a
  listing of the current folder would have, which is today's answer. The options
  weighed are static column inference per command (every command has to declare how
  its output relates to its input), running the stages (chosen) and always guessing
  (today).
- **[0032](../decisions/0032-a-stage-may-be-a-value.md). A stage may be a value.** A variable reference, with or without members,
  may stand as a stage: `$files`, `$problem.kind`, `$files | where $row.size gt 10`.
  Its value is the stage's value, so `??`, `else` and `try` apply as they do to any
  stage. The grammar already lets a tag stand as a stage (`Binder.evaluateTag`); this
  extends the same idea to variables. `$row` standing as a stage outside a predicate
  is a fault that says where `$row` exists. Decision 0014's slip
  (`$maybe ?? "default"` is not a line) becomes true.
- **[0033](../decisions/0033-a-predicate-is-a-question-about-the-row.md). A predicate is a yes-or-no question about the row.** Two changes. A
  predicate that never reads `$row` is a `Binding` fault when it is bound, with the
  bare words it compared named as the likely columns:
  `kind eq folder never reads $row, so it is the same for every row. Did you mean
  $row.kind eq folder?`. A predicate whose value for a row is not a boolean is an
  `Invalid` fault naming the value and the fix:
  `$row.kind is text (folder), not true or false. Compare it: $row.kind eq folder.`.
  A boolean attribute read bare, `where $row.done`, stays valid. `cd` with a plain
  operand is a path (decision 0013) and is not affected.

## The design

### How the cursor is read

`Context.analyse` takes the text and the cursor offset. It works in four steps:

1. Finds the word under the cursor lexically. The word runs from the nearest
   preceding whitespace, `|`, `(` or `,`, or from an unclosed `"`, to the end of the
   word.
2. Replaces the word with a placeholder identifier that no user will type, and closes
   whatever the line left open: a quote, a parenthesis, a tag.
3. Parses the result. The audit tried this with `QQ` standing in for the placeholder,
   and every line in the finding table parses. It fails for a tag attribute written
   without `=` (`save <note QQ/>`) and an unclosed parenthesis (`(ls | QQ`), which the
   closers in step 2 exist for.
4. Walks the tree to the placeholder. The node it sits in says where the cursor is.
   Positional arguments are counted the way `Binder.bindWith` counts them, so the
   parameter is the one the word would bind to.

If no variant parses, the answer is `Unknown`, and the lexical rules of today's
`Completion.fs` answer. They are kept as the fallback, not deleted.

```fsharp
type Stage =
    { Spec: CommandSpec option            // None when the name is not a command
      Index: int                          // its position in the pipeline
      Upstream: string option             // the source of the stages before it, for Shape
      Written: Tree.Argument list }       // what is already written, placeholder excluded

type Slot =
    | Parameter of Parameter              // the parameter the word would bind to
    | Flag                                // the word starts with -
    | Assignment of name: string option   // name= written, or a name being written
    | Surplus                             // more than the command takes

type Expression =                         // inside a Predicate-kind argument
    | Operand                             // a value starts here: $row., not, a literal
    | AfterOperand of left: Expr          // a comparison operator is next
    | ComparisonRight of op: string * left: Expr
    | AfterComparison                     // and, or, or the end of the argument

type Place =
    | Blank                               // nothing typed
    | CommandName of afterPipe: bool
    | Variable of inPredicate: bool
    | Member of variable: string * path: string list * stage: Stage option
    | Argument of Stage * Slot
    | Predicate of Stage * Expression
    | TagType
    | TagAttribute of typeName: string
    | Unknown

type Word = { Start: int; End: int; Prefix: string; Quoted: bool }
```

### What a completion is

```fsharp
type Completion =
    { Kind: string                        // unchanged vocabulary, plus "flag", "column", "value"
      Text: string
      Start: int
      End: int                            // new: the end of the word replaced, so mid-line works
      Detail: string option }             // new: `table · 4 rows`, a description, a type

type Signature =
    { Command: string
      Description: string
      Parameters: (string * bool * string) list   // name, optional, description
      Active: int option }

type CompletionResult = { Items: Completion list; Signature: Signature option }
```

`Session.Complete(text, cursor) : Async<CompletionResult>`. It is asynchronous because
decision 0031 runs part of the line. `TerminalSession.CompleteAsync` and the bridge's
`Complete` follow it. The page calls it with `invokeMethodAsync` and drops any
response older than the latest keystroke.

### What a parameter takes

A new field on `Parameter`, so that completion and the signature hint read the
command's own declaration rather than keeping a second list:

```fsharp
type Takes =
    | Anything                            // today's answer: files and folders
    | Path | Place | NewName | Url
    | Column | Count | Number | Text
    | Switch of on: string * off: string option
    | VariableName | CommandName | Value  // Value: offer the variables in scope
```

`Parameter.create` defaults it to `Anything`, and `Parameter.takes` sets it. In 8.0
no command sets it, so completion answers exactly as it did.

### What flows into a stage

```fsharp
type Shape =
    { Columns: (string * ColumnType) list
      Rows: Map<string, Value> list option }  // present when the upstream was run

module Shape =
    val ofUpstream : ShapeSource -> Stage -> Async<Shape>
```

In 8.0, `ofUpstream` returns the current folder's listing columns and no rows, which
is what `columnNames` answers today. Stream F makes it run the upstream.

### What a value is, in one line

`Summary.ofValue : Value -> string` for chip details, hover and `vars`. In 8.0 it is
the kind and the display text cut to 40 characters. Stream C makes it right for each
kind of value.

## Checkpoint 8.0: foundation

One agent, before anything else. It is the critical path, and it changes no behaviour
anyone can see: every existing completion test passes unchanged.

1. Confirm that decision records 0031, 0032 and 0033 are Accepted in the log's index.
   They were written when the owner accepted them, so this step writes nothing.
2. `Core/Completion/`: `Summary.fs`, `Shape.fs`, `Context.fs`, and one provider file
   per stream: `Commands.fs` (A), `Variables.fs` (C), `Arguments.fs` and `Paths.fs`
   (D), `Predicates.fs` (E), `Hover.fs` (G). `Completion.fs` becomes the dispatcher:
   it takes a `Place` and asks the providers. Today's logic moves into the providers
   and the lexical fallback as it stands. `Core/Nearest.fs` (edit distance) is
   registered before `Evaluator.fs` with a stub. Every file goes into
   `Core/Core.fsproj` now, so no stream edits the project file.
3. The types above, `Parameter.Takes` with its default, and the asynchronous
   `Session.Complete(text, cursor)`.
4. The adapter and the wire: `TerminalSession.CompleteAsync(text, cursor)`, the
   extended `Completion` DTO, the `Signature` DTO, and the bridge's `Complete`
   returning a task. The page is changed only as far as the new call needs: it
   passes `cmd.selectionStart`, awaits the result, keeps the latest response only,
   and replaces from `start` to `end`.
5. Test files, each empty but for one smoke test, registered in
   `Core.Tests/Core.Tests.fsproj`: `ContextTests.fs`, `CommandCompletionTests.fs`,
   `VariableCompletionTests.fs`, `ArgumentCompletionTests.fs`,
   `PredicateCompletionTests.fs`, `ShapeTests.fs`, `HoverTests.fs`.
   `ContextTests` gets its full table now: one row per line in the finding table,
   giving the `Place` expected. It is the contract every stream codes against.

**Done when** the build is clean, every suite passes, `ContextTests` passes, the
browser check passes, and the page completes exactly as before. One commit per step.

## Parallel workstreams

After 8.0, streams A to G start together. None waits for another to finish. Where
one uses another's work, it codes against the 8.0 interface and gets the better
answer when the other merges.

```
            ┌─ A  commands and help ─────────────┐
            ├─ B  value stages and parse wording ┤
            ├─ C  variables and values ──────────┤
8.0 ────────┼─ D  arguments by parameter ────────┼──▶ 8.9 integration
foundation  ├─ E  predicates ────────────────────┤    docs, spec, browser check
            ├─ F  what flows in ─────────────────┤
            └─ G  the page ──────────────────────┘
```

Soft dependencies, none blocking:

| Stream | Uses | Until it merges |
| --- | --- | --- |
| D (columns), E (values) | F's `Shape` | The current folder's columns, and no values |
| G (details, hover) | C's `Summary` | Kind and display text |
| G (signature line) | D's `Signature` | None, so the line stays empty |

### A. Commands and help

**Closes** 7, 8, 9, 10, 11. **Size** M.

- `Completion/Commands.fs`: each command chip carries the command's description as
  its detail. Straight after a pipe (`ls | `), offer the commands with a parameter
  that `AcceptsPipe`, rather than nothing. The head of an empty line still offers
  nothing, because the suggestion keys are there.
- `Core/Nearest.fs`: Damerau–Levenshtein distance, case-insensitive. A typed word
  also matches a command's `Keywords`, so `delete` offers `rm` with the detail
  `rm · matches "delete"`. Keywords rank below prefix matches, and distance-2 matches
  below both.
- `Fault.unknownCommand` takes the nearest names. `Evaluator.fs:297` passes them, and
  the message becomes `Unknown command : lss. Did you mean ls?`. The spacing before
  the colon is fixed with stream B, which owns the other `Unknown ... :` message.
- `help` takes an optional `command` (`Takes.CommandName`). With it, `help` answers a
  table of that command's parameters (name, required or optional, from the pipe, what
  it takes, description) and writes the description above it. `help where | count`
  still works.
- **Tests** in `CommandCompletionTests.fs` and `MetaCommandTests.fs`.
- **Owns** `Completion/Commands.fs`, `Nearest.fs`, `help` in `Commands/Meta.fs`.
  **Touches** `Faults.fs` (`unknownCommand`) and one line of `Evaluator.fs`.

### B. Value stages and parse wording

**Closes** 3, 4, 27, 28, 29. **Size** L. Builds decision 0032, which is Accepted.

- Grammar (`Parser.FParsec/Grammar.fs`): a stage may be a variable reference
  (0032). This needs a new tree node or an existing one reused, and the
  `TokenStreamVisitor` and `SerialisationVisitor` cases for it. Following Phase 7's
  convention, new parser tests go in the FParsec-only test classes, and the GOLD
  parser is not touched.
- `Evaluator.fs`: evaluates a value stage. `$row` as a value stage, and `$row` read
  anywhere outside a predicate, is a fault of its own:
  `$row is the row a predicate is testing. It exists only inside where, find, cd and
  save-view: ls | where $row.kind eq folder.`. `Fault.unknownVariable` loses its
  stray space: `Unknown variable: $nope`.
- Grammar: a stop after a variable must be followed by a name. The rule gives the
  explanation `a column name belongs after the stop, as in $row.kind`, so
  `$row.` is no longer a variable followed by a path called `.`. A comparison
  operator with nothing after it explains itself the same way
  (`eq needs a value to compare with, such as folder`).
- `TerminalSession.Describe(ParseErrorInfo)`: expected labels become phrases from one
  table: `identifier` → `a command name`, `$` → `a variable`, `<` and `<$` → `a tag`,
  `{` → `a component`, the three quote labels → `a quoted string`, `(` → `a
  parenthesised pipeline`. Duplicates are collapsed and the list is joined with "or".
- **Tests**: Parser.Tests (value stages, the stop, the operator explanation); Core
  (`$files | count`, `$problem.kind`, `$maybe ?? "x"`, `$row` alone, `echo $row`);
  Web.Core.Tests (the wording table).
- **Owns** `Grammar.fs`, the parser tests, the tree node, `TokenStreamVisitor.cs`,
  `Describe(ParseErrorInfo)`. **Touches** `Evaluator.fs` (stage evaluation) and
  `Faults.fs` (`unknownVariable`, the new `$row` fault).

### C. Variables and values

**Closes** 1, 2, 5, 21. **Size** M.

- `Completion/Summary.fs`: one line per value.

  | Value | Summary |
  | --- | --- |
  | Number, boolean, text | `number · 5`, `boolean · true`, `text · "a long…"` |
  | Table | `table · 4 rows · name, kind, folder…` |
  | File | `file · readme.txt · text · 41 bytes` |
  | Tag | `tag · note · 2 attributes` |
  | Fault | `fault · NotFound · Could not find missing.txt` |
  | Query | `query · $row.kind eq folder` |
  | List | `list · 3 items` |

  `vars` uses the same summaries, so the page and the command agree.
- `Completion/Variables.fs`: each variable chip carries its summary. `$row` is offered
  only in `Predicate` places, first, with the detail `the row being tested`.
- Members: after `$x.`, the members of what `$x` holds. That is a table's columns
  (each with its type as the detail), a file's or tag's attributes, or a fault's
  `kind`, `message`, `stage` and `path`, and nothing for a number, text or boolean.
  `$row.` keeps asking `Shape`, so that it improves when F merges.
- Tags: after `<`, the tag types seen in the filesystem and in variables. After
  `<note `, the attribute names that records of kind `note` carry, as `name=`.
- **Tests** in `VariableCompletionTests.fs`, and the `vars` output in
  `VariableCommandTests.fs`.
- **Owns** `Summary.fs`, `Variables.fs`, the tag places. **Touches** `vars` in
  `Commands/Values.fs`.

### D. Arguments by parameter

**Closes** 12 to 20. **Size** L.

- Annotate every parameter of every command with `Takes`. Examples: `sort column` →
  `Column`, `sort desc` → `Switch("desc", Some "asc")`, `take count` → `Count`,
  `cd` → `Place`, `mkdir` → `NewName`, `set name` → `VariableName`,
  `download url` → `Url`.
- `Completion/Arguments.fs`, dispatching on `Slot`:

  | Takes | Offers |
  | --- | --- |
  | `Anything`, `Path` | files and folders (`Paths.fs`) |
  | `Place` | folders and views (today's `cd` rule) |
  | `Column` | `Shape.Columns`, each with its type as the detail |
  | `Switch(on, off)` | `on` and `off` |
  | `VariableName` | the variables in scope |
  | `CommandName` | command names |
  | `Value` | the variables in scope, with their summaries |
  | `Count`, `Number`, `NewName`, `Text`, `Url` | nothing |

  `Flag` offers the command's flags as `-name`, and `Assignment` after `attr <file> `
  offers that record's attribute names as `name=`. A `Predicate` parameter is stream
  E's place, not this stream's.
- The signature: built for every `Argument` and `Predicate` place, with `Active` the
  parameter the word would bind to. `Count` and the other "nothing to pick" kinds are
  where it earns its keep.
- `Paths.fs`: quoted words. `cat "doc` offers `"documents/"`, and a name with a space
  in it completes quoted.
- **Tests** in `ArgumentCompletionTests.fs`: one row per command and parameter, so
  that a new command without `Takes` shows up as a missing row. That makes it a
  failure rather than a quiet fall back to files.
- **Owns** `Arguments.fs`, `Paths.fs`, the signature, the `Takes` annotations in
  `Commands/*.fs` (except `help`, which is A's).

### E. Predicates

**Closes** 22 to 26. **Size** M. The two faults are decision 0033, which is Accepted.

- `Completion/Predicates.fs`, dispatching on `Expression`:

  | Place | Offers |
  | --- | --- |
  | `Operand` | `$row.` (a chip that continues into member completion), `not`, and `(` |
  | `AfterOperand` | the 8 comparison operators (`Expr.comparisonOperators`) |
  | `ComparisonRight(op, $row.c)` | the distinct values of column `c` in `Shape.Rows`, most frequent first, at most 12; for `like`, those values with a `*` |
  | `AfterComparison` | `and`, `or` |

  The "is there a `$` earlier in the stage" test goes: the expression state answers
  it. So does the special case that kept `not` away from `cat no`: a `Path`
  argument is never a predicate place.
- `Expr.fs` and the table functions: the two faults of 0033. The static one is
  checked when the predicate is bound, from the `Expr` tree: no `Expr.Variable "row"`
  anywhere. The dynamic one is checked on the first row whose value is not a
  boolean. The static check lives where `Binder.bindArgument` builds the query, so
  `where`, `find`, `cd` and `save-view` all get it.
- **Tests** in `PredicateCompletionTests.fs`, and the faults in
  `TableCommandTests.fs` and `ViewTests.fs`. Every example program runs to its
  golden results unchanged, which is the proof that 0033 breaks nothing real.
- **Owns** `Predicates.fs`, the predicate checks in `Expr.fs`. **Touches**
  `Binder.fs` (`bindArgument`) and `Faults.fs` (two new constructors).

### F. What flows in

**Closes** 6, and gives 12 and 24 their real answers. **Size** M.

- `Completion/Shape.fs`: `ofUpstream` parses `Stage.Upstream` and runs it through the
  same path as `Session.Refresh`, which already refuses non-read-only lines and
  commits nothing, under the 150 ms budget and a `CancellationToken` that the next
  keystroke cancels. A table result gives columns and rows. A tag or a list of tags
  goes through the same table coercion as `table` (decision 0009). Anything else
  gives no columns.
- A cache keyed by the upstream text and the store's sequence number, so typing
  inside the last stage does not re-run the stages before it. `StoreChanged` clears
  it.
- A variable at the head (`$files | where $row.`, once B merges) reads the variable
  and runs nothing.
- **Tests** in `ShapeTests.fs`: `ls documents`, `ls | select name`, `from-csv`, a
  non-read-only upstream (falls back), a slow upstream (falls back inside the
  budget), a cache hit, and a cache cleared by a commit.
- **Owns** `Shape.fs`, and the upstream run in `Session.fs` or `Evaluator.fs` (a
  `Preview` beside `Refresh`, sharing its checks).

### G. The page

**Closes** 30 to 35, and draws what A, C and D produce. **Size** L.

- The detail line (`#detail`) has one job at a time, in this order: the signature
  while in an argument, with the active parameter bold and its description after it;
  otherwise the selected chip's detail; otherwise a parse error that is behind the
  word being typed.
- Chips: the first is selected, and its detail is shown. Tab fills the common prefix
  the first time, as now, and after that steps through the chips, putting each one
  in place. Shift+Tab steps back, and Escape puts back what was typed. Up and Down
  stay history. Past 12 chips, a `+N` chip shows the rest in the row. Chips are 44
  pixels tall.
- The cursor: completion is asked at `selectionStart`, and a completion replaces
  `start` to `end` only. The rest of the line is kept, and the caret lands after the
  inserted text.
- Live diagnostics: when the parse fails at a column before the current word, the
  mirror underlines from that column to the end of its token. The error is shown
  only once it is behind the word being typed, so a half-written word never flashes
  red.
- Hover: tapping a token asks a new bridge call, `Describe(text, offset)`, answered by
  `Completion/Hover.fs` from `Context` and `Summary`. A variable shows its summary, a
  command its signature and description, a member its column type, and an operator
  what it compares. It replaces today's `kind — text` line.
- **Tests**: Web.Core.Tests for `Describe`, `HoverTests.fs` for the core, and the
  browser check lines listed under 8.9.
- **Owns** `index.html` (beyond 8.0's call-site change), `Hover.fs`, and the bridge's
  `Describe`.

## Who touches what

A file appears once as owned. The shared files are small, additive edits that
different streams make in different places. The integrator resolves them at merge.

| File | 8.0 | A | B | C | D | E | F | G |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `Core/Core.fsproj`, `Core.Tests/Core.Tests.fsproj` | own | | | | | | | |
| `Core/Completion/Context.fs` | own | | | | | | | |
| `Core/Completion.fs` (dispatcher) | own | | | | | | | |
| `Core/Completion/Commands.fs`, `Core/Nearest.fs` | stub | own | | | | | | |
| `Core/Completion/Variables.fs`, `Summary.fs` | stub | | | own | | | | |
| `Core/Completion/Arguments.fs`, `Paths.fs` | stub | | | | own | | | |
| `Core/Completion/Predicates.fs` | stub | | | | | own | | |
| `Core/Completion/Shape.fs` | stub | | | | | | own | |
| `Core/Completion/Hover.fs` | stub | | | | | | | own |
| `Core/Commands/Spec.fs` (`Takes`) | own | | | | | | | |
| `Core/Commands/*.fs` (annotations) | | `help` | | `vars` | own | | | |
| `Core/Faults.fs` | | edit | edit | | | edit | | |
| `Core/Evaluator.fs` | | edit | own | | | | edit | |
| `Core/Expr.fs`, `Core/Binder.fs` | | | | | | own | | |
| `Core/Session.fs` | own | | | | | | edit | |
| `Parser.FParsec/Grammar.fs`, `Parser.Tree/` | | | own | | | | | |
| `Web.Core/TerminalSession.cs` | own | | wording | | | | | `Describe` |
| `Web.Core/TokenStreamVisitor.cs` | | | own | | | | | |
| `WebClient/TerminalBridge.cs` | own | | | | | | | `Describe` |
| `WebClient/wwwroot/index.html` | call site | | | | | | | own |
| `docs/`, `tools/browser-check.mjs` | records | | | | | | | |

Documentation and the browser check are left to 8.9, so the user docs and the spec
are written once, against what merged, rather than seven times in parallel.

## Running it

The session that runs this phase is the orchestrator, following
[running.md](running.md). That gives nine pieces of work, and says who does each:

```
orchestrator   8.0 foundation ──────────────────────────────────▶ merge each ──▶ 8.9 index, verifier, republish, As built
sub-agents                     A B C D E F G, in parallel worktrees             8.9 docs · spec · check, in parallel
```

1. **8.0: the orchestrator, itself,** in the main checkout. It is the critical path,
   and every brief is written against it. Scouts may help it read. When the
   checkpoint's "Done when" holds, it pushes, then launches the streams.
2. **A to G: seven stream agents, launched together in one message,** each in its own
   worktree created from the 8.0 commit. Each gets
   [the brief](running.md#the-brief), filled in from its section above and from this
   table:

   | Stream | Title | Findings | Records to read | Done when, beyond the brief |
   | --- | --- | --- | --- | --- |
   | A | Commands and help | 7 to 11 | — | `lss` run says `Did you mean ls?`, and `help where \| count` still works |
   | B | Value stages and parse wording | 3, 4, 27 to 29 | 0014, 0032 | `$v` run answers `5`; no syntax error names a grammar label |
   | C | Variables and values | 1, 2, 5, 21 | 0008 | `vars` and the chip details print the same summaries |
   | D | Arguments by parameter | 12 to 20 | 0017, 0021 | `ArgumentCompletionTests` has a row for every parameter of every command |
   | E | Predicates | 22 to 26 | 0008, 0013, 0033 | the four example programs still give their golden results |
   | F | What flows in | 6, and 12 and 24's real answers | 0009, 0031 | every case listed under `ShapeTests.fs` passes, the budget and the cache included |
   | G | The page | 30 to 35 | 0030 | the existing browser check passes against a local publish of the worktree |

   Each stream's "Owns" and "Touches" lines, and its column in
   [Who touches what](#who-touches-what), fill in the brief's rule 1.
3. **Merging: the orchestrator,** in this order when several are waiting:
   1. **F** and **B** first, because others improve once they are in.
   2. Then **C**, **D** and **E**.
   3. Then **A**.
   4. **G** last, because it draws everything else.

   After each merge: the full build, every test suite, and the browser check. A stream
   that is not ready does not hold the others: merge order is a preference, not a
   dependency.
4. **8.9, parts 1 to 3: three sub-agents in parallel,** once all seven are merged, each
   owning one set of files and briefed with its item of [Checkpoint 8.9](#checkpoint-89-integration):

   | Part | Owns | Covers |
   | --- | --- | --- |
   | docs | the user documents in `docs/` | item 1 |
   | spec | `docs/spec/` | item 2 |
   | check | `tools/browser-check.mjs` | item 3 |

5. **8.9, parts 4 and 5: the orchestrator.** The decision index, a verifier run of the
   [Acceptance](#acceptance) table against the merged head, the republish, the "As
   built" section, and the status lines here and in the README.

## Checkpoint 8.9: integration

1. **Documentation**, re-run and re-pasted with `tools/transcript.fsx`:
   - `docs/web-terminal.md`: the completion table, the detail line, Tab, hover.
   - `docs/language.md`: value stages (0032).
   - `docs/tables.md`: the predicate faults (0033).
   - `docs/commands.md`: `help <command>`.
   - `docs/errors.md`: the new messages.
   - `docs/getting-started.md`: "type `$` to see your variables".
2. **Specification**:
   - `docs/spec/host-interfaces.md`: "Completion" rewritten as a table of `Place` to
     offers, with the new wire format and `Describe`.
   - `lexical-grammar.md` and `semantic-tree.md`: the value stage and the stop rule.
   - `execution-model.md`: the predicate rules.
   - `command-catalogue.md`: `help`.
   - `conformance.md`: counts regenerated.
3. **Browser check**: a `PHASE_8` list covering, at 390 by 844:
   - `$` shows `$files` with `table · 4 rows` in the detail line;
   - `ls | sort ` offers `name`;
   - `ls | where $row.kind eq ` offers `folder`;
   - Tab steps through the chips;
   - completing mid-line keeps the tail;
   - `$files` alone draws the table;
   - `lss` says `Did you mean ls?`;
   - tapping `$files` in a finished line shows its summary.
4. **Decision index**: a record, and its row, for every question a stream raised
   that the owner answered during the phase.
5. **Republish** and an "As built" section here.

## Progress

Kept by the orchestrator, and committed after every change of state, so a new session
can pick up where the last one stopped. States: `not started`, `in progress`,
`reported`, `merged`, `blocked` (with the reason).

| Work | Done by | State | Commit |
| --- | --- | --- | --- |
| 8.0 Foundation | orchestrator | not started | |
| A. Commands and help | stream agent | not started | |
| B. Value stages and parse wording | stream agent | not started | |
| C. Variables and values | stream agent | not started | |
| D. Arguments by parameter | stream agent | not started | |
| E. Predicates | stream agent | not started | |
| F. What flows in | stream agent | not started | |
| G. The page | stream agent | not started | |
| 8.9 docs, spec, check | three sub-agents | not started | |
| 8.9 index, verifier, republish, As built | orchestrator | not started | |

## Acceptance

In the seeded filesystem at `/`, after `set v 5`, `ls | set files` and
`try cat missing.txt | set problem`. `|` marks the cursor where it is not at the end.

| Line | Offers, or does |
| --- | --- |
| `$` | `$files` `table · 4 rows…`, `$problem` `fault · NotFound…`, `$v` `number · 5`; no `$row` |
| `ls \| where $` | `$row` first, detail `the row being tested` |
| `$v` run | `5` |
| `$files \| count` run | `4` |
| `$row` run, `echo $row` run | the fault naming where `$row` exists |
| `$v.` | nothing |
| `$problem.` | `kind`, `message`, `stage`, `path` |
| `ls documents \| where $row.` | the columns of `ls documents` |
| `ls \| select name \| where $row.` | `name` |
| `wh` | `where`, detail `Keep the rows a predicate is true for` |
| `ls \| ` | the commands that take a table from the pipe |
| `delete` | `rm` |
| `lss` run | `Unknown command : lss. Did you mean ls?` |
| `help where` run | a table of `predicate` and `table` |
| `ls \| sort ` | `name`, `kind`, `folder`, `size`, `modified`; signature `sort <column> [desc] [table]`, `column` active |
| `ls \| sort name d` | `desc` |
| `ls \| sort name -` | `-desc` |
| `ls \| take ` | nothing; signature with `count` active |
| `ls \| where ` | `$row.`, `not`, `(` |
| `attr readme.txt ` | `readme.txt`'s attributes as `name=` |
| `cat "doc` | `"documents/"` |
| `ls \| where $row.kind ` | `eq`, `ne`, `gt`, `ge`, `lt`, `le`, `like`, `has` |
| `ls \| where $row.kind eq ` | `folder`, `text` |
| `ls \| where $row.kind eq folder ` | `and`, `or` |
| `ls \| where $row.kind` run | the "not true or false" fault |
| `ls \| where kind eq folder` run | the "never reads `$row`" fault, suggesting `$row.kind` |
| `ls \| where $row.` run | `a column name belongs after the stop…` |
| `ls \| where $row.kind eq` run | `eq needs a value to compare with…` |
| `cat re\| documents` | `readme.txt`; applying it keeps ` documents` |

Plus: every existing suite green, the four example programs at their golden results,
the payload under 20 MB, and no warnings.

## Risks

| Risk | Mitigation |
| --- | --- |
| Running the upstream on every keystroke is slow on a phone | The 150 ms budget, cancellation by the next keystroke, and the cache keyed by upstream text and store sequence. A budget miss falls back to today's answer; it is never an error. |
| The placeholder parse misreads a line | `ContextTests` pins every place in the finding table. `Unknown` falls back to today's lexical rules, so a misread offers what it used to, not something worse. |
| 0033 breaks a line that people write | The example programs and every documented transcript are re-run in E. A boolean attribute read bare stays valid. If the owner prefers a warning to a fault, only the constructor changes. |
| Asynchronous completion races the typing | The page keeps a sequence number per request and drops stale answers. The core cancels a superseded upstream run. |
| Several streams edit `Faults.fs` (A, B, E) and `Evaluator.fs` (A, B, F) | Edits are additive and in different functions. Merge order puts the owner of the largest change (B) first. |
| Tab stepping through chips surprises desktop users | The first Tab still fills the common prefix. Stepping starts only when that does nothing more, which is how shells that cycle behave. |
