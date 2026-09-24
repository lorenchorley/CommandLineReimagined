# Troubleshooting

Symptoms that are not error messages, and what is behind them. For messages, see the
[error reference](errors.md).

## The page says `Loading the terminal…` and the input stays disabled

The .NET runtime has not finished loading. The banner, the `note` at the top of the
scrollback, is where the page says how its start is going; there is no status line. A
first visit fetches about 10 MB; later visits come from the browser cache. If the banner
says `failed to load` in red, the runtime did not start within about forty seconds, so
its files are missing or blocked: reload, and if it persists check that your host serves
`.wasm` files with the `application/wasm` content type.

If the banner reads `restoring…` for a moment and then `failed to restore`, the runtime
loaded but replaying the stored log failed. The scrollback says why. The input stays
disabled, because showing an empty filesystem that may not be empty would be worse.

## My files disappeared

They should not, so it is worth reading the banner at the top of the scrollback.

If it says **`not persisted`**, this browser is not keeping the log and the session
lasted only as long as the tab. A private or incognito window is the usual reason:
IndexedDB is unavailable there. Site-data settings that block storage for this origin
do the same. Hover the mark to see what the browser said.

If storage goes away while the page is open, the session carries on in memory, and the
banner says `not persisted` after the next line you run. What you do from then on
is not kept, and the next load shows the files as they were when storage went.

Otherwise, the filesystem is stored by your browser for this site, so anything that
clears site data for it takes the filesystem with it: clearing browsing data, a
"clear cookies and site data" setting, or some privacy extensions. There is no copy
anywhere else, because nothing is uploaded.

If a `note` in the scrollback says that stored lines could not be read and were
skipped, a log was written by a different build of the terminal than the one loading it. The unreadable
lines are skipped rather than stopping the page; `reset` starts over cleanly.

## `undo` said there was nothing to undo

Undo works on lines that changed something. A line that changed nothing, such as `ls`
or `pwd`, is not recorded, so it is never what undo reaches for — and the filesystem
you started with is recorded but is not yours to take back, so undo stops before it.
Moving is a change: where you are is kept in the log, so `undo` after `in documents`
takes you back.

If you have only run read-only lines in a fresh session, there is genuinely nothing to
undo, and that is what it says.

## `redo` will not go any further

Redo puts back what undo took away, and stops once everything is back. It does not
carry on and take it away again.

## I want to start over

```
$ reset
Reset. 17 files restored.
```

It empties the log and seeds the filesystem again. It cannot be undone, which is why it
is not one of the suggestion keys.

## `cd`, `up` or `cat` says `Unknown command`

They were renamed `in`, `out` and `read`
([decision 0037](decisions/0037-in-out-back-and-read.md)). The message is
`Unknown command : cd`, and the note under it, labelled `did you mean`, says which to
use and offers the line with it, `in documents` for `cd documents`, as a chip to tap.
Typing the old name offers the new one as a completion too. To go back to where you
were before a move, use `back`.

## The guide in my filesystem changed

A seeded file you have not changed follows the terminal's own copy, so when the guide or
the readme is revised, yours is brought up to date on the next load
([decision 0040](decisions/0040-seeded-files-follow-the-seed.md)). One you have written
to, renamed, moved or tagged is left as it is; `reset` gives you the current one.

## A listing stopped updating

Only the newest listing starts live; an older one pauses when a newer one arrives, and
its badge reads `paused`. Tap `paused` to make it live again. Any number can be live at
once.

## A line that failed shows a table under the error

That is the command's help, in a panel labelled `help`: the command was called wrongly,
with an argument missing, one too many or a flag it does not have, and the panel says
what it takes ([decision 0038](decisions/0038-a-wrong-call-shows-its-help.md)). The line
failed as it would have anyway.

A panel labelled `did you mean` is a note: the terminal's guess at what you meant, a
command, a file, a folder, a variable or a question, with a chip for each corrected
line. Neither panel is part of the failure, and a script never sees them.

## I tapped the chip under an error and it did not run my line

It is not meant to. A chip in a `did you mean` or `why` panel is a fix: it puts the
whole corrected line in the input, in place of what was there, with the caret at the
end, and stops there ([decision 0044](decisions/0044-a-fault-may-carry-fixes.md)). The
guess can be wrong, and running it for you could change your files without your having
asked. Read the line, change it if you need to, and press Run or Enter.

The tap leaves the keyboard as it was, as every button does, so on a phone with the
keyboard down the line is in the input and the keyboard stays down until you tap into
it. A mistake inside a script that `run` ran has no chip at all, since the line to
correct is in the script: open it with `read`, and `write` it corrected.

## My filter answered an empty table

Look under the table for a panel labelled `why`. When `where`, `find` or a view keeps no
row of a table that had some, it says why
([decision 0043](decisions/0043-an-empty-filter-explains-itself.md)):

- `No row has knd; did you mean kind?`: the column is misspelt, or no row carries it.
  Columns are matched exactly, so `$row.Kind` is not `$row.kind`.
- `kind is folder or text`: the column is there, and none of its values is the one you
  compared with. The panel lists the values it does have, most frequent first. A value
  a slip from one of them comes with the corrected line as a chip.
- `size runs from 0 to 193`: a comparison of numbers that none of them passes.

With no panel, the question was answered and the answer really is none: every part of
it keeps a row on its own, as `$row.kind eq folder and $row.size gt 0` does at the root,
or it is an `or`, a `not`, `ne` or `has`, which are not explained. `ls | columns` lists
the columns, and `ls | distinct kind` the values of one. A table that was empty before
the filter needs no reason, and gets none.

## My XML is not a table

A document is a table only when its root's children all have one name and none has
children of its own. Anything nested deeper is a tree, and a table function says which
child broke the shape. `pick` reads a tree: it finds elements by a CSS selector and
answers them as a table, which `where`, `select` and `sort` take as they take any
other ([Reading a tree with `pick`](tables.md#reading-a-tree-with-pick)). From a fresh
tab:

```
$ write shop.xml "<shop><dept name='tools'><item sku='A1' price='4.5'>Hammer</item><item sku='A2' price='12'>Saw</item></dept><dept name='garden'><item sku='B1' price='3'>Trowel</item><dept name='seeds'><item sku='B7' price='1.2'>Basil</item></dept></dept></shop>"
shop.xml

$ from-xml shop.xml | where $row.price lt 5
<shop> is not a table: child 1 has children of its own.

$ from-xml shop.xml | pick item | where $row.price lt 5 | select sku text
sku  text
A1   Hammer
B1   Trowel
B7   Basil
```

For a document you have not seen, `pick "*" | group @tag` says which elements it holds
and how many of each:

```
$ from-xml shop.xml | pick "*" | group @tag
key   rows
shop  1 row
dept  3 rows
item  4 rows
```

If `pick` answers only the header `@tag  @children`, nothing matched:

- Names are compared exactly, so `Item` is not `item`.
- `>` means a direct child, and a space means anywhere inside: `shop > item` matches
  nothing here, since every item is inside a `dept`, and `shop item` matches all four.
- An attribute test compares text: `[price^=1]` is a price that starts with `1`, which
  here is `12` and `1.2`, not every price over 10. Compare numbers with `where`, after
  the `pick`.

A selector with a space, `>`, `[` or `,` in it has to be in double quotes, or the line
is split at the space or does not parse. `pick` itself refuses a selector outside the
subset it reads, and says where it stopped: see
[Selector errors](errors.md#selector-errors).

## A table cell says `2 children` instead of what is in it

A cell is one line, so a list in one says how many items it holds
([decision 0051](decisions/0051-a-list-in-a-table-cell-is-summarised.md)): `N children`
in the `@children` column `pick` answers, `N items` in any other, and nothing when the
list is empty. The value is still the whole list. Pick inside the rows to go further
down, or read the list off a row with `@children`:

```
$ from-xml shop.xml | pick dept | select name @children
name    @children
tools   2 children
garden  2 children
seeds   1 child

$ from-xml shop.xml | pick dept | where $row.name eq seeds | pick item | select text
text
Basil
```

## A file name with a space is split into two arguments

Quote it: `write "my notes.txt" hello`. Unquoted words end at a space. Tapping a chip or
a cell inserts the file's path for you, quoted when it needs to be.

## `in ..` shows an odd path

It should not. Paths are normalised, so `in ..` reports the parent's real name. If you
see a path containing `..`, the build predates that fix.

## Tab does nothing

Tab applies a completion, and there is nothing to apply when the word is already
complete or nothing matches. The completion row above the suggestions shows what is
available; when it is empty, Tab has nothing to do. Some places have nothing to pick
by design, such as the count after `ls | take `: there the detail line above the
location names what is wanted instead. Phone keyboards have no Tab key, so tapping a
chip is the intended path there.

## Tab put a different word in

After the first Tab has filled in all the chips have in common, each further Tab puts
the next chip in place, and Shift+Tab the one before. Escape puts back what you had
typed before the first Tab.

After a command that has what it needs, such as `ls `, the first chip is `|`
([decision 0039](decisions/0039-the-pipe-comes-first.md)), so the first Tab there puts
`|` in place. Tab again for the chip after it.

## `where` used to answer an empty table and now fails

`ls | where kind eq folder` and `ls | where $row.kind` were never questions about the
row, and answered an empty table whatever the rows held. They are faults now
([decision 0033](decisions/0033-a-predicate-is-a-question-about-the-row.md)), and the
note under the message says what to write, with the line as a chip:
[A predicate is a question about the row](tables.md#a-predicate-is-a-question-about-the-row).

## The Run button says Stop and will not run my line

A command is still running. Press Stop, Enter or Escape, and it stops at its next
checkpoint. Commands that do not check for cancellation between steps finish first.

## `download` fails in the browser but the URL works elsewhere

The page fetches over the network like any other browser request, so the remote host
must allow cross-origin reads. Hosts that do not send permissive CORS headers fail no
matter how valid the URL is. The default URL is chosen because it does allow them.

## `progress` finishes instantly

You gave it a small `steps` or `delay`. `progress 4 5` takes four steps of five
milliseconds. Plain `progress` is a hundred steps of a hundred milliseconds, about ten
seconds.

## `echo -5` says there is no argument named `5`

You are on a build from before the negative-number rule. A `-` in front of a digit now
starts a word, so `echo -5` writes minus five; a `-` in front of anything else is still
a flag.

## `< thing` is a syntax error

A `<` opens a tag only when a name, a `$` or a `/` comes straight after it, with no
space. Comparisons are words, such as `lt`, so a `<` means nothing else. Write
`<thing/>`.

## Something behaves differently from these docs

The examples in these pages are pasted from real output, but if you find a gap, the
[specification](spec/README.md) states the intended behaviour normatively and the
conformance section maps each requirement to the test that proves it.
