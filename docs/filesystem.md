# The filesystem

There are no directories here, in the sense you are used to. There are records, every
record is a set of attributes, and `folder` is one of those attributes. A directory is
a record that says `kind = folder`, and being "in" one is a question the terminal keeps
asking: *which records say their folder is this one?*

Once being somewhere is a question, any question will do. `cd $row.mood eq great` is a
place in exactly the way `/journal` is a place, and `ls` answers it the same way.

This page is about that model: what a record is, which attributes the terminal owns,
and what you can do once a query is somewhere you can stand.
[Tables and predicates](tables.md) is the guide to the questions themselves;
[Command reference](commands.md) is the per-command detail.

## A file is a set of attributes

Every file is a record. It has attributes, and optionally some content. The examples on
this page are one session, started in a fresh tab:

```
$ mkdir journal
journal

$ cd journal
journal

$ save <note name=monday mood=good tag=work/>
monday

$ attr monday
name      value
created   2026-09-22T09:30:00.0000000+00:00
folder    /journal
kind      note
modified  2026-09-22T09:30:00.0000000+00:00
mood      good
name      monday
tag       work
```

`mood` and `tag` are not special. Nothing declared them, no schema knows about them,
and no command was taught them: a tag's attributes *are* a file's attributes, so
writing one down and saving it is the whole of creating a file with metadata. `attr`
adds them to a file that already exists:

```
$ attr /readme.txt tag=work
readme.txt
```

A listing shows every attribute anything in it carries, so a new attribute becomes a
new column the moment something has one:

```
$ save <note name=tuesday mood=better tag=work/>
tuesday

$ save <note name=saturday mood=great tag=home/>
saturday

$ ls
name      kind  folder    size  modified                           mood    tag
monday    note  /journal  0     2026-09-22T09:30:00.0000000+00:00  good    work
saturday  note  /journal  0     2026-09-22T09:30:00.0000000+00:00  great   home
tuesday   note  /journal  0     2026-09-22T09:30:00.0000000+00:00  better  work
```

A record that does not carry one has a gap in that column rather than an empty string,
and a gap is not equal to anything, including zero and the empty string.

## The attributes the terminal owns

Five attributes are the runtime's. You can read all of them; you may write two.

| Attribute | Written by | Meaning |
| --- | --- | --- |
| `name` | you | Unique within its folder. `attr x name=y` renames. |
| `kind` | inferred, or you | `folder`, `text`, `xml`, `csv`, `json`, `markdown`, `script`, `view`, or whatever a saved tag's type was. Guessed from the extension when nothing says. |
| `folder` | the terminal | The full path of the containing directory, `/` for the root. |
| `created` | the terminal | When the record was made. |
| `modified` | the terminal | When its content or attributes last changed. |

Writing one of the last three is refused:

```
$ attr /readme.txt folder=/elsewhere
'folder' is set by the terminal and cannot be written.
```

`size` is not an attribute at all. It is the length of the content, worked out when a
listing is built, so it can never disagree with what `cat` shows. Moving a file is
therefore not a copy and a delete: it is one attribute changing.

## Directories are records too

A directory is a record whose `kind` is `folder` and which has no content. That is the
whole of it — there is no second kind of thing in the store.

```
$ cd /
/

$ ls | where $row.kind eq folder | count
4
```

The root is the exception. `/` is implicit: nothing records it, because a record for it
would need a `folder` attribute of its own and there is nothing above it. That is why
`cd /` answers a path where `cd journal` answers the directory's record.

Names are unique within a directory, across files and directories alike, so `mkdir
readme.txt` beside the file of that name is refused with
`Target directory already exists : /readme.txt`.

## Views

A predicate over attributes is a place. With one more note saved at the root:

```
$ save <note name=postcard mood=great tag=home/>
postcard

$ cd $row.mood eq great
$row.mood eq great

$ pwd
$row.mood eq great

$ ls
name      kind  folder    size  modified                           mood   tag
postcard  note  /         0     2026-09-22T09:30:00.0000000+00:00  great  home
saturday  note  /journal  0     2026-09-22T09:30:00.0000000+00:00  great  home
```

Three things are worth noticing.

**A view is not scoped to a directory.** `postcard` is in `/` and `saturday` is in
`/journal`, and both are here, because the question was about moods and not about
directories. The `folder` column is what says where each row came from; tapping one in
the browser writes the `cd` that goes there.

**A view does not replace the directory you are in.** It is a way of looking, not a
place to put things, so a file created while a view is set still lands in the directory
underneath it:

```
$ mkdir keepsakes
keepsakes

$ up
/

$ ls | where $row.name eq keepsakes | select name folder
name       folder
keepsakes  /
```

**`up` comes back out of one thing at a time.** In a view it puts the view down and
leaves you in the directory you were already in; with no view it moves to the parent.
Two `up`s from a view over a subdirectory come out in the order they went in.

What decides whether `cd` takes a name or a question is whether an operator was
written. `cd journal` is a name. `cd $row.mood eq great` has `eq` in it, so it is a
question. Nothing else distinguishes them, and there is no separate command.

## Asking once: `find`

`find` is `ls` over the whole terminal with a predicate written on the spot, and it
does not move you:

```
$ find $row.kind eq note and $row.tag eq work | count
2
```

Use `find` for a question you are asking once and `cd` for one you want every `ls` to
keep asking. Neither changes anything, so neither leaves a transaction and `undo`
reaches past both.

## Keeping a view: `save-view`

A question worth asking twice is worth keeping, and the way to keep one is to make it
a file:

```
$ save-view cheerful $row.mood eq great
cheerful

$ cat cheerful
$row.mood eq great

$ cd cheerful
$row.mood eq great
```

A view is a record of kind `view` whose content is the predicate as it was written.
Nothing else about it is special: it appears in `ls`, `cat` shows what it asks, `attr`
renames it, `rm` deletes it and `undo` brings it back. The view is created in the
current directory, which has nothing to do with what it matches. Inside the view, the view
file itself is not one of the rows, because its `mood` is not `great`; out of it, the file
is in the directory like anything else:

```
$ ls | where $row.kind eq view | select name
name

$ up
/

$ ls | where $row.kind eq view | select name
name
cheerful
```

## Live listings

In the browser, the newest listing keeps itself up to date. A listing is a question
about the store, and the store changes underneath it: `mkdir x` in the next entry makes
the answer above it wrong.

Because every change is an entry in the log, the page knows the moment one lands, asks
the same question again, and replaces the table in place with a brief flash so you can
see that it moved. Nothing is re-run that could change anything — a refresh that named
a writing command is refused rather than run — so nothing appears in `history` and
`undo` is unaffected.

Only the newest listing is live; the ones above it froze when you moved on, which is
what a scrollback is for, and say `frozen`. The badge underneath the live one says
`live`, and tapping it stops the refreshing for that listing.

## Paths

Paths are still text, and still work the way you expect: absolute from `/`, or relative
to the current directory, with `.` and `..`. A path is resolved against
`folder` attributes, which is why `cd ..` shows the parent's real name rather than a
path ending in `..`.

```
$ cd /journal
journal
$ cd ..
/
```

## Where this came from

The model is [decision 0013](decisions/0013-attribute-filesystem.md), which follows the
Be File System: files carry typed attributes, and a query over them is a first-class
object that behaves like a folder. The hierarchy was kept as one ordinary attribute
rather than removed, so that trees remain expressible and an imported file has
somewhere to record where it came from.

Records live in an event log, not in a tree that commands edit
([decision 0010](decisions/0010-undo-by-event-sourcing.md)). The filesystem you see is
a fold over that log, which is why `undo` restores a deleted record with its attributes
intact and why a reload in the browser puts you back in the view you were in.
[How it works](concepts.md) follows one line from Enter to the answer.
