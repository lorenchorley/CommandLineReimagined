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

There are no host-specific commands. `debug`, which required the entity component
system, was dropped when the command layer moved to the core; a host that wants it
supplies it.

Every command below returns events rather than changing anything. The `Undo` rows say
what reversing the line does, which is a consequence of the events, not a method the
command implements.

## Navigation

### ls

| Field | Value |
| --- | --- |
| Name | `ls` |
| Parameters | `path` (optional, flag `path`) |
| Returns | `List` of `File` |
| Undo | none |

Lists `path`, or the current folder. The list **must** be ordered: the parent entry
first when the folder being listed is not the root, then folders, then files, each
group ordered by name with an ordinal comparison.

This ordering is normative. It previously said entry order "follows the filesystem",
which meant it was whatever the host happened to return and could not be tested at all.

Errors: `Directory does not exist : <path>`.

### cd

| Field | Value |
| --- | --- |
| Name | `cd` |
| Parameters | `TargetPath` (piped) |
| Returns | `Text` of the new current folder's path |
| Undo | returns to the previous directory |

The target **must** be normalised, so `cd ..` yields the parent's real path.

Errors: `Directory does not exist : <target>`, quoting the target as written.

### up

| Field | Value |
| --- | --- |
| Name | `up` |
| Parameters | none |
| Returns | `Text` of the new current folder's path |
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
| Returns | `Text` of the current folder's path |
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
| Returns | `File` |
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
| Returns | `File` |
| Undo | deletes the copy |

The copy keeps the source's file name. Overwriting is refused.

Errors: `File does not exist : <source>`,
`Target directory does not exist : <target>`, `Target file already exists : <path>`.

### mkdir

| Field | Value |
| --- | --- |
| Name | `mkdir` |
| Parameters | `FolderName` |
| Returns | `File` of the new folder |
| Undo | deletes it |

Errors: `Target directory already exists : <path>`, including when the name is taken by
a file, since names are unique within a folder across files and folders together
([decision 0016](../decisions/0016-folders-as-records.md)).

## Attributes

### attr

| Field | Value |
| --- | --- |
| Name | `attr` |
| Parameters | `path` (piped), `assignments` (kind `Assignments`) |
| Returns | `List` of `Text` with no assignments, otherwise the `File` |
| Undo | restores every attribute the record had |

With no assignments it **must** return one `Text` per attribute, formatted
`name = value`. With assignments it emits `AttributesChanged` and returns the file.

`name` and `kind` may be written and **must** be validated: renaming onto a name
already used in the folder is a `Conflict`. `folder`, `created` and `modified` are the
runtime's and **must** be refused.

Errors: `File does not exist : <path>`, `'<name>' is set by the terminal and cannot be
written.`, `Target file already exists : <path>`.

### save

| Field | Value |
| --- | --- |
| Name | `save` |
| Parameters | `tag` (piped) |
| Returns | `File` |
| Undo | deletes the record |

Creates a record from an object tag. The tag's type name becomes `kind`, its `name`
attribute becomes `name`, and every other attribute is copied across. The record has no
content, so `cat` on it **must** return empty text rather than failing.

Errors: `A saved tag needs a 'name' attribute.`, `Target file already exists : <path>`,
`'save' needs a tag, not <kind>.`

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
| Returns | `File` |
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

### undo

| Field | Value |
| --- | --- |
| Name | `undo` |
| Parameters | none |
| Returns | `Text` naming the line it reversed, or `Nothing to undo.` |
| Meta | yes |

Reverses the latest undoable, uncompensated line. Having nothing to undo **must not**
be a fault.

### redo

| Field | Value |
| --- | --- |
| Name | `redo` |
| Parameters | none |
| Returns | `Text` naming the line it restored, or `Nothing to redo.` |
| Meta | yes |

Reverses the latest undo that has not itself been reversed, and **must** name the
original line rather than the undo. Having nothing to redo **must not** be a fault.

### history

| Field | Value |
| --- | --- |
| Name | `history` |
| Parameters | none |
| Returns | `List` of `Text`, or `Text "Nothing has happened yet."` |
| Meta | yes |

One line per transaction, oldest first, formatted `<seq>  <HH:mm:ss>  <source>`, with
`  (undone)` appended where the line's effect is not currently in force.
