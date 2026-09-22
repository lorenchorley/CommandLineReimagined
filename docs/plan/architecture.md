# Target architecture

What the codebase looks like when every phase is done. Phases build towards this; when
a phase document and this one disagree, this one is wrong and should be fixed first.

## Projects

| Project | Language | Role | Fate |
| --- | --- | --- | --- |
| `Parser.Tree` | C# | Semantic tree, visitors, parser error types | Unchanged, gains nodes for expressions |
| `Parser.FParsec` | F# | Grammar and parser | Extended per phase |
| **`Core`** (new, `Core/Core.fsproj`, assembly `CommandLineReimagined.Core`) | F# | Values, faults, railway, events, store, projections, filesystem model, binder, evaluator, commands, session, completion, XML | New in Phase 1 |
| **`Core.Tests`** (new, `Core.Tests/Core.Tests.fsproj`) | F# | Every semantic test | New in Phase 1; absorbs `Execution.Tests` |
| `Web.Core` | C# | Adapter: `Session` to JSON DTOs; tokeniser; parse service | Rewritten as adapter in Phase 1 |
| `Web.Core.Tests` | C# | DTO mapping, completion, streaming, cancel | Kept |
| `WebClient` | C# + HTML/JS | Blazor bridge, IndexedDB log, page | Extended per phase |
| `Web` | C# | ASP.NET host, parse only | Unchanged |
| `CommandLine` (assembly `Terminal`) | C# | WPF shell over the ECS | Desktop adapter only; its `Execution/` and `Commands/` folders are deleted |
| `Commands` | C# | Command implementations | Deleted; `DebugOut` moves into `CommandLine/` |
| `Execution.Tests` | C# | Execution tests | Deleted once `Core.Tests` covers every case |
| `CommandLineReimagined` (`Application.csproj`) | C# | WPF host | Registration updated to the `Session` |

`FileId` is declared in `Values.fs` rather than `Events.fs`, because `FileRef` names
one and values compile before events. Faults compile before values, because from
Phase 5 a fault is one (`Value.Fault`).

`Core` depends on `Parser.Tree` and `Parser.FParsec` only. Nothing in `Core` references
the ECS, rendering, WPF, Blazor or JavaScript.

## Core types

Namespaces under `CommandLineReimagined.Core`. Signatures are normative; bodies are the
implementer's.

### Values

```fsharp
type Value =
    | Empty                        // a command that returns nothing
    | None                         // an absent optional value
    | Text of string
    | Number of float
    | Boolean of bool
    | File of FileRef              // a record in the store
    | List of Value list
    | Object of Tag                // <type a=1>...</type>
    | Component of Tag             // {type a=1/}
    | Table of Table               // Phase 3
    | Query of Expr                // Phase 4: a predicate as a value
    | Fault of Fault               // produced by `try`

and Tag     = { TypeName: string; Attributes: Map<string, Value>; Children: Value list }
and FileRef = { Id: FileId; Name: string; Kind: string; Folder: string }

module Value =
    val display  : Value -> string     // how it reads to a person
    val argument : Value -> string     // what it means as an argument
    val kind     : Value -> string     // "text" | "number" | ... for DTOs
```

`display` and `argument` keep the two-string rule from the current specification:
a `File` displays as its name and arguments as its full path `folder/name`. Options
are not a wrapper: an optional result is either a value or `None`, and `??` unwraps.
The implementation uses `Value option` internally wherever absence is meaningful.

### Faults and the railway

```fsharp
type FaultKind =
    | Syntax | Binding | UnknownCommand | NotFound | Conflict | Invalid
    | Cancelled | Internal

type Fault =
    { Kind: FaultKind; Message: string; Stage: int option
      Path: string option; Cause: Fault option }

type Outcome<'T> = Result<'T, Fault>

module Outcome =
    val bind   : ('a -> Outcome<'b>) -> Outcome<'a> -> Outcome<'b>
    val map    : ('a -> 'b) -> Outcome<'a> -> Outcome<'b>
    val orElse : (Fault -> Outcome<'a>) -> Outcome<'a> -> Outcome<'a>
    val ofOption : Fault -> 'a option -> Outcome<'a>

type OutcomeBuilder  // `outcome { let! x = ... }` computation expression
```

Every message in the current [error reference](../errors.md) is preserved verbatim
as a `Fault.Message`; the kind is added. A fault renders as its message; `try` turns
it into a `Value.Fault` a user can inspect.

### Events, transactions, log

```fsharp
type FileId = string          // GUID, lower-case, no braces
type Hash   = string          // SHA-256 of UTF-8 content, lower-case hex

type FileRecord = { Id: FileId; Attributes: Map<string, Value>; Content: Hash option }

type Location = { Folder: string; View: Expr option }   // View is Phase 4

type Event =
    | FileCreated       of FileRecord
    | FileDeleted       of FileRecord
    | AttributesChanged of id: FileId * before: Map<string, Value> * after: Map<string, Value>
    | ContentChanged    of id: FileId * before: Hash option * after: Hash option
    | VariableChanged   of name: string * before: Value option * after: Value option
    | LocationChanged   of before: Location * after: Location

type Transaction =
    { Seq: int64; At: DateTimeOffset; Source: string
      Events: Event list; Compensates: int64 option }

type ILog =
    abstract Append  : Transaction -> Async<unit>
    abstract ReadAll : unit -> Async<Transaction list>
    abstract PutBlob : string -> Async<Hash>
    abstract GetBlob : Hash -> Async<string option>
```

Every event carries its own inverse's information. `invert` is total:

| Event | Inverse |
| --- | --- |
| `FileCreated r` | `FileDeleted r` |
| `FileDeleted r` | `FileCreated r` |
| `AttributesChanged (id, b, a)` | `AttributesChanged (id, a, b)` |
| `ContentChanged (id, b, a)` | `ContentChanged (id, a, b)` |
| `VariableChanged (n, b, a)` | `VariableChanged (n, a, b)` |
| `LocationChanged (b, a)` | `LocationChanged (a, b)` |

Undo appends `{ Events = List.rev (List.map invert t.Events); Compensates = Some t.Seq }`
for the latest transaction that is neither a compensation nor already compensated.
Redo appends the original events with `Compensates = Some c.Seq` for the latest
compensation whose own seq is not compensated. "Compensated" means some later
transaction's `Compensates` names it. History is the list of transactions with a
derived `Undone: bool`.

Logs: `InMemoryLog` (tests, desktop) and `IndexedDbLog` (browser, Phase 2, implemented
in `WebClient` against a small JavaScript module and handed to `Core` through `ILog`).

### Projection and store

```fsharp
type Projection =
    { Files: Map<FileId, FileRecord>; Variables: Map<string, Value>
      Location: Location; Applied: int64 }

module Projection =
    val empty : Projection
    val apply : Projection -> Event -> Projection
    val invert: Event -> Event

type Store(log: ILog, clock: unit -> DateTimeOffset) =
    member Initialize : unit -> Async<unit>                       // replay
    member Current    : Projection
    member Commit     : source: string -> Event list -> Async<Outcome<Transaction option>>
    member Undo       : unit -> Async<Outcome<Transaction option>>
    member Redo       : unit -> Async<Outcome<Transaction option>>
    member History    : unit -> (Transaction * undone: bool) list
    member Changed    : IEvent<int64>                             // Phase 4 live views
```

`Commit` validates events against `Current` before appending (a `FileCreated` whose
name already exists in its folder is a `Conflict`), applies them, appends, raises
`Changed`. An empty event list appends nothing and answers `Ok None`, which is how a
read-only line leaves no transaction behind.

### Filesystem model

A file is a `FileRecord`. Reserved attributes, all `Text` unless stated:

| Attribute | Set by | Meaning |
| --- | --- | --- |
| `name` | user | Unique within its folder together with `kind = folder` or not |
| `kind` | inferred or user | `folder`, `text`, `xml`, `csv`, `json`, `note`, ... ; inferred from the extension when not given |
| `folder` | runtime | Full path of the containing folder, `/` for the root |
| `created`, `modified` | runtime | ISO-8601 `Text`; `modified` updates on content or attribute change |

`size` is not stored: it is the content's length, computed when a table is built.
Any other attribute is user data of any `Value` type.

A folder is a record with `kind = folder` and no content. The root is implicit. A
folder's path is `parent-folder + "/" + name`, with `/` for the root and no trailing
separator. Names are unique within a folder across files and folders.

Paths in arguments are text: absolute (`/documents/notes.txt`) or relative to
`Location.Folder`, with `.` and `..`. Resolution is a pure function over `Projection`.

```fsharp
module Files =
    val resolve  : Projection -> Location -> string -> Outcome<FileRecord>       // NotFound
    val resolveFolder : Projection -> Location -> string -> Outcome<string>      // path of an existing folder
    val inFolder : Projection -> string -> FileRecord list                       // direct children
    val inferKind : name: string -> string
    val pathOf   : Projection -> FileRecord -> string
```

`Location.Folder` is the current folder. `cd <folder>` changes it and answers the
folder's record. In Phase 4, `cd <predicate>` sets `Location.View` instead and answers
the predicate; `ls` then lists every record in the store the view matches, `pwd`
answers a `Query`, `up` puts the view down before it moves, `find <predicate>` asks the
same question without going anywhere, and `save-view <name> <predicate>` keeps one as a
record of kind `view` whose content is the predicate text, which `cd <name>` enters.
New files are always created in `Location.Folder`.

### Commands

```fsharp
type ParamKind = Single | Predicate | Assignments     // Predicate Phase 3, Assignments Phase 1

type Parameter =
    { Name: string; Description: string; Optional: bool; Flag: string option
      Default: Value; AcceptsPipe: bool; Kind: ParamKind }

type CommandSpec =
    { Name: string; Description: string; Keywords: string list
      Parameters: Parameter list
      Meta: bool          // no transaction (undo, redo, history, help)
      ReadOnly: bool }    // Phase 4: a live view may re-run it

type IOutput =
    abstract NewLine : unit -> IOutputLine
and IOutputLine =
    abstract Write : text: string -> IOutputText
and IOutputText =
    abstract Text : string with get, set

type IBlobs =
    abstract Put : string -> Async<Hash>
    abstract Get : Hash -> Async<string option>

type Invocation =
    { Spec: CommandSpec; Args: Map<string, Value>; Assignments: (string * Value) list
      Input: Value; Output: IOutput; Scope: Scope; Projection: Projection
      Location: Location; Blobs: IBlobs; Cancel: CancellationToken }

type CommandResult = { Value: Value; Events: Event list }

type Command = { Spec: CommandSpec; Run: Invocation -> Async<Outcome<CommandResult>> }
```

A command reads `Projection` and `Location`, never the store. It returns events; it
does not apply them. `Blobs` is the whole of the log a command may see: `write` puts
its text and names the hash in an event, `cat` gets it back. It is a capability rather
than the log itself, so a command can read and write content and cannot append,
undo or read the history. Long-running commands write through `Output` and observe
`Cancel`. Meta commands receive a `StoreAccess` capability through a separate
constructor argument and are the only ones allowed to call `Undo`, `Redo`, `History`,
or, in the case of `run`, to execute further lines.

### Scripts

A script is a file of command lines, kind `script`, extension `.clr`. Blank lines and
lines beginning with `#` are skipped. `run path` executes each line as its own
transaction and stops at the first fault; see [examples.md](examples.md#the-run-command).
The four programs in `examples/` are embedded in the core assembly and seeded into
`/examples` on a fresh log.

### Binder and evaluator

The binding algorithm is the one in
[execution-model.md](../spec/execution-model.md#argument-binding), unchanged, with two
additions: an argument of the form `name=value` (Phase 1 grammar) binds to the single
`Assignments` parameter if the command declares one and is a `Binding` fault
otherwise; an argument that parses as an expression with operators binds to a
`Predicate` parameter (Phase 3).

```fsharp
type Evaluator(commands: Command list, store: Store, output: IOutput) =
    member Execute : RootNode -> executionId: int -> CancellationToken -> Async<Outcome<Value>>
```

Per line: fold stages left to right threading `Value`; accumulate events in a working
projection so a later stage sees an earlier stage's effect; on `Ok` commit one
transaction with `Source = the line`; on `Error` commit nothing. A line whose events
are empty commits nothing either, so `undo` never has a read-only line to undo. Meta
commands run outside the transaction.

### Session

```fsharp
type Response =
    { Source: string; Output: string list; Result: Value option
      Fault: Fault option; Location: Location }

type SeedFile = { Name: string; Folder: string; Content: string option }
type Seed     = IBlobs -> Async<Event list>

type Session(log: ILog, options: SessionOptions, seed: Seed) =
    member Initialize    : unit -> Async<unit>       // replay, then seed if the log was empty
    member Execute       : source: string * executionId: int * CancellationToken -> Async<Response>
    member Refresh       : source: string -> Async<Response>   // Phase 4: re-read, commit nothing
    member Cancel        : unit -> bool
    member Complete      : string -> Completion list
    member Commands      : CommandSpec list
    member Location      : Location
    member Projection    : Projection
    member OutputChanged : IEvent<int * string list>
    member StoreChanged  : IEvent<int64>
```

`Execute` never raises. A parse failure is a `Syntax` fault; an unexpected exception
is an `Internal` fault; cancellation is a `Cancelled` fault with message `Stopped.`.

A seed is a function of the blob store rather than a list of events, because content
has to reach the blob store before an event can name its hash, and the blob store
belongs to the log the session is handed. `SessionOptions` carries the four things
hosts differ on: the clock, the source of record ids, the HTTP client and what `exit`
means.

## Wire formats (C# adapter in `Web.Core`)

The existing `ExecutionResponse` keeps its fields. `result` items gain kinds and one
carries structure:

```json
{ "kind": "file",    "text": "notes.txt", "path": "/documents/notes.txt" }
{ "kind": "folder",  "text": "documents", "path": "/documents" }
{ "kind": "none",    "text": "" }
{ "kind": "fault",   "text": "File does not exist : /x", "faultKind": "NotFound" }
{ "kind": "query",   "text": "$row.kind eq note" }
{ "kind": "table",   "columns": [ { "name": "name", "type": "file" } ],
                     "rows": [ [ { "kind": "file", "text": "a.txt", "path": "/a.txt" } ] ] }
```

`error` stays a sentence for the page's red line; `fault` is added beside it with
`kind` and `message`. `workingDirectory` becomes `location`: `{ "folder": "/documents",
"view": null }`. Keep `workingDirectory` as an alias for one phase, then remove it.

Bridge additions: `Initialize()` (async, replays the log), `Undo`, `Redo` become
commands and the bridge methods are removed; `Refresh(source)` and
`window.terminal.storeChanged(seq)` for live views (Phase 4).

## Grammar changes, by phase

Notation is [decision 0007](../decisions/0007-notation-conflicts.md): lookahead
delimiters plus word operators. Full EBNF for the current grammar is in
[lexical-grammar.md](../spec/lexical-grammar.md); the deltas are:

**Phase 1**

```
Word       ::= WordStart WordChar*  where a "/" that is immediately followed by ">"
               or "}" ends the word instead of joining it
TagAttribute ::= Identifier Space* "=" Space* ArgumentSimpleValue Space*    -- bare words now allowed
Number     ::= "-"? Digit ...                    -- "-" followed by a digit is a number, not a flag
Assignment ::= Identifier "=" ArgumentValue      -- no spaces; only in command-argument position
CommandArgument ::= Flag | Assignment | ArgumentValue
ObjectInstance opener: "<" is a tag only when the next character is an IdentifierChar, "$" or "/"
```

**Phase 3**

```
CommandArgument ::= Flag | Assignment | Expression
Expression  ::= OrExpr
OrExpr      ::= AndExpr ( "or" AndExpr )*
AndExpr     ::= NotExpr ( "and" NotExpr )*
NotExpr     ::= "not" NotExpr | Comparison
Comparison  ::= Operand ( CompareOp Operand )?
CompareOp   ::= "eq" | "ne" | "gt" | "ge" | "lt" | "le" | "like" | "has"
Operand     ::= ArgumentValue | "(" Pipeline ")"
VariableReference ::= "$" Identifier ( "." Identifier )*       -- member access, $row.size
```

The reserved words `and or not eq ne gt ge lt le like has else try` cannot be bare
words; quote them to pass them as text. A command may not be named after one.

**Phase 4**

```
CommandName ::= Identifier ( "-" Identifier )*   -- adjacent hyphens only; `ls -l` is unchanged
CommandExpression_CLINotation ::= CommandName <CommandArgumentList>
FunctionExpression            ::= CommandName "(" <FunctionArgumentList> ")"
```

Decision [0022](../decisions/0022-hyphenated-command-names.md). The parser also gains
an expression root, so a saved view's text can be read back as the predicate it was.

**Phase 5**

```
Program    ::= Space* ( EOF | Line Space* EOF )
Line       ::= Pipeline ( "else" Pipeline )*
Pipeline   ::= Stage ( "|" Stage )*
Stage      ::= "try"? CommandExpression ( "??" Operand )?
CommandExpression ::= FunctionExpression | CliExpression | InstanceTag | "(" Pipeline ")"
FunctionExpression: the "(" must be adjacent to the name; "name (" is a command with a parenthesised operand
```

`try` and `??` apply to one stage: `try cat x | set problem` binds the fault, and
`first (ls) ?? "none"` defaults `first`'s result. `else` applies to whole pipelines.

From Phase 3, `ls` returns a table of records and has no parent row; the page shows
`up` in the location line.

Each grammar change ships with: parser tests, tokeniser kinds (`operator`, `member`,
`keyword` added to the token kinds), serialiser round-trip, and an update to
`lexical-grammar.md`.

## Browser page

The page keeps its structure. Additions by phase: fault rendering with kind (1),
`Initialize` before enabling the input and a "restoring" status (2), an HTML table
renderer with sortable headers and tappable cells (3), the location line showing folder
or view and live table refresh on `storeChanged` (4), keyword colouring (3, 5). Chips
grow to 44 pixels in Phase 1.

## Expected new decision records

| # | Decision | Phase |
| --- | --- | --- |
| 0015 | A line is one atomic transaction | 1 |
| 0016 | Folders are records of kind `folder`; the root is implicit | 1 |
| 0017 | `name=value` in argument position is data, `name: value` binds a parameter | 1 |
| 0019 | Reserved words in expression positions | 3 |
| 0022 | A command's name may be several words joined by hyphens | 4 |
| 0023 | Function form requires an adjacent parenthesis; a spaced parenthesis is a nested pipeline | 5 |
| 0024 | Stop is not a failure a line can recover from | 5 |
| 0025 | XML element text content is out of scope for the first XML release; it is read as a `text` attribute | 6 |
| 0026 | The inventory program sorts its reorder list before writing it | 6 |
| 0020 | Scripts: one line per statement, `#` comments, `.clr`, `run` commits per line and stops at the first fault | 3 |
| 0021 | One parameter may collect the remaining positional arguments | 3 |
