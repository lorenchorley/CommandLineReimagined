# Command catalogue

The normative contract of each built-in command: its parameters, its result, what it
writes, what it reverses and how it fails. The
[user-facing reference](../commands.md) covers the same ground with examples.

A parameter marked *piped* has `AcceptsPipedInput`. A parameter marked *optional* is an
`OptionalCommandParameter`. Parameters are listed in declaration order, which is the
order positional arguments fill them.

Every path argument is resolved against the current directory unless it is rooted.
Messages quote the resolved path except where noted.

## Registration

A host chooses which commands to register. The browser terminal registers every
command below. `UnknownCommand` **must** be registered by any host that wants an
unrecognised name reported as a normal failure; see
[Resolving a command](execution-model.md#resolving-a-command).

`debug` requires the entity component system and **must not** be registered by a host
without one.

## Navigation

### ls

| Field | Value |
| --- | --- |
| Name | `ls` |
| Parameters | `path` (optional, flag `path`) |
| Returns | `ListValue` of `PathValue` |
| Undo | none |

Lists `path`, or the current directory. The list **must** be ordered: the parent entry
first when the current directory is not the filesystem root, then directories, then
files. Entry order within each group follows the filesystem.

Errors: `Directory does not exist : <path>`.

### cd

| Field | Value |
| --- | --- |
| Name | `cd` |
| Parameters | `TargetPath` (piped) |
| Returns | `PathValue` of the new current directory |
| Undo | returns to the previous directory |

The target **must** be normalised, so `cd ..` yields the parent's real path.

Errors: `Directory does not exist : <target>`, quoting the target as written.

### up

| Field | Value |
| --- | --- |
| Name | `up` |
| Parameters | none |
| Returns | `PathValue` of the new current directory |
| Undo | returns to the previous directory |

At the filesystem root, moving up **must** leave the current directory unchanged and
**must not** fail. The directory a session starts in is not a boundary: a user may
navigate above it, and what is reachable from there is the host's filesystem, which
under WebAssembly is the page's in-memory one.

### pwd

| Field | Value |
| --- | --- |
| Name | `pwd` |
| Parameters | none |
| Returns | `PathValue` of the current directory |
| Undo | none |

## Files

### cat

| Field | Value |
| --- | --- |
| Name | `cat` |
| Parameters | `path` (piped) |
| Returns | `TextValue` of the file's contents |
| Undo | none |

Errors: `That is a directory, not a file : <path>`, `File does not exist : <path>`.

### write

| Field | Value |
| --- | --- |
| Name | `write` |
| Parameters | `path`, `text` (piped) |
| Returns | `PathValue` of the file |
| Undo | restores the previous contents, or deletes a file that did not exist |

The text written is the value's **display** string, so writing a list writes what the
list reads as.

Errors: `That is a directory, not a file : <path>`,
`Directory does not exist : <parent>`. Creating missing parents is **not** permitted.

### rm

| Field | Value |
| --- | --- |
| Name | `rm` |
| Parameters | `path` (piped) |
| Returns | `TextValue`, `Removed <name>` |
| Undo | restores the file with its contents, or recreates the directory |

A directory **must** be empty to be deleted, and the current directory **must not** be
deletable. Recursive deletion is deliberately absent: it cannot be undone from a
snapshot of one entry.

Errors: `Directory is not empty : <path>`, `Cannot delete the current directory.`,
`Nothing exists at : <path>`.

### cp

| Field | Value |
| --- | --- |
| Name | `cp` |
| Parameters | `sourcePathAndFile`, `targetPath` |
| Returns | `PathValue` of the new file |
| Undo | deletes the copy |

The copy keeps the source's file name. Overwriting is refused.

Errors: `File does not exist : <source>`,
`Target directory does not exist : <target>`, `Target file already exists : <path>`.

### mkdir

| Field | Value |
| --- | --- |
| Name | `mkdir` |
| Parameters | `FolderName` |
| Returns | `PathValue` of the new directory |
| Undo | deletes it |

Errors: `Target directory already exists : <path>`.

## Values

### echo

| Field | Value |
| --- | --- |
| Name | `echo` |
| Parameters | `text` (piped) |
| Returns | the value it was given, unchanged |
| Undo | none |

`echo` **must not** convert its argument. A number stays a number and a list stays a
list; this is how a test or a user inspects what a pipe carries.

### set

| Field | Value |
| --- | --- |
| Name | `set` |
| Parameters | `name`, `value` (piped) |
| Returns | the bound value |
| Undo | restores the previous binding, or unbinds a new name |

The name **must** consist of letters, digits and underscore. Returning the value lets
`set` sit mid-pipeline.

Errors: `'<name>' is not a valid variable name.`, `'set' needs a value for '<name>'.`

### vars

| Field | Value |
| --- | --- |
| Name | `vars` |
| Parameters | none |
| Returns | `ListValue` of the values, or `Empty` when nothing is bound |
| Undo | none |

Writes one output line per variable, `$<name> = <display>`, ordered by name. With
nothing bound it writes one line inviting the user to bind something.

## Long-running commands

Both are `CommandActionAsync`. Both **must** observe the invocation's cancellation
token.

### progress

| Field | Value |
| --- | --- |
| Name | `progress` |
| Parameters | `steps` (optional, default 100), `delay` (optional, default 100 ms) |
| Returns | `NumberValue`, the percentage reached |
| Undo | writes `Progress test undone` |

Writes two output lines and updates them in place: a percentage and a bar. On success
it writes `Progress test finished`; on cancellation `Cancelled at N%`; on failure
`Progress test failed : <reason>`.

Errors: `'steps' must be at least 1.`,
`'steps' must be a whole number, not '<value>'.`

This command exists to exercise the asynchronous path. It **must not** touch the
filesystem.

### download

| Field | Value |
| --- | --- |
| Name | `download` |
| Parameters | `url` (optional), `into` (optional, default the current directory) |
| Returns | `PathValue` of the downloaded file |
| Undo | deletes the file |

Writes three output lines and updates them in place: a percentage, a bar and a rate in
MB/s. An existing file at the destination is deleted before the transfer starts.

The implementation **must** stream to disk rather than buffering the response, and
**must** fail rather than loop when the server closes early.

Errors: `Not a valid URL : <text>`, `Target directory does not exist : <path>`,
`The server did not report a content length.`,
`The transfer ended before all bytes arrived.` A failure is also written as
`Download failed : <reason>`.

## Host commands

### exit

| Field | Value |
| --- | --- |
| Name | `exit` |
| Parameters | none |
| Returns | `Empty` |
| Undo | none |

Calls `IApplicationLifetime.Shutdown`. A command **must not** reach a window or a
process directly; what shutting down means is the host's decision, and a host with
nothing to close **may** do nothing.

### UnknownCommand

| Field | Value |
| --- | --- |
| Name | `UnknownCommand` |
| Parameters | `name` |
| Returns | never returns |

Raises `Unknown command : <name>`. It is not offered in listings or completion.

### debug

Desktop shell only. Writes the entity and component tree to a file, and can open it.
Parameters: `open` (optional).
