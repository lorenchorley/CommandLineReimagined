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
```

Asynchronous throughout, because the browser's storage is and WebAssembly is single
threaded, so there is nowhere to block. An implementation **must** store content by the
hash of its text, so that storing the same text twice stores it once.

`InMemoryLog` is supplied. A persistent implementation is Phase 2.

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

## TerminalSession

The reference host-side object, shared by both web front ends.

| Member | Contract |
| --- | --- |
| `TerminalSession(ILog? log)` | Builds a session over the log, in memory by default. Does not replay it. |
| `InitializeAsync()` | Replays the log, and seeds it when it was empty. **Must** be awaited before any execution. |
| `Commands` | Every command as `CommandSummary`, sorted by name, excluding `UnknownCommand`. |
| `Location` | Where the session is: a folder, and from Phase 4 possibly a view. |
| `IsRunning` | Whether a command is in flight. |
| `OutputChanged` | `Action<int, IReadOnlyList<string>>`, raised with the execution id and the complete current output lines. |
| `StoreChanged` | `Action<long>`, raised after every committed transaction with its sequence number. |
| `ExecuteAsync(source, executionId, cancellation)` | Parses and runs one line; never throws. |
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
| First word, or first word after `\|` | command names, plus `help` and `clear` |
| A word starting with `$` | variable names, including the `$` |
| Anything else | files and folders in the current folder, folders ending in `/` |

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
  "location": { "folder": "/", "view": null },
  "workingDirectory": "/"
}
```

`result` is the value flattened for display. Kinds are `file`, `folder`, `parent`,
`object`, `component`, `number`, `boolean`, `none` and `text`. A list flattens into its
items. `path` is present only for files, and is the value's argument string, so
inserting it into a line resolves. `resultText` is the value's display string.

`error` carries a user-facing sentence, or null. `fault` carries the same message with
structure beside it:

```json
{ "kind": "NotFound", "message": "Directory does not exist : nowhere", "stage": 1, "path": "nowhere" }
```

A host **must** keep `error` as the sentence it always was; `fault` is additional.

`location` replaces `workingDirectory`, which is kept as an alias for one phase and
then removed. A folder is `/` at the root, with no trailing separator.

### Other shapes

```json
{ "name": "write", "description": "...", "parameters": [ { "name": "path", "optional": false } ] }
{ "kind": "folder", "text": "documents/", "start": 3 }
{ "name": "v", "text": "5", "items": [ { "kind": "number", "text": "5", "path": null } ] }
```

## Browser bridge

Exported from the `WebClient` assembly and callable through Blazor's interoperability
layer.

| Call | Returns |
| --- | --- |
| `Parse(source)` | Parse response, as a JSON string. Synchronous. |
| `Execute(source, executionId)` | Execution response, as a JSON string. Asynchronous. |
| `Cancel()` | `true` when a command was running. |
| `Initialize()` | Replays the log. Asynchronous, and **must** be awaited before the input is enabled. |
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
  output(executionId, lines) { /* lines is the complete current output */ }
};
```

The host **must** coalesce these calls; the reference implementation sends at most one
every 40 milliseconds, because a download updates its counter on every few kilobytes
and each call costs more than the redraw. A page **must** tolerate missing updates: the
final response carries every line.

A page **should** ignore an update whose `executionId` is not the command it is
currently showing.

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
