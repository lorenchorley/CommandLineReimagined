# Host interfaces

What a host must provide to run the execution layer, and the wire formats the browser
front end depends on.

## Interfaces the execution layer requires

### ICommandOutput

Where a command writes while it runs, as distinct from the value it returns.

```csharp
public interface ICommandOutput
{
    IOutputLine NewLine();
    void AbandonLine(IOutputLine line);
}

public interface IOutputLine
{
    IOutputText Write(string description, string text);
}

public interface IOutputText
{
    string Text { get; set; }
}
```

An implementation **must** let a command mutate `Text` after writing it, which is how a
progress indicator updates in place, and **must** keep written segments distinct within
a line so changing one does not disturb another.

`description` is a label for the host, not for the user: the desktop shell uses it to
name the entity it creates.

A host **should** implement `IClearableOutput` when it can withdraw what was written;
undo uses it to remove a command's output.

```csharp
public interface IClearableOutput { void Clear(); }
```

A host **must not** require a command to know anything else about presentation. This is
the interface that keeps commands free of a window, a render loop or a graphics library,
and it is why they run under WebAssembly.

### IApplicationLifetime

```csharp
public interface IApplicationLifetime { void Shutdown(); }
```

`exit` calls it. A host with nothing to close **may** implement it as a no-op.

### Service resolution

The evaluator resolves a command by its `CommandActionType` from an
`IServiceProvider`, and enumerates `ICommandAction` to discover definitions. A host
**must** register each command so that both work, and **must** register them so that
each execution receives its own instance.

## TerminalSession

The reference host-side object, shared by both web front ends.

| Member | Contract |
| --- | --- |
| `TerminalSession(string? rootDirectory)` | Creates the registry, scope and working directory. Seeds the root when it is empty. |
| `Commands` | Every registered command as `CommandSummary`, sorted by name, excluding `UnknownCommand`. |
| `WorkingDirectory` | The current directory. |
| `IsRunning` | Whether a command is in flight. |
| `OutputChanged` | `Action<int, IReadOnlyList<string>>`, raised with the execution id and the complete current output lines. |
| `ExecuteAsync(source, executionId, cancellation)` | Parses and runs one line; never throws for user error. |
| `Cancel()` | Cancels the running command; returns whether there was one. |
| `Undo()` | Undoes the last command. |
| `Complete(text)` | Completions for the last word. |
| `Variables()` | Everything bound in scope. |

Rules an implementation **must** follow:

- `ExecuteAsync` returns a response for every outcome, including parse failure, command
  failure and cancellation. It **must not** propagate a user-level exception.
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
| First word, or first word after `\|` | command names, plus `help`, `clear`, `undo` |
| A word starting with `$` | variable names, including the `$` |
| Anything else | files and directories, directories ending in `/` |

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
  "result": [ { "kind": "directory", "text": "documents", "path": "/home/terminal/documents" } ],
  "resultText": "documents\\",
  "error": null,
  "workingDirectory": "/home/terminal"
}
```

`result` is the value flattened for display. Kinds are `file`, `directory`, `parent`,
`object`, `component`, `number`, `boolean` and `text`. A list flattens into its items.
`path` is present only for paths. `resultText` is the value's display string.

`error` carries a user-facing sentence, or null.

### Other shapes

```json
{ "name": "write", "description": "...", "parameters": [ { "name": "path", "optional": false } ] }
{ "kind": "directory", "text": "documents/", "start": 3 }
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
| `Undo()` | Execution response, as a JSON string. |
| `Commands()` | Command summaries, as a JSON string. |
| `Complete(text)` | Completions, as a JSON string. |
| `Variables()` | Variable summaries, as a JSON string. |
| `WorkingDirectory()` | The current directory, as a plain string. |

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
