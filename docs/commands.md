# Command reference

Every command available in the browser terminal, in the order `help` lists them.

Each entry gives the parameters in declaration order, which is the order positional
arguments fill them. A parameter marked **piped** takes the piped value when you do not
write it out. A parameter marked **optional** may be omitted.

Paths are relative to the current directory unless they start with a separator or a
drive. `..` and `.` work.

## Summary

| Command | Purpose | Changes anything? |
| --- | --- | --- |
| [`attr`](#attr) | Show a file's attributes, or set them | only when given `name=value` |
| [`cat`](#cat) | Read a file as text | no |
| [`cd`](#cd) | Enter a directory | yes, where you are |
| [`cp`](#cp) | Copy a file into a directory | yes |
| [`download`](#download) | Download a file, with progress | yes |
| [`echo`](#echo) | Return an argument or the piped value | no |
| [`exit`](#exit) | Ask the host to close | no |
| [`history`](#history) | The lines that changed something | no |
| [`ls`](#ls) | List a directory | no |
| [`mkdir`](#mkdir) | Create a directory | yes |
| [`progress`](#progress) | Run a progress bar, to exercise long commands | no |
| [`pwd`](#pwd) | The current directory | no |
| [`redo`](#redo) | Put back what `undo` took away | yes |
| [`reset`](#reset) | Empty the log and start again | yes, and cannot be undone |
| [`rm`](#rm) | Delete a file or empty directory | yes |
| [`save`](#save) | Create a file from a tag | yes |
| [`set`](#set) | Bind a value to a variable | yes |
| [`undo`](#undo) | Reverse the last line that changed something | yes |
| [`up`](#up) | Move up one directory | yes, where you are |
| [`vars`](#vars) | List the variables in scope | no |
| [`write`](#write) | Write text to a file | yes |

The column says whether the command produces events. A line made only of commands that
change nothing leaves no trace at all, which is why `undo` after `ls` reverses the line
before the `ls` rather than the `ls` itself. See
[How it works](concepts.md#events-and-what-a-line-is).

`help` and `clear` are handled by the page rather than by a command. See
[The web terminal](web-terminal.md#words-the-page-handles-itself).

---

## attr

Shows a file's attributes, or writes new ones.

```
attr <path> [name=value ...]
```

| Parameter | Required | Meaning |
| --- | --- | --- |
| `path` | yes, or piped | The file |
| `name=value` | no | Attributes to set, written with no spaces around the `=` |

With no assignments it lists every attribute the record carries:

```
$ attr readme.txt
created = 2026-09-21T09:00:00.0000000+00:00
folder = /
kind = text
modified = 2026-09-21T09:00:00.0000000+00:00
name = readme.txt
```

With assignments it writes them and returns the file. The names are yours: a file can
carry any attribute you like, which is the point of an attribute filesystem.

```
$ attr readme.txt tag=work due=2026-10-01
readme.txt
```

`name` and `kind` may be set, and renaming to a name already used in the folder is an
error. `folder`, `created` and `modified` are the terminal's and are refused:

```
$ attr readme.txt modified=yesterday
'modified' is set by the terminal and cannot be written.
```

Assignments are data, not named parameters. A command that does not collect them says
so rather than ignoring them:

```
$ progress steps=20
'progress' does not take 'steps=' assignments.
```

Undo restores every attribute the record had.

---

## cat

Reads a file and returns its contents as text.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | piped. The file to read. |

**Returns** the file's text. The terminal renders it as a block rather than a chip, so
line breaks survive.

```
$ cat readme.txt
This filesystem lives in the browser tab.
$ echo notes.txt | cat
Try: ls, cd documents, mkdir scratch, echo "hello"
$ cat(path: readme.txt)
This filesystem lives in the browser tab.
```

**Errors**

| Message | Cause |
| --- | --- |
| `File does not exist : <path>` | Nothing at that path. |
| `That is a directory, not a file : <path>` | The path names a directory. |

---

## cd

Enters a directory and makes it current.

**Parameters**

| Name | Notes |
| --- | --- |
| `TargetPath` | piped. The directory to enter. |

**Returns** the new current directory as a path.

```
$ cd documents
documents
$ cd ../projects
projects
$ echo documents | cd
documents
```

`cd ..` normalises, so the working directory shows the parent's real name rather than a
path ending in `..`.

**Undo** returns to the directory you were in before.

**Errors**

| Message | Cause |
| --- | --- |
| `'cd' needs an argument for 'TargetPath'.` | No path given and nothing piped in. |
| `Directory does not exist : <path>` | No such directory. |

---

## cp

Copies a file into a directory. The copy keeps the source's file name.

**Parameters**

| Name | Notes |
| --- | --- |
| `sourcePathAndFile` | The file to copy. |
| `targetPath` | The directory to copy it into. |

**Returns** the new file's path.

```
$ cp readme.txt documents
readme.txt
```

**Undo** deletes the copy.

**Errors**

| Message | Cause |
| --- | --- |
| `File does not exist : <path>` | No such source file. |
| `Target directory does not exist : <path>` | No such destination directory. |
| `Target file already exists : <path>` | Refuses to overwrite; use `write` or `rm` first. |

---

## download

Downloads a file into a directory, writing progress while it runs. This is an
asynchronous command: it can be stopped, and it reports what it wrote.

**Parameters**

| Name | Notes |
| --- | --- |
| `url` | optional. Defaults to this project's README on raw.githubusercontent.com. |
| `into` | optional. Defaults to the current directory. |

**Returns** the downloaded file's path.

```
$ download
100%
=====...=====>
Downloaded to /home/terminal/README.md
README.md
```

While it runs it writes three lines: a percentage, a bar that grows to a hundred
characters, and a transfer rate in MB/s. The bar above is shortened to fit this page.

**Undo** deletes the downloaded file.

**Errors**

| Message | Cause |
| --- | --- |
| `Not a valid URL : <text>` | The URL is not absolute. |
| `Target directory does not exist : <path>` | No such destination directory. |
| `The server did not report a content length.` | The response had no `Content-Length`; progress cannot be computed. |
| `The transfer ended before all bytes arrived.` | The connection closed early. |
| `Download failed : <reason>` | Written as output when the transfer throws. |

In a browser, a download is subject to the origin's CORS policy. Hosts that do not send
permissive headers fail regardless of the URL being valid. See
[Troubleshooting](troubleshooting.md#download-fails-in-the-browser-but-the-url-works-elsewhere).

---

## echo

Returns its argument unchanged, or whatever was piped in. Useful for turning a word
into a value, and for seeing what a pipe is carrying.

**Parameters**

| Name | Notes |
| --- | --- |
| `text` | piped. The value to return. |

```
$ echo hello
hello
$ echo 42
42
$ echo "hello world"
hello world
$ ls | echo
up  documents  projects  readme.txt
```

The value keeps its type: `echo 42` returns a number, and `ls | echo` returns the list
of paths rather than a printed copy of it.

**Errors**

| Message | Cause |
| --- | --- |
| `'echo' needs an argument for 'text'.` | Nothing written and nothing piped in. |

---

## exit

Asks the host application to shut down.

**Parameters** none.

In the browser there is nothing to close, so the command succeeds and returns nothing.
In the desktop shell it closes the window.

---

## history

The lines that changed something, oldest first.

```
history
```

```
$ history
1  09:00:00  seed
2  09:01:12  mkdir alpha
3  09:01:20  write note.txt hello
```

Each line shows its sequence number, the time, and the line exactly as it was typed. A
line whose effect has been reversed is marked:

```
$ undo
Undone: write note.txt hello
$ history
1  09:00:00  seed
2  09:01:12  mkdir alpha
3  09:01:20  write note.txt hello  (undone)
4  09:01:31  write note.txt hello
```

The fourth entry is the undo itself, which is a line in its own right. Lines that
changed nothing, such as `ls`, never appear, because they were never recorded.

`seed` is the filesystem the session started with. It is shown, and it cannot be undone.

---

## ls

Lists a directory: the parent entry first, then directories, then files.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | optional. Defaults to the current directory. |

**Returns** a list of paths. Each one is a chip you can tap to insert its name.

```
$ ls
up  documents  projects  readme.txt
$ ls documents
up  notes.txt
$ ls -path documents
up  notes.txt
```

The `up` entry is omitted when the current directory is the root.

**Errors**

| Message | Cause |
| --- | --- |
| `Directory does not exist : <path>` | No such directory. |

---

## mkdir

Creates a directory.

**Parameters**

| Name | Notes |
| --- | --- |
| `FolderName` | The directory to create, relative to the current one. |

**Returns** the new directory's path.

```
$ mkdir scratch
scratch
```

**Undo** deletes it.

**Errors**

| Message | Cause |
| --- | --- |
| `Target directory already exists : <path>` | Something is already there. |

---

## progress

Runs a progress bar. It exists to exercise long-running commands: live output,
cancellation and undo. It changes nothing.

**Parameters**

| Name | Notes |
| --- | --- |
| `steps` | optional. How many steps to take. Default 100. |
| `delay` | optional. Milliseconds between steps. Default 100. |

**Returns** the percentage reached, as a number.

```
$ progress 4 5
100%
=========================>
Progress test finished
100
$ progress -steps 2 -delay 1
100%
=========================>
Progress test finished
100
```

Press **Stop** while it runs and it reports where it stopped:

```
$ progress
9%
====>
Cancelled at 9%
Stopped.
```

**Undo** writes `Progress test undone`.

**Errors**

| Message | Cause |
| --- | --- |
| `'steps' must be at least 1.` | `steps` was zero or negative. |
| `'steps' must be a whole number, not '<value>'.` | The argument was not a number, for example a bare `-steps` flag. |

---

## pwd

Returns the current directory.

**Parameters** none.

```
$ pwd
terminal
```

The line above the input shows the same thing as a full path, at all times.

---

## redo

Puts back what `undo` took away, and names the line it restored.

```
redo
```

```
$ mkdir alpha
alpha
$ undo
Undone: mkdir alpha
$ redo
Redone: mkdir alpha
```

With nothing undone it says so, and that is not an error:

```
$ redo
Nothing to redo.
```

Redo is undo applied to an undo, so `undo`, `redo`, `undo` leaves you where the first
`undo` did.

---

## reset

Empties the log and starts again from the seeded filesystem.

```
reset
```

```
$ reset
Reset. 4 files restored.
```

This is the only command that cannot be undone. The lines it would have been undone
from are the ones it threw away:

```
$ reset
Reset. 4 files restored.
$ undo
Nothing to undo.
```

It is the way out of a log that cannot be read back, and the way to start a session
over. Because it cannot be reversed, it is deliberately not one of the suggestion keys
under the input: you have to type it.

---

## rm

Deletes a file, or a directory that is empty.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | piped. What to delete. |

**Returns** text naming what it removed.

```
$ rm note.txt
Removed note.txt
```

A directory with anything in it is refused: undoing that would mean restoring a whole
tree, and a one-word recursive delete is not a safe thing to hand a phone. Empty it
first.

**Undo** puts the file back with its contents, or recreates the directory.

**Errors**

| Message | Cause |
| --- | --- |
| `Nothing exists at : <path>` | No such file or directory. |
| `Directory is not empty : <path>` | Delete the contents first. |
| `Cannot delete the current directory.` | Move out of it first. |

---

## save

Creates a file from a tag. The tag's type becomes the file's `kind`, its `name`
attribute becomes the name, and everything else is carried across.

```
save <tag/>
```

```
$ save <note name=todo due=2026-10-01/>
todo
$ attr todo
created = 2026-09-21T09:00:00.0000000+00:00
due = 2026-10-01
folder = /
kind = note
modified = 2026-09-21T09:00:00.0000000+00:00
name = todo
```

A tag with attributes is exactly what a file record is, which is why this is one word
rather than a `write` followed by several `attr` calls.

The tag can be piped in: `echo <note name=todo/> | save`.

The `name` attribute is required, and a name already used in the folder is an error.
The file has no content, so `cat` on it returns empty text; `write` gives it some.

---

## set

Binds a value to a variable. This is how a command's result gets a name; tags bind
variables too, but only for objects and components.

**Parameters**

| Name | Notes |
| --- | --- |
| `name` | The variable's name, without the `$`. |
| `value` | piped. The value to bind. |

**Returns** the value, so `set` can sit in the middle of a pipeline.

```
$ set greeting hello
hello
$ ls | set files
up  documents  projects  readme.txt
$ echo $greeting
hello
```

**Undo** restores the previous binding, or unbinds a new name.

**Errors**

| Message | Cause |
| --- | --- |
| `'set' needs an argument for 'value'.` | No value written and nothing piped in. |
| `'<name>' is not a valid variable name.` | Names take letters, digits and underscore. |
| `Unknown variable : $<name>` | You wrote `set $x 1`; `$x` reads the variable rather than naming it. |

---

## undo

Reverses the last line that changed something, and names it.

```
undo
```

```
$ write note.txt second
note.txt
$ undo
Undone: write note.txt second
$ cat note.txt
first
```

It works on lines, not on commands. A line that changed nothing is not in the way:

```
$ mkdir alpha
alpha
$ ls
alpha documents projects readme.txt
$ undo
Undone: mkdir alpha
```

With nothing to undo it says so, and that is not an error:

```
$ undo
Nothing to undo.
```

Undoing does not erase history: it appends the reverse, so `redo` can reverse it in
turn and `history` shows both.

---

## up

Moves to the parent directory.

**Parameters** none.

**Returns** the new current directory.

```
$ up
terminal
```

**Undo** returns to the directory you were in before.

---

## vars

Lists every variable in scope, one per line, and returns their values as a list.

**Parameters** none.

```
$ vars
$files = up documents\ projects\ readme.txt
$greeting = hello
```

With nothing bound it writes `No variables. Try: set greeting hello` and returns
nothing.

---

## write

Writes text to a file, creating it or replacing its contents.

**Parameters**

| Name | Notes |
| --- | --- |
| `path` | The file to write. |
| `text` | piped. What to write. |

**Returns** the file's path.

```
$ write note.txt hello
note.txt
$ echo hi | write note.txt
note.txt
$ cat notes.txt | write copy.txt
copy.txt
$ write(note.txt, hello)
note.txt
```

**Undo** restores the previous contents, or deletes the file if it did not exist.

**Errors**

| Message | Cause |
| --- | --- |
| `That is a directory, not a file : <path>` | The path names a directory. |
| `Directory does not exist : <path>` | The parent directory is missing; create it first. |
| `'write' needs an argument for 'text'.` | No text written and nothing piped in. |

---

## Commands only in the desktop shell

None at present. `debug`, which wrote the entity and component tree to a file, was
dropped when the command layer moved to F#: it reaches into the entity component
system, which the core knows nothing about. It can come back as a host-supplied
command if it is wanted.
