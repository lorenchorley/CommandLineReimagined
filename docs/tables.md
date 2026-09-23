# Tables and predicates

Every listing is a table. `ls` does not return a list of names, it returns rows with
columns, and a table is a value like any other: you can filter it, sort it, count it,
pick a column out of it, and pipe what is left into something else.

The examples below are pasted from a real session, not typed from memory. Timestamps
are whatever the clock said at the time. Each section says where it starts; a fresh tab
is one with only the seeded files in it.

## A listing is a table

From a fresh tab:

```
$ ls
name        kind    folder  size  modified
documents   folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples    folder  /       0     2026-09-22T09:30:00.0000000+00:00
guide       folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects    folder  /       0     2026-09-22T09:30:00.0000000+00:00
readme.txt  text    /       192   2026-09-22T09:30:00.0000000+00:00
```

Five columns are always there: `name`, `kind`, `folder`, `size` and `modified`. Any
other attribute anything in the folder carries becomes a column of its own, after those
five, ordered by name. A file that does not carry it has a gap in that column.

`size` is not stored anywhere. It is the length of the file's content, worked out when
the table is built, so it can never disagree with what `cat` shows you. `created` is on
every record and is not a column: it is rarely what a listing is for, and `attr` shows
it.

Ask what the columns are with `columns`:

```
$ ls | columns
name      type
name      file
kind      text
folder    text
size      number
modified  text
```

The type is not declared anywhere: it is read off the cells. A column is typed when
every cell in it that is not a gap agrees, and `mixed` when they disagree. That is what
decides whether `lt` compares numbers or words and whether `sort` orders numerically —
so a column of numbers behaves like numbers whether it came from a listing or out of an
XML file.

In the browser a table is drawn as a real table: tap a column header to re-sort what is
on screen, and tap a cell to insert it into the line you are typing. Wide tables scroll
sideways inside their own entry.

## Predicates

`where` keeps the rows a predicate is true for. A predicate names the row it is talking
about, explicitly, as `$row`, and reads a column off it with a full stop:

```
$ ls | where $row.kind eq folder
name       kind    folder  size  modified
documents  folder  /       0     2026-09-22T09:30:00.0000000+00:00
examples   folder  /       0     2026-09-22T09:30:00.0000000+00:00
guide      folder  /       0     2026-09-22T09:30:00.0000000+00:00
projects   folder  /       0     2026-09-22T09:30:00.0000000+00:00
```

There is no `==` and no `<`. Comparisons are words, because a phone keyboard has no
convenient place for symbols and because `<` already opens a tag:

| Operator | True when |
| --- | --- |
| `eq` | the two sides are equal |
| `ne` | they are not |
| `gt`, `ge` | the left is greater, or greater or equal |
| `lt`, `le` | the left is less, or less or equal |
| `like` | the right is part of the left, ignoring case — or matches it as a glob, if it contains `*` |
| `has` | the left is a list or a table containing the right, or text containing it |

Combine them with `and`, `or` and `not`. `and` binds tighter than `or`, so
`a or b and c` means `a or (b and c)`. `not` takes the whole comparison after it.

The same predicate is also somewhere you can be. `find` runs one over the whole
terminal, and `cd` on one moves into it, so every `ls` afterwards asks it again:
[The filesystem](filesystem.md#views).

The examples that follow are in a folder of stock items, made with `save`:

```
$ mkdir stock
stock

$ cd stock
stock

$ save <item name=bolts sku=A1 qty=120 min=50/>
bolts

$ save <item name=nuts sku=B2 qty=12 min=40/>
nuts

$ save <item name=washers sku=C3 qty=0 min=20/>
washers

$ ls | select name sku qty min
name     sku  qty  min
bolts    A1   120  50
nuts     B2   12   40
washers  C3   0    20
```

```
$ ls | where $row.qty lt $row.min | select name qty min
name     qty  min
nuts     12   40
washers  0    20

$ ls | where $row.qty lt 50 and $row.qty gt 0 | select name qty
name  qty
nuts  12

$ ls | where not $row.sku eq A1 | select name
name
nuts
washers

$ ls | where $row.name like *s | select name
name
bolts
nuts
washers
```

Both sides of a comparison can be columns, as `$row.qty lt $row.min` is: the question
"which items are below their own minimum" does not need a number written down anywhere.

### Numbers, words and gaps

Two values compare as numbers when both read as numbers — including text that happens
to read as one, which is how a column loaded from a document behaves. Everything else
compares as the text you see on the screen. So `9 lt 100` is true, and `"9" lt "100"`
is true as well.

A comparison that touches a gap is false. Not less than, not greater than, not equal:
a missing cell is not a small one. Two gaps are `eq`, and that is the only exception.

### A predicate is a question about the row

A predicate is asked once for every row, so it has to be a yes-or-no question about
that row ([decision 0033](decisions/0033-a-predicate-is-a-question-about-the-row.md)).
Two ways of writing one are not, and both fail with a message that says what to write
instead. From a fresh tab:

```
$ ls | where kind eq folder
kind eq folder never reads $row, so it is the same for every row. Did you mean $row.kind eq folder?

$ ls | where $row.kind
$row.kind is text (folder), not true or false. Compare it: $row.kind eq folder.
```

The first compares the word `kind` with the word `folder`, which is false whatever the
row is, and never looks at a row at all. It is caught before any row is tested, and
`find`, `cd` and `save-view` catch it the same way. The second asks whether a kind is
true, and a kind is text. It is caught on the first row whose answer is neither true
nor false, which here is `documents`, whose kind is `folder`. Both used to answer an
empty table in silence, which reads as a real answer; `else` and `try` recover from
them like any other fault.

A column that really holds true or false can be read on its own. `history`'s `undone`
is one:

```
$ mkdir a
a

$ undo
Undone: mkdir a

$ history | where $row.undone
seq  at        source   undone  compensates
2    09:30:00  mkdir a  true
```

A row that has no value in the column counts as false, so a sparse column skips the
rows without it rather than stopping the line. So does the word `false`, and the word
`true` counts as true ([decision 0034](decisions/0034-what-answers-a-predicate.md)),
because `attr` stores the word it was given. From a fresh tab:

```
$ mkdir chores
chores

$ cd chores
chores

$ save <task name=laundry/>
laundry

$ save <task name=dishes/>
dishes

$ attr laundry done=true
laundry

$ ls | where $row.done
name     kind  folder   size  modified                           done
laundry  task  /chores  0     2026-09-22T09:30:00.0000000+00:00  true

$ ls | where not $row.done
name    kind  folder   size  modified                           done
dishes  task  /chores  0     2026-09-22T09:30:00.0000000+00:00
```

Each side of an `and` or an `or`, and what follows a `not`, is held to the same rule,
and the fault names the part that was not a question. Back in a fresh tab, at the root:

```
$ ls | where not $row.kind
$row.kind is text (folder), not true or false. Compare it: $row.kind eq folder.

$ ls | where $row.kind eq text or $row.kind
$row.kind is text (folder), not true or false. Compare it: $row.kind eq folder.
```

A part that is never read is never checked: in
`ls | where $row.kind eq nothing and $row.kind`, the left side is false for every row,
so the right side is not looked at and the answer is an empty table.

### The reserved words

These thirteen words are operators wherever they appear, and never arguments:

```
and  or  not  eq  ne  gt  ge  lt  le  like  has  else  try
```

`else` and `try` belong to [error recovery](language.md#errors-as-values). Writing one
of them as an ordinary word is a syntax error that says so:

```
$ echo eq
Column 5: 'eq' is an operator; write "eq" to pass it as text

$ echo "eq"
eq
```

The match is exact: `equals`, `eq.txt` and `Eq` are ordinary words, and only the bare
word itself is taken.

## The table functions

Each one takes its table from the pipe. Each one returns a table unless the reference
below says otherwise, and none of them changes anything: a pipeline of these is a
question, and asking a question leaves nothing to undo.

| Command | Arguments | Answers |
| --- | --- | --- |
| `where` | a predicate | the rows it is true for |
| `select` | one or more column names | those columns, in that order |
| `sort` | a column, and `desc` to reverse it or `asc`, the default, to say so | the rows in order |
| `take`, `skip` | a count | the first rows, or the rest |
| `first`, `last` | — | one row as an object, or nothing |
| `count` | — | how many rows, as a number |
| `distinct` | a column, optionally | unique rows, or that column's unique values |
| `group` | a column | a table of `key` and `rows` |
| `columns` | — | a table of `name` and `type` |
| `rows` | — | a list of objects |
| `table` | — | the same table, coerced explicitly |

From a fresh tab again, where the five seeded entries are the whole of the root:

```
$ ls | select name size
name        size
documents   0
examples    0
guide       0
projects    0
readme.txt  192

$ ls | sort size desc | select name size
name        size
readme.txt  192
documents   0
examples    0
guide       0
projects    0

$ ls | take 2 | select name
name
documents
examples

$ ls | skip 3 | select name
name
projects
readme.txt

$ ls | where $row.kind eq folder | count
4

$ ls | distinct kind
kind
folder
text

$ ls | group kind
key     rows
folder  4 rows
text    1 row
```

`sort` is stable: rows that compare equal keep the order they arrived in, going up or
down, so `sort name | sort size desc` leaves the equal sizes in name order.

`group` puts a whole table in each `rows` cell. A cell is one line, so it says how many
rows it has; `rows` is how you look inside one.

### One row, and the rows on their own

`first` and `last` answer a single row as an object, which reads the way you would write
it as a tag:

```
$ ls | sort name desc | first
<row name=readme.txt kind=text folder=/ size=192 modified=2026-09-22T09:30:00.0000000+00:00/>
```

Nothing to answer is an answer: `first` on an empty table gives nothing rather than
failing. `rows` turns the whole table into a list of those objects:

```
$ ls | select name kind | rows
<row name=documents kind=folder/> <row name=examples kind=folder/> <row name=guide kind=folder/> <row name=projects kind=folder/> <row name=readme.txt kind=text/>
```

## Tags are tables

A tag whose children all have the same type, and none of which has children of its own,
*is* a table: the children are the rows and their attributes are the columns. Nothing
has to be converted; any table function reads one as a table.

```
$ <items><item sku=A1 name=bolts qty=120/><item sku=B2 name=nuts qty=12/><item sku=C3 qty=0/></items> | table
sku  name   qty
A1   bolts  120
B2   nuts   12
C3          0
```

The columns are the union of what the children carry, in the order they first appear,
and a child that is missing one has a gap there — `C3` above has no name.

A tag that is not that shape stays a tree, and says which child broke the shape:

```
$ <items><item sku=A1/><other sku=B2/></items> | count
<items> is not a table: child 2 is <other> where the first is <item>.
```

`table` is the explicit form, for seeing what a tag reads as. Everywhere else the
coercion is implicit, so `where`, `count` and the rest take a tag directly.

## Reading and writing files

A table can live in a file, as XML or as CSV, and comes back out as the same value it
went in as. Four commands do it: `to-xml` and `to-csv` write, `from-xml` and `from-csv`
read. A document is an ordinary file in the terminal: it shows up in `ls`, `cat` reads
it as text, and `undo` takes a write away like any other.

From a fresh tab, and continuing in the same one to the end of this section:

```
$ mkdir stock
stock
$ cd stock
stock
$ <items><item sku=A1 name=bolts qty=120 min=50/><item sku=B2 name=nuts qty=12 min=40/><item sku=C3 name=washers qty=0 min=20/></items> | to-xml items.xml
items.xml
$ cat items.xml
<items>
  <item sku="A1" name="bolts" qty="120" min="50"/>
  <item sku="B2" name="nuts" qty="12" min="40"/>
  <item sku="C3" name="washers" qty="0" min="20"/>
</items>
```

`from-xml` reads a document into the same tree the tag notation makes, so a
table-shaped document is a table wherever one is expected, exactly as a tag is.
Attributes are typed by the rule a bare word in a tag gets: one that reads as a number
is a number. That is what makes `qty` and `min` number columns, and `lt` and `sort`
numeric on a file that only ever held characters:

```
$ from-xml items.xml | columns
name  type
sku   text
name  text
qty   number
min   number
```

`to-csv` writes a table with a header row. `from-csv` reads one back, and a column is
a number column only when every cell in it reads as a number:

```
$ from-xml items.xml | where $row.qty lt $row.min | sort qty | select sku qty | to-csv reorder.csv
reorder.csv
$ cat reorder.csv
sku,qty
C3,0
B2,12

$ from-csv reorder.csv | sort qty desc
sku  qty
B2   12
C3   0
```

Every line of a CSV ends in a line break, the last one included. A field is quoted only
when it has to be: when it holds the delimiter, a quote or a line break, or when it is
empty text, since an empty field is a gap and `""` is text that happens to be empty. A
gap is written as an empty field and reads back as a gap. `-delimiter` chooses another
separator, for reading and for writing; `tab` names a tab.

### What to-xml writes

A tag is written as itself. A table is written as a root element called `table` with
a `row` element per row; `-root` and `-row` name them instead. A list of tags is
written under a root called `list`, each item keeping its own name. `-declaration`
begins the file with an XML declaration:

```
$ from-xml items.xml | select sku qty | to-xml short.xml -root stock -row line -declaration
short.xml
$ cat short.xml
<?xml version="1.0" encoding="UTF-8"?>
<stock>
  <line sku="A1" qty="120"/>
  <line sku="B2" qty="12"/>
  <line sku="C3" qty="0"/>
</stock>
```

Both writers set the kind of a file they create to `xml` or `csv`, whatever it is
called. Writing to a file that already exists replaces its content and keeps its kind,
the same as `write`.

### Text inside an element

The tag notation has no way to write text inside an element, so an element's text is
read as an attribute called `text`, and a `text` attribute is written back as the
element's content ([decision 0025](decisions/0025-xml-text-content.md)):

```
$ write memo.xml "<memo to='ann'>Back at ten.</memo>"
memo.xml
$ from-xml memo.xml
<memo to=ann text=Back at ten./>
$ from-xml memo.xml | to-xml memo-copy.xml
memo-copy.xml
$ cat memo-copy.xml
<memo to="ann">Back at ten.</memo>
```

That round trip is exact for elements with only attributes, only text, or both. It is
not for *mixed* content, where text and child elements interleave: the text is
gathered, trimmed, into one attribute and written back before the children. Comments
and processing instructions are dropped, a prefixed name such as `x:item` is kept as
written, and a document with a DTD is refused.

## The other tables

`ls` is not the only command that answers one.

- `vars` is a table of `name` and `value`.
- `attr <path>` with no assignments is a table of `name` and `value`, including
  `created`.
- `history` is a table of `seq`, `at`, `source` and `undone`, so
  `history | where $row.undone eq true | count` is an ordinary question.
- `help` is a table of `name`, `parameters` and `description`:

```
$ help | where $row.name like sort
name  parameters               description
sort  <column> [desc] [table]  Order the rows by a column
```

- `help <command>` writes what the command does and answers a table of its
  parameters, with `name`, `required`, `piped`, `takes` and `description`:

```
$ help sort
Order the rows by a column
name    required  piped  takes        description
column  true      false  a column     The column to order by
desc    false     false  desc or asc  Write 'desc' to order downwards
table   false     true   a value      The table to work on; taken from the pipe when it is not written
```

- `find <predicate>` is a listing of everything in the terminal the predicate is true
  of, wherever it lives, with the same columns `ls` gives.

## When it goes wrong

| Message | What happened |
| --- | --- |
| `'count' needs a table, not text.` | Something that is not a table, and cannot be read as one, reached a table function. |
| `'select' has no column named 'nowhere'.` | A column name that is not in the table. `columns` lists what is. |
| `'select' needs at least one column.` | `select` with nothing to select. The pipe is the table, not the column list. |
| `'sort' takes 'desc' or 'asc' for 'desc', not 'up'.` | A word after the column that is neither direction. |
| `<items> is not a table: child 2 is <other> where the first is <item>.` | A tag whose children disagree about their type, or one that has children of its own. |
| `Column 5: 'eq' is an operator; write "eq" to pass it as text` | A reserved word in argument position. The column is where the word starts. |
| `'echo' takes a value for 'text', not an expression.` | A comparison was written for a command that does not take a predicate. |
| `kind eq folder never reads $row, so it is the same for every row. Did you mean $row.kind eq folder?` | A predicate that compares two fixed words. Name the column with `$row.`. |
| `$row.kind is text (folder), not true or false. Compare it: $row.kind eq folder.` | A predicate that is a value rather than a question. Compare it with something. |
| `$row is the row a predicate is testing. It exists only inside where, find, cd and save-view: ls \| where $row.kind eq folder.` | `$row` read outside a predicate, where no row is being tested. |
| `Column 16: a column name belongs after the stop, as in $row.kind` | `$row.` with no column after the full stop. |
| `Column 23: eq needs a value to compare with, such as folder` | A comparison with nothing on its right. |
| `Not well-formed XML : /stock/broken.xml line 1, position 16` | `from-xml` was given a file that is not XML. The line and position are where the parser gave up. |
| `'to-xml' needs a tag, a table or a list of tags, not text.` | Only something with elements in it can be written as a document. |
| `'first name' is not a name XML allows.` | A column name that XML cannot hold, often from a CSV header with a space in it. |
| `/stock/r.csv line 3 has 1 fields where the header has 2.` | A CSV record of a different width from its header. A table has one width. |
