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

There is no way to withdraw what was written. Undo used to erase a command's output;
`undo` is a line of its own now and writes its own result, so the block it reverses
stays on screen as a record of what happened.

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
a variable can hold (`try cat x | set problem`); every version 1 document
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
| `Complete(text)` | Completions for the last word. |
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

`Complete` returns whole replacements for the last word, with the offset at which to
apply them.

| Context | Offers |
| --- | --- |
| Empty line | nothing |
| The first word of a stage: at the start, after `\|`, after `(`, after `else`, after `try` | command names, `clear`, and the keyword `try` |
| A word starting with `$` and containing a `.` | column names after the stop: the record columns and every attribute in the current folder |
| A word starting with `$` | variable names, including the `$`, and `$row` |
| After an operand in a stage that has a `$` in it | the word operators |
| After `cd` | folders, and files of kind `view` |
| Anything else | files and folders in the current folder, folders ending in `/`; and `else` once two letters of it are typed |

The last word starts after the nearest preceding whitespace, `|`, `(` or `,`. Matching
is case-insensitive and by prefix. A path completion's text includes whatever directory
prefix the user already typed, so applying it never loses their position.

## Wire formats

Both web front ends serialise with .NET's web defaults: camel-cased property names.

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

On failure, `tokens` is empty, `reserialised` is null and `error` is:

```json
{ "kind": "syntax", "line": 0, "column": 6, "expected": ["identifier", "/>", ">"] }
```

`kind` is `syntax`, `lexical` or `error`. For `error`, `expected` carries the messages
and the position is zero.

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
  "location": { "folder": "/", "view": null }
}
```

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

`error` carries a user-facing sentence, or null. `fault` carries the same message with
structure beside it:

```json
{ "kind": "NotFound", "message": "Directory does not exist : nowhere", "stage": 1, "path": "nowhere" }
```

`kind` here, and `faultKind` on a caught fault, is the kind's word exactly as
`$f.kind` reads it.

A host **must** keep `error` as the sentence it always was; `fault` is additional.

`location` is where the session is after the line. A folder is `/` at the root, with no trailing separator. `view` is null,
or the predicate as it was written. The two are independent: a view does not replace
the folder, because a new file still lands there.

### Other shapes

```json
{ "name": "write", "description": "...", "parameters": [ { "name": "path", "optional": false } ] }
{ "kind": "folder", "text": "documents/", "start": 3 }
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
| `Commands()` | Command summaries, as a JSON string. |
| `Complete(text)` | Completions, as a JSON string. |
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
