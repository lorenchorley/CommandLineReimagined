# Command reference

Every command available in the browser terminal, in the order `help` lists them.

Each entry gives the parameters in declaration order, which is the order positional
arguments fill them. A parameter marked **piped** takes the piped value when you do not
write it out. A parameter marked **optional** may be omitted.

Paths are relative to the current directory unless they start with a separator or a
drive. `..` and `.` work.

## Summary

| Command | Purpose | Undo |
| --- | --- | --- |
| [`cat`](#cat) | Read a file as text | nothing to reverse |
| [`cd`](#cd) | Enter a directory | returns to the previous directory |
| [`cp`](#cp) | Copy a file into a directory | deletes the copy |
| [`download`](#download) | Download a file, with progress | deletes the file |
| [`echo`](#echo) | Return an argument or the piped value | nothing to reverse |
| [`exit`](#exit) | Ask the host to close | nothing to reverse |
| [`ls`](#ls) | List a directory | nothing to reverse |
| [`mkdir`](#mkdir) | Create a directory | deletes it |
| [`progress`](#progress) | Run a progress bar, to exercise long commands | writes a note |
| [`pwd`](#pwd) | The current directory | nothing to reverse |
| [`rm`](#rm) | Delete a file or empty directory | puts it back |
| [`set`](#set) | Bind a value to a variable | restores the previous binding |
| [`up`](#up) | Move up one directory | returns to the previous directory |
| [`vars`](#vars) | List the variables in scope | nothing to reverse |
| [`write`](#write) | Write text to a file | restores the previous contents |

`help`, `clear` and `undo` are handled by the page rather than by a command. See
[The web terminal](web-terminal.md#words-the-page-handles-itself).

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

`debug` writes the entity and component tree to a file and can open it. It needs the
scene, so it is not registered in the browser.
