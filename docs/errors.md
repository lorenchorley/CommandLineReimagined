# Error reference

Every message the terminal can show, what it means, and what to do. Messages are
grouped by where they come from.

Errors never stop the session. The command that failed changed nothing beyond what it
had already done, and the next line runs normally.

## Parse errors

These appear before anything runs, because the line could not be turned into a tree.

### `Syntax error at column N: expected ...`

The parser reached column N, counted from zero, and could not continue. The list names
the symbols the grammar would have accepted there.

```
$ <thing
Syntax error at column 6: expected identifier, />, >.
```

Common causes:

| Line | Why |
| --- | --- |
| `<thing` | A tag was never closed. |
| `{renderer` | A component tag was never closed. |
| `echo --double` | Two dashes. A flag takes one. |
| `mkdir \|` | A pipe with nothing after it. |
| `[size=3]` | A property tag outside a tag body. |
| `echo "unterminated` | A string was never closed. |

### `Closing tag 'b' does not match opening tag 'a'`

The shape was valid but the names disagree. Either repeat the opening type or use the
empty closing form:

```
$ <a></a>
<a/>
$ <a></>
<a/>
```

### `Lexical error at column N`

A character that cannot begin any token. Rare; usually a stray symbol such as `&`.

### `Could not parse the command.`

A parse failure with no message attached. It should not happen; if you can reproduce
it, the line is worth reporting.

## Argument errors

These come from matching what you wrote against what the command declared.

### `'<command>' needs an argument for '<parameter>'.`

A required parameter was not given, nothing was piped in, and it has no default.

```
$ cd
'cd' needs an argument for 'TargetPath'.
```

Give the argument, or pipe a value into a parameter that accepts one.

### `'<command>' takes N arguments, but M were given.`

More arguments than the command has parameters. N counts every parameter, optional ones
included.

```
$ ls documents extra
'ls' takes 1 argument, but 2 were given.
```

Quote an argument that was meant to be one word: `write note.txt "two words"`.

### `'<command>' has no argument named '<name>'.`

A flag or a named argument that the command does not declare.

```
$ echo -verbose
'echo' has no argument named 'verbose'.
```

Run `help` to see the parameters a command actually takes.

### `Unknown variable : $<name>`

A `$name` that is not bound. Check `vars` for what is bound, and remember that `set $x`
reads `$x` rather than naming it.

### `Unsupported argument value : <node>`

The parser produced a value the binder does not know how to evaluate. Strings, words,
numbers, variable references and tags are all handled, so this means the grammar
accepts something execution does not implement yet. It is a gap, not your mistake.

## Execution errors

Raised by commands themselves. The command reports the path it actually used, which is
resolved against the current directory.

| Message | Meaning |
| --- | --- |
| `Unknown command : <name>` | No command by that name. Run `help`. |
| `Directory does not exist : <path>` | `cd`, `ls` or `write` could not find it. |
| `File does not exist : <path>` | `cat` or `cp` could not find it. |
| `That is a directory, not a file : <path>` | `cat` or `write` was given a directory. |
| `Target directory already exists : <path>` | `mkdir` will not overwrite. |
| `Target file already exists : <path>` | `cp` will not overwrite. Use `write` or `rm` first. |
| `Target directory does not exist : <path>` | `cp` or `download` has nowhere to put it. |
| `Nothing exists at : <path>` | `rm` found neither a file nor a directory. |
| `Directory is not empty : <path>` | `rm` only deletes empty directories. |
| `Cannot delete the current directory.` | Move out of it first. |
| `'<name>' is not a valid variable name.` | Letters, digits and underscore only. |
| `'set' needs a value for '<name>'.` | `set` was given a name but an empty value. |
| `'steps' must be at least 1.` | `progress` was given zero or a negative count. |
| `'steps' must be a whole number, not '<value>'.` | `progress` was given something that is not a number, such as a bare `-steps` flag. |
| `Not a valid URL : <text>` | `download` needs an absolute URL. |
| `The server did not report a content length.` | `download` cannot show progress without one. |
| `The transfer ended before all bytes arrived.` | The connection closed early. |

## Evaluation errors

From building a value out of a tag.

| Message | Meaning |
| --- | --- |
| `Cannot evaluate a child PropertyAssignment.` | Property tags parse but are not evaluated yet. |
| `Cannot evaluate a <node>.` | A tree shape execution does not handle. A gap, not your mistake. |
| `Unknown command type : <type>` | A command that is neither synchronous nor asynchronous. Internal. |

## Terminal messages

Not errors from the language, but from the session.

| Message | Meaning |
| --- | --- |
| `A command is already running. Stop it first.` | One command at a time. Press Stop. |
| `Stopped.` | You cancelled the command. It may have written its own note as well, such as `Cancelled at 9%`. |
| `Nothing to undo.` | The history is empty. |
| `Undone: <command>` | Undo reversed that command. Read-only commands are on the history too. |

## Reading a path in a message

Most messages quote the resolved path rather than what you typed. Run `cat notes.txt`
in `/home/terminal` and the message names `/home/terminal/notes.txt`. That is
deliberate: the most common cause of a missing file is being somewhere else than you
thought, and the prompt above the input always shows where you are.

`cd` is the exception. It reports the target as you wrote it, because the thing you
usually need to see there is your own spelling.
