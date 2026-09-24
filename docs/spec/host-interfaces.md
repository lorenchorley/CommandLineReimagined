# Host interfaces

What a host must provide to run the execution layer, and the wire formats the browser
front end depends on.

## Interfaces the core requires

### IOutput

Where a command writes while it runs, as distinct from the value it returns.

```fsharp
type IOutput =
    abstract NewLine : unit -> IOutputLine

and IOutputLine =
    abstract Write : text: string -> IOutputText

and IOutputText =
    abstract Text : string with get, set
```

An implementation **must** let a command mutate `Text` after writing it, which is how a
progress indicator updates in place, and **must** keep written runs distinct within a
line so changing one does not disturb another.

A host **must not** require a command to know anything else about presentation. This is
the interface that keeps commands free of a window, a render loop or a graphics library,
and it is why they run under WebAssembly.

There is no way to withdraw what was written. Taking a line off the screen is the
host's business, not the command's: the execution response says which lines were undone
and redone (see [Execution response](#execution-response)), and a host **may** hide the
entry for an undone line and show it again on redo
([decision 0030](../decisions/0030-undo-takes-the-line-back-on-screen.md)). The browser
page does; the desktop shell draws `undo` as a line of its own.

### ILog

Where transactions and content live.

```fsharp
type ILog =
    abstract Append  : Transaction -> Async<unit>
    abstract ReadAll : unit -> Async<Transaction list>
    abstract PutBlob : string -> Async<Hash>
    abstract GetBlob : Hash -> Async<string option>
    abstract Clear   : unit -> Async<unit>
```

Asynchronous throughout, because the browser's storage is and WebAssembly is single
threaded, so there is nowhere to block. An implementation **must** store content by the
hash of its text, so that storing the same text twice stores it once.

An implementation **must** be able to reproduce the projection by replaying what it
returns from `ReadAll`, in sequence order.

`Clear` is the one operation that is not append-only. It exists for `reset` and
**must not** be reachable from a command other than that one.

`InMemoryLog` is supplied, and is what the tests and the desktop shell use.
`IndexedDbLog` in the browser client keeps the log in the browser's own storage.

### The stored shape

What a log holds is a contract between builds: what one writes, a later one reads. An
implementation **must not** serialise the F# types directly, or renaming a union case
would make a stored filesystem unreadable.

Every document carries `"v"`, and a reader exists per version. This build writes
version 2 and reads 1 and 2. Version 2 added the `query` and `fault` value kinds, which
a variable can hold (`try read x | set problem`); every version 1 document
is a valid version 2 one, so one reader serves both, and the version exists so that a
build which only knew version 1 refuses a log it would misread rather than failing on
an unknown kind halfway through it.

```json
{
  "v": 2,
  "seq": 2,
  "at": "2026-09-21T12:34:56.7890000+02:00",
  "source": "write notes.txt hello",
  "undoable": true,
  "compensates": null,
  "events": [
    { "type": "fileCreated",
      "record": { "id": "id-1",
                  "attributes": { "name": { "k": "text", "v": "notes.txt" } },
                  "content": null } },
    { "type": "contentChanged", "id": "id-1", "before": null, "after": "abc123" }
  ]
}
```

Event types are `fileCreated`, `fileDeleted`, `attributesChanged`, `contentChanged`,
`variableChanged` and `locationChanged`.

A `locationChanged` event's two sides are each `{ "folder": ..., "view": ... }`, where
`view` is null or the predicate's display text. It is stored as text and read back
through the grammar's expression entry point, so it is the same text a saved view
holds and the same text the location line shows.

Values are written tagged with a kind `k`, one of `empty`, `none`, `text`, `number`,
`boolean`, `file`, `list`, `object`, `component`, `table`, `query` or `fault`. A `query`
is its predicate's display text, read back through the grammar like a view. A `fault`
is `{ "k": "fault", "kind": "NotFound", "message": ..., "stage": 1, "path": ...,
"cause": null }`, where `kind` is the kind's word and `cause` is another fault or null. An implementation **must not**
write a value as the nearest JSON type: an attribute whose text is `2026` has to come
back as text, and JSON cannot tell that from a number without being told.

`at` **must** be round-trip formatted, so two transactions in the same second stay
distinguishable.

Rules for a reader:

- An unknown version **must** be refused with a message naming it. A log that cannot be
  read is a filesystem that cannot be opened, and the user needs to know which it is.
- A missing `undoable` **must** read as true. A log written before
  [decision 0018](../decisions/0018-the-seed-is-not-a-line-anyone-typed.md) has no such
  field, and every transaction in it was the user's to take back.
- A host **should** skip a transaction it cannot decode rather than refusing to open the
  session, and **should** say how many it skipped. Losing part of a history is bad;
  refusing to start at all is worse.

### Degrading without storage

Storage is absent in a private window and can stop working mid-session when site data
is cleared with the page open. An implementation **must not** let either break the
terminal: the log falls back to memory, the session keeps working for as long as the
tab is open, and the host reports the state so the page can say `not persisted`.

A host **must** report this before the first command runs, not after: a user who finds
out at the end of a session that nothing was kept has already lost the work.

### SessionOptions

The four things hosts differ on:

```fsharp
type SessionOptions =
    { Clock      : unit -> DateTimeOffset
      NewId      : unit -> FileId
      HttpClient : unit -> HttpClient
      Exit       : unit -> unit }
```

`Exit` is what `exit` calls; a host with nothing to close **may** pass a function that
does nothing. A C# host **should** build these with `SessionOptions.ofDelegates`, which
takes plain `Func` and `Action`, rather than constructing F# functions: the boundary is
where F# types stop.

### What a host no longer provides

A host does not register commands, resolve them, or supply a service provider. The
session holds every command, and a host that wants to add one supplies it to the
session rather than to a container.

## Hosts

Three hosts build a session, and each differs only in its `SessionOptions` and its log:

| Host | Log | `Exit` | Renders |
| --- | --- | --- | --- |
| Browser client (`WebClient`) | `IndexedDbLog`, falling back to memory | nothing | DTOs from `TerminalSession`, drawn by the page |
| Desktop shell (`Terminal.Execution.DesktopSession`) | `InMemoryLog` | closes the window | display strings, as text blocks |
| Tests (`Core.Tests`, `Web.Core.Tests`) | `InMemoryLog` | nothing | assertions on values and DTOs |

The desktop shell keeps its log in memory, so its filesystem lives as long as the window
does; [decision 0012](../decisions/0012-browser-first.md) keeps it out of scope beyond
compiling and running commands.

## TerminalSession

The reference host-side object, shared by both web front ends.

| Member | Contract |
| --- | --- |
| `TerminalSession(ILog? log)` | Builds a session over the log, in memory by default. Does not replay it. |
| `InitializeAsync()` | Replays the log, and seeds it when it was empty. **Must** be awaited before any execution. |
| `ReplayedCount` | How many transactions came back. Zero on a first visit. |
| `Commands` | Every command as `CommandSummary`, sorted by name, excluding `UnknownCommand`. |
| `Location` | Where the session is: a folder, and the predicate being looked through, if any. |
| `IsRunning` | Whether a command is in flight. |
| `OutputChanged` | `Action<int, IReadOnlyList<string>>`, raised with the execution id and the complete current output lines. |
| `StoreChanged` | `Action<long>`, raised after every committed transaction with its sequence number. |
| `ExecuteAsync(source, executionId, cancellation)` | Parses and runs one line; never throws. |
| `RefreshAsync(source)` | Re-runs a read-only line for a live listing. Same response shape; commits nothing; refuses a line that names any command that could change something. |
| `Cancel()` | Cancels the running command; returns whether there was one. |
| `CompleteAsync(text, cursor)` | What the word at the cursor could become, and the signature of the command it is in, as a `CompletionResponse`. Asynchronous; see [Completion](#completion). |
| `DescribeAsync(text, offset)` | What the token ending at `offset` is, as a `HoverInfo`, or null. Asynchronous; see [Hover](#hover). |
| `Complete(text)` | The items `CompleteAsync` answers with the cursor at the end of the text, waited for. For tests, and a host that cannot await. |
| `Variables()` | Everything bound in scope. |

`Undo()` is gone: `undo` is a command, and so are `redo` and `history`. A host **must
not** offer a separate path to them, or the two will drift.

Rules an implementation **must** follow:

- `ExecuteAsync` returns a response for every outcome, including parse failure, command
  failure and cancellation. It **must not** propagate a user-level exception.
- An execution before `InitializeAsync` has completed **must** be refused with a fault
  of kind `Internal`. An empty filesystem and a lost one look identical to a user, so a
  host that forgets to await is told rather than shown nothing.
- A second `ExecuteAsync` while one is running **must** be refused with
  `A command is already running. Stop it first.`
- A cancelled execution **must** report `Stopped.` as its error, keeping whatever the
  command wrote.
- `OutputChanged` carries the complete set of lines, not a delta, so a listener can
  redraw without tracking state.

### Completion

`CompleteAsync(text, cursor)` answers what the word at the cursor could become, and the
signature of the command the cursor is in. It reads the line rather than guessing from
its text, and may run the stages before the cursor to learn what flows into the one
being written ([decision 0031](../decisions/0031-completion-reads-the-line.md)). The
reference implementation is `Core/Completion.fs`, which dispatches, `Core/Completion/`
for the providers, and `Session.Complete(text, cursor)`, which answers an
`Async<CompletionResult>`.

#### The word

The cursor is clamped to the text. The word under it runs back to the nearest
whitespace, `|`, `(` or `,`, or to the quote an unclosed string opened with, and forward
to the next whitespace, `|`, `(`, `,`, `)`, `"`, `>`, `}` or a `/` that has `>` or `}`
after it. A quoted word runs forward past its closing quote, or to the end of the line
when it has none.

Every item **must** carry the word's `start` and `end`, and applying an item **must**
replace that span and nothing else, so completing in the middle of a line keeps what
follows the word. `start` of a quoted word is at its opening quote, and the items for it
carry their quotes.

Matching against what is written of the word before the cursor is by prefix and ignores
case.

#### Reading the place

An implementation **must** find the place by parsing, not by inspecting the text around
the word:

1. Replace the word with a placeholder that is an identifier no one will type. A word
   that begins with `$`, `-`, `<` or `<$`, one that is `name=` or its value, and one
   inside a tag that is still open keep that form around the placeholder.
2. Close whatever the line left open: a quote, parentheses, an open tag, a variable tag.
3. Parse the result. When it does not parse, parse the line cut at the end of the word
   instead, in case what follows the cursor is what fails.
4. Find the placeholder in the tree. The node it is in names the place. Written
   arguments are counted as steps 1 and 2 of
   [argument binding](execution-model.md#argument-binding) count them, so the parameter
   named is the one the word would bind to.

A word that begins with `$`, `<` or `<$`, or that is inside an open tag, is classified
by its form, and the parse supplies the stage it is in. When nothing parses, the place
is `Unknown`.

| Place | Where the word is | Offers |
| --- | --- | --- |
| `Blank` | The line is empty or white space. | Nothing. The page shows its suggestion chips instead. |
| `CommandName` | Where a stage's command is named: the head of a line, after `\|`, `else`, `try` or `(`. | The commands whose name starts with the word, each with its description as the detail. From three letters, also the commands with a keyword that starts with the word, detail `rm · matches "delete"`, and at any length the commands with a keyword that is the whole word, so `cd` offers `in · matches "cd"`, and then the names one edit away from the word, two for a word longer than four letters, each with its description. Then `clear`, which the page handles, and the keyword `try`. After a pipe, only the commands with a parameter that takes the pipe, and not `clear`. When the stages before it were previewed and answered a table, not the commands whose piped parameters all take a `Path` or a `Place` either: `ls \| ` offers no `read`, `rm` or `in`, and `echo readme.txt \| ` offers all three. A line that could not be previewed keeps them. |
| `Variable` | `$` or `<$` and the start of a name. | The variables in scope whose name starts with what is written, ordered by name, each with its [summary](#a-value-in-one-line) as the detail, with the sigil written. Inside an argument handed to a *predicate* parameter, `$row` first, detail `the row being tested`; anywhere else `$row` **must not** be offered. |
| `Member` | After `$name.`, and after any members written after it. | The members of what the variable holds, the written members read first: a tag's attributes and a file's `name`, `kind`, `folder`, `path` and `id`, each with its summary; a fault's `kind`, `message`, `stage` and `path`, and `cause` when it has one. A number, a text, a boolean, a table and anything else have none; a table's columns are read off a row, through `$row.`. For `$row`, the columns of [what flows into the stage](#what-flows-into-a-stage), or of a listing of the current folder outside a stage, and after `$row.column.` the members of that column's value in the first row that has one. Each item is the whole word, `$row.kind`. |
| `Argument`, a parameter | An argument that would bind to a declared parameter. | By what the parameter [takes](#what-a-parameter-takes). |
| `Argument`, a flag | A word that starts with `-`. | `-name` for each parameter of kind `Single` that is optional or a switch, does not take the pipe and is not already written, the flag's own name where it declares one, each with its description. |
| `Argument`, an assignment | A plain word past the positional parameters of a command with an *assignments* parameter. | For `attr`, the attributes of the record its first plain argument names, as `name=`: its own attributes, then `name` and `kind`, leaving out `folder`, `created`, `modified` and `size` and those already assigned on the line, each with the summary of its value. Nothing once `name=` is written. |
| `Argument`, surplus | More arguments than the command takes, or any argument of a name that is not a command. | Files and folders, as for a path. |
| `Predicate`, an operand | Where a value starts in an argument handed to a *predicate* parameter: its start, after `not`, `and` or `or`. | `$row.`, `not` and `(`, each with a detail. For `in`'s first word, where a plain operand is a path ([decision 0013](../decisions/0013-attribute-filesystem.md)), folders and views instead. |
| `Predicate`, after an operand | An operand is written and nothing joins it to anything. | The eight comparison operators, `eq`, `ne`, `gt`, `ge`, `lt`, `le`, `like` and `has`. |
| `Predicate`, the right of a comparison | After a comparison operator whose left side is `$row.column`. | The distinct display texts of that column in the rows that flow in, most frequent first and then by ordinal comparison, at most 12, each with its count as the detail, `3 rows`. After `like`, each is followed by `*`. A value that would not read back as one word is written quoted. Nothing when the rows are not known, or the left side is anything else. |
| `Predicate`, after a comparison | A whole comparison is written. | `and` and `or`. |
| `TagType` | A word that starts with `<`. | `<type` for each kind of record in the store other than `folder` and `view`, and each type of tag a variable holds, alone or in a list, ordered by name, with the detail `4 records` and, for a variable's tag, `1 tag in variables`. |
| `TagAttribute` | Inside an open tag, after its type. | `name=` for each attribute that records of that kind, and tags of that type held by variables, carry: `name` first, then by name, leaving out `kind`, `folder`, `created`, `modified` and `size` and those already written in the tag, with how many of the records and tags carry it as the detail, `2 of 3 carry it`. |
| `Unknown` | No variant of the line parses, or the word is a tag attribute's value. | The [lexical rules](#the-lexical-rules). |

In a parameter's argument, an assignment's name and a surplus argument, the keyword
`else` is offered first, once two letters of it are there and the word is not quoted.

An implementation **must** answer each place by its own rule, and **must not** fall back
to files where a parameter takes something else. How items are ranked within a rule is
left to the implementation ([Design doc](design-doc.md#degree-of-constraint)).

#### What a parameter takes

A parameter declares what its argument is, as `Takes` (`Commands/Spec.fs`), and an
`Argument` place for it is answered by that declaration. Binding never reads it. The
[command catalogue](command-catalogue.md#what-each-parameter-takes) lists every
parameter's.

| `Takes` | Offers |
| --- | --- |
| `Anything`, `Path` | The records in the folder the word's directory part names, or the current folder: folders with a trailing `/`, files, and views by name. A name that a bare word cannot carry, and every name when the word is quoted, is written quoted. |
| `Place` | Folders and saved views only. |
| `Column` | The columns of what flows into the stage, each with its type. For a *rest* parameter, not the ones already written. |
| `Switch(on, off)` | `on`, with the parameter's description, and `off` when there is one. |
| `VariableName` | The variables in scope, without the `$`, each with its summary. |
| `CommandName` | The commands, each with its description. |
| `Value` | The variables in scope, with the `$`, each with its summary. |
| `Count`, `Number`, `NewName`, `Text`, `Url` | Nothing. The signature says what is wanted. |

`Anything` is the default, so a parameter that declares nothing is offered files and
folders, as every argument was before decision 0031.

#### The signature

For an `Argument` or a `Predicate` place in a command that exists, the response carries
the command's signature: its name, its description, and its parameters in declaration
order, each with its name, whether it is optional and its description. `active` is the
index of the parameter the word would bind to:

- the parameter itself, for a parameter's argument;
- the *assignments* parameter, for an assignment;
- the parameter whose name or flag the written flag names exactly, for a flag, and none
  while the flag is still being written;
- the *predicate* parameter, anywhere in a predicate;
- none for a surplus argument.

Every other place has no signature.

#### What flows into a stage

The columns a stage's argument is offered, and the values a comparison's right side is
offered, depend on what flows into the stage. An implementation **must** learn it as
follows, and **must** answer rather than fail whenever it cannot:

- At the head of a pipeline, nothing flows in. The answer is the columns a listing of
  the current folder would have, `name`, `kind`, `folder`, `size` and `modified` and
  every attribute a record there carries, with no rows.
- Otherwise the stages before it are written out as a line of their own, the upstream.
  A lone `$name` is read from the variables, and nothing runs. `$name | rest` runs as
  `echo $name | rest`.
- Any other upstream is run the way a [refresh](execution-model.md#refreshing) runs a
  line: refused before it runs unless every stage is read-only, committing nothing,
  touching neither the history nor the undo chain, and writing its output nowhere.
- A table answers its columns and its rows. A tag, or a list of tags, is read as a table
  the way the table functions read one
  ([decision 0009](../decisions/0009-table-coercion.md)). Any other value, and a tag
  that is not table-shaped, has no columns.
- A refusal, a fault, an unset variable at the head, a missed budget and a cancellation
  all answer the current folder's listing columns, with no rows.

The budget is 150 milliseconds from the start of the run. A run that waits on
something is answered for with the listing's columns when the budget is spent. It is
not stopped, and its answer is kept for the next request. A run that never waits has
nothing that could interrupt it, and its answer is used however long it took.

Answers are cached by the upstream text and the store's sequence number, so typing
inside the last stage does not run the stages before it again. Every committed
transaction clears the cache, and an answer to a run that started before a clear
**must not** be stored.

#### Cancellation and stale answers

Completion is asked on every keystroke, and an answer may arrive after the next one.

- Each `CompleteAsync` **must** cancel the one before it, so that an upstream run for a
  keystroke that has been superseded stops. A cancelled run answers the listing's
  columns and is not cached.
- A host **must** drop any answer that is not to its latest request. The reference
  page numbers its requests and keeps an answer only when its number is the latest.
- `DescribeAsync` is not a keystroke and **must not** cancel a completion in flight; it
  runs with a token of its own that is never cancelled.

#### A value in one line

A variable's detail, a member's detail and a hover all describe a value with the same
one-line summary (`Completion/Summary.fs`). A part that runs free, a text, a message or
a query, is cut to 40 characters, ending in `…`, with its line breaks folded to spaces.

| Value | Summary |
| --- | --- |
| `Empty`, `None` | `empty`, `none` |
| Number, boolean, text | `number · 5`, `boolean · true`, `text · "NotFound"` |
| A folder | `folder · documents` |
| Any other file | `file · readme.txt · text` |
| List | `list · 3 items` |
| Object, component | `tag · note · 2 attributes`, `component · panel · 1 attribute`, and the children when there are any, `tag · list · 0 attributes · 1 child` |
| Table | `table · 4 rows · name, kind, folder…`, naming at most three columns |
| Query | `query · $row.kind eq folder` |
| Fault | `fault · NotFound · File does not exist : /missing.txt` |

`vars` does not use these summaries: its `value` column holds the values themselves
(see [vars](command-catalogue.md#vars)).

#### The lexical rules

When the place is `Unknown`, the rules that answered before decision 0031 answer,
reading the text before the cursor. They are kept as the fallback, not deleted:

| Context | Offers |
| --- | --- |
| Empty line | nothing |
| The first word of a stage: at the start, after `\|`, after `(`, after `else`, after `try` | command names, `clear`, and the keyword `try` |
| A word starting with `$` and containing a `.` | column names after the stop: the record columns and every attribute in the current folder |
| A word starting with `$` | variable names, including the `$`, and `$row` |
| After an operand in a stage that has a `$` in it | the word operators |
| After `in` | folders, and files of kind `view` |
| Anything else | files and folders in the current folder, folders ending in `/`; and `else` once two letters of it are typed |

Their items replace from the start of the word to its end, as every other item does.

### Hover

`DescribeAsync(text, offset)` says what the token that ends at `offset` means where it
stands (`Completion/Hover.fs`). It reads the line as completion does, with the offset as
the cursor, and answers a `HoverInfo`, or null when there is nothing to say, in which
case a host shows the token's grammar role.

| The token | `kind` | `detail` | `signature` |
| --- | --- | --- | --- |
| A variable | `variable` | Its summary, or `not set`. `$row` is `the row being tested` inside a predicate, and `the row a predicate is testing, only inside where, find, in and save-view` anywhere else. | none |
| A member | `member` | For `$row.column`, `column · <type>` from what flows into the stage. For another variable, a table's column as `column · <type>`, or the summary of the member read, and none when there is no such member. | none |
| A command's name | `command` | The command's description. | The command's, with nothing active. |
| An operator in a predicate | `operator` | `and`: `true when both sides are true`; `or`: `true when either side is true`; `not`: `true when what follows is false`; a comparison: `true when <left> <meaning> <right>`, with `the value before it` or `the value after it` for a side that is not there. | none |
| A flag | `flag` | `<parameter> · <description>` of the parameter it names. | The command's, with that parameter active. |
| Any other argument of a command | `argument` | `<parameter> · <description>` of the parameter it binds to, or of the predicate parameter. | The command's, with that parameter active. |

The meanings of the comparisons are `is equal to`, `is not equal to`,
`is greater than`, `is greater than or equal to`, `is less than`,
`is less than or equal to`, `matches the pattern (* for anything)` and `contains`, so
`eq` in `ls | where $row.kind eq folder` reads `true when $row.kind is equal to folder`.

Nothing is said about an empty token, a tag's type or attribute, or a word whose place
is `Unknown`.

## Wire formats

Both web front ends serialise with .NET's web defaults: camel-cased property names.
The serialiser escapes quotes and characters outside ASCII, `\u00B7` for `·`; the
examples below are shown with those escapes decoded.

### Parse response

```json
{
  "type": "tokens",
  "source": "ls | echo",
  "tokens": [ { "text": "ls", "kind": "command" },
              { "text": " ",  "kind": "whitespace" } ],
  "reserialised": "ls | echo",
  "error": null
}
```

On failure, `tokens` is empty, `reserialised` is null and `error` is, for `<thing`:

```json
{"kind":"syntax","line":0,"column":6,"expected":["attribute name","/>",">"],"explanation":null,"sentence":"Syntax error at column 6: expected an attribute name or the end of the tag."}
```

`kind` is `syntax`, `lexical` or `error`. For `error`, `expected` carries the messages
and the position is zero. `explanation` is the sentence the grammar gave when it knew
why the input was wrong ([Errors](lexical-grammar.md#errors)), or null; for
`ls | where $row.kind eq`:

```json
{"kind":"syntax","line":0,"column":23,"expected":[],"explanation":"eq needs a value to compare with, such as folder","sentence":"Column 23: eq needs a value to compare with, such as folder"}
```

`sentence` is what running the line would show as its `error`, so that a page can say it
while the line is still being typed without a second copy of the wording. It is built
from the rest (`TerminalSession.Describe(ParseErrorInfo)`):

- with an explanation, `Column <column>: <explanation>`;
- for `error`, the messages joined with a space, or `Could not parse the command.` when
  there are none;
- otherwise `Syntax error at column <column>: expected <phrases>.`, `Lexical` for a
  lexical error, or `Syntax error at column <column>.` when there is nothing to name.

An implementation **must not** show a grammar label in a sentence. Each label the parser
expected becomes a phrase from this table, each phrase is named once, in the table's
order rather than the parser's, and the last two are joined with `or`: `a tag or a
component`, `an argument, a variable or a quoted string`.

| Labels | Phrase |
| --- | --- |
| `identifier` | a command name |
| `argument` | an argument |
| `$` | a variable |
| `variable name` | a variable name |
| `-` | a flag |
| `"`, `""`, `"""` | a quoted string |
| `(` | a parenthesised pipeline |
| `<`, `<$` | a tag |
| `{` | a component |
| `tag type` | a tag type |
| `attribute name` | an attribute name |
| `property name` | a property name |
| `column name` | a column name |
| `not` | 'not' |
| `eq`, `ne`, `gt`, `ge`, `lt`, `le`, `like`, `has`, `and`, `or` | an operator |
| `try` | 'try' |
| `??` | '??' |
| `else` | 'else' |
| `\|` | a pipe |
| `,` | a comma |
| `)` | a closing parenthesis |
| `]` | a closing bracket |
| `/>`, `>` | the end of the tag |
| `/}`, `}` | the end of the component |
| `</`, `{/`, `[/` | a closing tag |
| `/` | nothing: it is only expected because a word could go on |
| `end of input` | the end of the line |

A label that is not in the table is named quoted, `'label'`, after the ones that are.

### Execution response

```json
{
  "type": "result",
  "source": "ls",
  "tokens": [ { "text": "ls", "kind": "command" } ],
  "output": ["100%", "Progress test finished"],
  "result": [ { "kind": "folder", "text": "documents", "path": "/documents" } ],
  "resultText": "documents",
  "error": null,
  "fault": null,
  "location": { "folder": "/", "view": null },
  "changes": { "committed": [], "undone": [], "redone": [], "reset": false }
}
```

`changes` says what the line did to the log, by sequence number. `committed` lists the
lines it committed: none for a line that changed nothing, one usually, one per script
line for `run`. `undone` and `redone` list the lines it undid and redid, and **must**
name the line itself, never the compensation that reversed it, so a redo names the line
the undo had reversed rather than the undo. The seed is never listed
([decision 0018](../decisions/0018-the-seed-is-not-a-line-anyone-typed.md)). `reset` is
true when the log was emptied while the line ran; numbers start again after it, so a
host **must** forget the ones it remembered. A refresh's `changes` is always empty.

`result` is the value flattened for display. Kinds are `file`, `folder`, `object`,
`component`, `number`, `boolean`, `none`, `table`, `query`, `fault` and `text`. A list flattens
into its items. `path` is present only for files, and is the value's argument string,
so inserting it into a line resolves. `resultText` is the value's display string.

A table is one item and keeps its shape, with `columns` and `rows` beside its `text`:

```json
{ "kind": "table",
  "columns": [ { "name": "name", "type": "file" }, { "name": "size", "type": "number" } ],
  "rows": [ [ { "kind": "file", "text": "notes.txt", "path": "/documents/notes.txt" },
              { "kind": "number", "text": "50" } ] ] }
```

A fault that `try` or `else` made into a value is a result, not an error: `error` and
`fault` are null, and the item carries the fault's kind beside its message, with the
path it was about, if any, in `path`:

```json
{ "kind": "fault", "text": "File does not exist : /x", "path": "/x", "faultKind": "NotFound" }
```

A host **must** draw it differently from a failed line, because the line did not fail.

A column's `type` is `text`, `number`, `boolean`, `file`, `object` or `mixed`. Each
cell **must** be described by the same rules as a standalone value, so a file in a
listing still carries its path.

`error` carries a user-facing sentence, or null. For a line that does not parse it is
the parse error's `sentence`; otherwise it is the fault's message. `fault` carries the
message with structure beside it:

```json
{ "kind": "NotFound", "message": "Directory does not exist : nowhere", "stage": 1, "path": "nowhere" }
```

`kind` here, and `faultKind` on a caught fault, is the kind's word exactly as
`$f.kind` reads it.

A host **must** keep `error` as the sentence it always was; `fault` is additional.

`location` is where the session is after the line. A folder is `/` at the root, with no trailing separator. `view` is null,
or the predicate as it was written. The two are independent: a view does not replace
the folder, because a new file still lands there.

### Completion response

What `Complete(text, cursor)` answers, here for `$` in a session where `v`, `files` and
`problem` are set:

```json
{"items":[{"kind":"variable","text":"$files","start":0,"end":1,"detail":"table · 4 rows · name, kind, folder…"},{"kind":"variable","text":"$problem","start":0,"end":1,"detail":"fault · NotFound · File does not exist : /missing.txt"},{"kind":"variable","text":"$v","start":0,"end":1,"detail":"number · 5"}],"signature":null}
```

and for `ls | sort name d`:

```json
{"items":[{"kind":"keyword","text":"desc","start":15,"end":16,"detail":"Write 'desc' to order downwards"}],"signature":{"command":"sort","description":"Order the rows by a column","parameters":[{"name":"column","optional":false,"description":"The column to order by"},{"name":"desc","optional":true,"description":"Write 'desc' to order downwards"},{"name":"table","optional":true,"description":"The table to work on; taken from the pipe when it is not written"}],"active":1}}
```

An item replaces the text from `start` to `end` with `text`. `kind` is one of `command`,
`keyword`, `variable`, `member`, `operator`, `flag`, `column`, `value`, `file`, `folder`
and `view`, and a host colours by it. `detail` is a line about the item, or null.
`signature` is null outside a command's arguments; `active` is an index into
`parameters`, or null.

### Hover response

What `Describe(text, offset)` answers, here for `ls | where $row.kind eq folder` with
the offset at the end of `eq`:

```json
{"kind":"operator","text":"eq","detail":"true when $row.kind is equal to folder","signature":null}
```

and for `ls | sort name` with the offset at the end of `name`:

```json
{"kind":"argument","text":"name","detail":"column · The column to order by","signature":{"command":"sort","description":"Order the rows by a column","parameters":[{"name":"column","optional":false,"description":"The column to order by"},{"name":"desc","optional":true,"description":"Write 'desc' to order downwards"},{"name":"table","optional":true,"description":"The table to work on; taken from the pipe when it is not written"}],"active":0}}
```

`kind` is `variable`, `command`, `member`, `operator`, `flag` or `argument`; `detail`
and `signature` may each be null. The whole response is `null` when there is nothing to
say about the token (see [Hover](#hover)).

### Other shapes

```json
{ "name": "write", "description": "...", "parameters": [ { "name": "path", "optional": false } ] }
{ "persistent": true, "replayed": 12, "unreadable": 0, "reason": null }
{ "name": "v", "text": "5", "items": [ { "kind": "number", "text": "5", "path": null } ] }
```

## Browser bridge

Exported from the `WebClient` assembly and callable through Blazor's interoperability
layer.

| Call | Returns |
| --- | --- |
| `Parse(source)` | Parse response, as a JSON string. Synchronous. |
| `Execute(source, executionId)` | Execution response, as a JSON string. Asynchronous. |
| `Refresh(source)` | Execution response for a re-read, as a JSON string. Asynchronous. Commits nothing, and answers a fault for a line that is not read-only. |
| `Cancel()` | `true` when a command was running. |
| `Initialize()` | Opens the store and replays the log. Asynchronous, and **must** be awaited before the input is enabled. Answers with the store's status. |
| `Status()` | The store's status again, as a JSON string. The page asks after every line, so storage that stops answering mid-session shows as `not persisted` when it happens. |
| `Commands()` | Command summaries, as a JSON string. |
| `Complete(text, cursor)` | Completion response, as a JSON string. Asynchronous. |
| `Describe(text, offset)` | Hover response, as a JSON string, or `null`. Asynchronous. |
| `Variables()` | Variable summaries, as a JSON string. |
| `Location()` | The location, as a JSON string. |

```js
const response = JSON.parse(
    await DotNet.invokeMethodAsync('WebClient', 'Execute', 'ls', 7));
```

### Live output

While a command runs, the bridge calls into the page:

```js
window.terminal = {
  output(executionId, lines) { /* lines is the complete current output */ },
  storeChanged(sequence)     { /* a transaction was committed */ }
};
```

The host **must** coalesce the output calls; the reference implementation sends at most
one every 40 milliseconds, because a download updates its counter on every few
kilobytes and each call costs more than the redraw. A page **must** tolerate missing
updates: the final response carries every line.

A page **should** ignore an update whose `executionId` is not the command it is
currently showing.

### Live listings

`storeChanged` is raised after every committed transaction, and is how a page knows a
listing on screen has gone stale. It is raised from inside the commit, which is inside
the call the page is still awaiting, so:

- The host **must not** require the page to answer synchronously, and **must not** fail
  an execution because the call into the page failed.
- A page **must** defer its refresh until nothing is running; `Refresh` during an
  execution is refused like any other concurrent call.
- A missed `storeChanged` **must** cost nothing worse than a stale table until the
  next one.

## ASP.NET host

Optional, for tooling. It serves the same client plus:

| Endpoint | Behaviour |
| --- | --- |
| `GET /healthz` | `{ "status": "ok", "utc": ..., "runtime": ... }`. |
| `GET /api/parse?q=<text>` | A parse response. |
| `WS /ws` | Sends `{ "type": "hello", "utc": ... }` on connect, then a parse response per message. |

A WebSocket message is `{ "type": "parse", "text": "<line>" }`. Malformed JSON is
answered with `{ "type": "error", "message": "Malformed request." }` rather than by
closing the socket.

This host parses only. It does not execute commands, because execution belongs to the
client where the filesystem is.
