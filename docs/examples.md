# Worked examples

Complete sessions you can type line by line. Output is what the terminal shows; result
chips are written as plain words.

Each example starts from a fresh tab, except that examples 1, 2 and 8 run in sequence:
the second uses the directory the first made, and the last clears up after both.

## 1. Edit a file and take it back

```
$ ls
up  documents  projects  readme.txt

$ mkdir scratch
scratch

$ write scratch/plan.txt "first draft"
plan.txt

$ cat scratch/plan.txt
first draft

$ write scratch/plan.txt "second draft"
plan.txt

$ cat scratch/plan.txt
second draft

$ undo
Undone: cat

$ undo
Undone: write

$ cat scratch/plan.txt
first draft
```

The first `undo` reversed the `cat`, which had nothing to reverse. That is why it names
what it undid. The second reversed the overwrite and restored the previous contents.

## 2. Copy a file through a pipe

```
$ cat documents/notes.txt
Try: ls, cd documents, mkdir scratch, echo "hello"

$ cat documents/notes.txt | write scratch/notes-copy.txt
notes-copy.txt

$ ls scratch
up  notes-copy.txt  plan.txt
```

`cat` returned text, and `write` used it for the parameter you did not write out. The
text never became a command-line string in between, so quoting could not go wrong.

## 3. Name results and reuse them

```
$ ls | set files
up  documents  projects  readme.txt

$ set target documents
documents

$ ls $target
up  notes.txt

$ vars
$files = up documents\ projects\ readme.txt
$target = documents

$ undo
Undone: ls

$ undo
Undone: set

$ vars
$files = up documents\ projects\ readme.txt
```

`$files` holds the list itself. Undo unwound the `set target` binding and left the
earlier one alone.

## 4. Structured values with tags

```
$ <size|measurement unit=metres value=3/>
<measurement unit=metres value=3/>

$ echo $size
<measurement unit=metres value=3/>

$ <$size>
<measurement unit=metres value=3/>

$ {renderer colour=red/}
{renderer colour=red/}

$ <outer><inner depth=2/></outer>
<outer><inner depth=2/></outer>
```

Angle brackets build an object, braces build a component, and each reads back the way
you wrote it. The `size|` prefix bound the value to a variable without becoming part of
it.

## 5. A long command, watched and stopped

```
$ progress 20 100
100%
=========================>
Progress test finished
100
```

The first two lines are one line each, updated in place while it runs: the percentage
climbs and the bar grows. What you see afterwards is their final state.

Run it again and press Stop after a moment:

```
$ progress
9%
====>
Cancelled at 9%
Stopped.
```

Then try to start two at once. Submit `progress`, and while it runs submit `ls`:

```
$ ls
A command is already running. Stop it first.
```

## 6. Everything the parser can tell you

```
$ echo "hello world"
hello world
```

Tap the words `hello world` in the scrollback and the inspector names them a string.
Tap `echo` and it names it a command. The quotes are separate tokens, also strings. Now make a mistake on purpose:

```
$ <thing
Syntax error at column 6: expected identifier, />, >.

$ <a></b>
Closing tag 'b' does not match opening tag 'a'

$ echo --double
Syntax error at column 5: expected argument, end of input, ", "", """, $, <, <$, {, |.
```

The column is a zero-based offset into the line, and the list is what the grammar could
have accepted at that point.

## 7. Same call, three ways

```
$ write note.txt hello
note.txt

$ write(note.txt, hello)
note.txt

$ echo hello | write note.txt
note.txt
```

All three bind the same two parameters. The third one leaves `text` to the pipe.

## 8. Clean up

```
$ rm scratch/plan.txt
Removed plan.txt

$ rm scratch/notes-copy.txt
Removed notes-copy.txt

$ rm scratch
Removed scratch

$ undo
Undone: rm

$ ls
up  documents  projects  scratch  readme.txt
```

`rm` refuses a directory that still has anything in it, which is why the files went
first. Undo recreated the directory.
