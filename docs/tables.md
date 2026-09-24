# Tables and predicates

Every listing is a table. `ls` does not return a list of names, it returns rows with
columns, and a table is a value like any other: you can filter it, sort it, count it,
keep only the columns you want, and pipe what is left into something else. A tag is a
table when it has that shape, and `pick` makes one out of any tree.

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
readme.txt  text    /       193   2026-09-22T09:30:00.0000000+00:00
```

Five columns are always there: `name`, `kind`, `folder`, `size` and `modified`. Any
other attribute anything in the folder carries becomes a column of its own, after those
five, ordered by name. A file that does not carry it has a gap in that column.

`size` is not stored anywhere. It is the length of the file's content, worked out when
the table is built, so it can never disagree with what `read` shows you. `created` is on
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
sideways inside their own entry. A listing, from `ls` or `find`, keeps itself up to
date while its badge reads `live`; the newest starts live, and tapping `paused` on an
older one makes it live again ([Live listings](web-terminal.md#live-listings)).

Once a stage has what it needs, completion offers `|` first, and taking it turns the
chips to the commands that take a table: `ls ` offers `|` before the folders, and
`ls | where $row.kind eq folder ` offers `|` before `and` and `or`
([decision 0039](decisions/0039-the-pipe-comes-first.md)).

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
terminal, and `in` on one moves into it, so every `ls` afterwards asks it again:
[The filesystem](filesystem.md#views).

The examples that follow are in a folder of stock items, made with `save`:

```
$ mkdir stock
stock

$ in stock
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
Two ways of writing one are not, and both fail, with a note under the message that
says what to write instead. The indented lines are the note: its text, and the line it
offers as a fix, which on the page is a chip that puts the line in the input without
running it ([Notes beside a message](errors.md#notes-beside-a-message)). From a fresh
tab:

```
$ ls | where kind eq folder
kind eq folder never reads $row, so it is the same for every row.
  suggestion: Did you mean $row.kind eq folder?
  fix: ls | where $row.kind eq folder

$ ls | where $row.kind
$row.kind is text (folder), not true or false.
  suggestion: Did you mean $row.kind eq folder?
  fix: ls | where $row.kind eq folder
```

The first compares the word `kind` with the word `folder`, which is false whatever the
row is, and never looks at a row at all. It is caught before any row is tested, and
`find`, `in` and `save-view` catch it the same way. The second asks whether a kind is
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

$ in chores
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
$row.kind is text (folder), not true or false.
  suggestion: Did you mean $row.kind eq folder?
  fix: ls | where not $row.kind eq folder

$ ls | where $row.kind eq text or $row.kind
$row.kind is text (folder), not true or false.
  suggestion: Did you mean $row.kind eq folder?
```

The second offers no fix: `$row.kind` is written twice in the line, and the note cannot
tell which of the two to change.

A part that is never read is never checked: in
`ls | where $row.kind eq nothing and $row.kind`, the left side is false for every row,
so the right side is not looked at and the answer is an empty table, explained as the
next section describes.

### An empty answer says why

An empty table is a real answer, and it is also what a misspelt column or value gives,
so when `where`, `find` or a view keeps no row of a table that had some, the terminal
says why beside it ([decision 0043](decisions/0043-an-empty-filter-explains-itself.md)).
The answer is still the empty table; the explanation is a note, labelled `why` on the
page. Still in the same tab:

```
$ ls | where $row.knd eq folder
name  kind  folder  size  modified
  explanation: No row has knd; did you mean kind?
  fix: ls | where $row.kind eq folder

$ ls | where $row.kind eq foldr
name  kind  folder  size  modified
  explanation: kind is folder or text
  fix: ls | where $row.kind eq folder

$ ls | where $row.kind eq view
name  kind  folder  size  modified
  explanation: kind is folder or text

$ ls | where $row.size gt 1000
name  kind  folder  size  modified
  explanation: size runs from 0 to 193
```

There is at most one explanation, and it is the first of these that applies:

- **A column no row has.** A column the predicate reads through `$row.` that no row
  carries, with the nearest columns some row does carry, and a fix for the first. With
  nothing near, it says only `No row has knd.` A column only some rows carry is not one
  of these: a gap is an ordinary cell, and a filter over mixed records reads one.
- **The values a column has.** For `eq` or `like` of a column with a value no row has,
  the values the column does have, most frequent first, at most five, and then how many
  more there are, as in `name is documents, examples, guide, projects or readme.txt`.
  When the value compared is a slip or two from one of them, the explanation offers
  that one as a fix ([decision 0045](decisions/0045-a-near-value-offers-a-fix.md)):
  `foldr` offers `folder`, and `view` offers nothing.
- **The span of a column of numbers.** For `gt`, `ge`, `lt` or `le` over numbers, where
  the numbers lie.

In an `and`, the first side that keeps nothing on its own is the one explained. Anything
else stays silent: `ne` and `has`, an `or` or a `not` that keeps nothing, and a
predicate whose every part keeps a row though the whole keeps none, as
`$row.kind eq folder and $row.size gt 0` does here. So does a filter that keeps a row,
and one given an empty table: an empty answer to an empty question needs no reason.

A script never sees an explanation. The answer is the empty table, so `count` gives `0`
and `else` has nothing to recover from:

```
$ ls | where $row.kind eq view | count
0
  explanation: kind is folder or text
```

The note is still shown, because the `where` that kept nothing is part of the line. A
line of a script that `run` runs is explained the same way, under the script's output,
but offers no fix: the line to correct is in the script, not the one you typed.

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
readme.txt  193

$ ls | sort size desc | select name size
name        size
readme.txt  193
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
<row name=readme.txt kind=text folder=/ size=193 modified=2026-09-22T09:30:00.0000000+00:00/>
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

A tree is not a dead end. `pick` finds the elements inside it and answers them as a
table: [Reading a tree with `pick`](#reading-a-tree-with-pick).

## Reading and writing files

A table can live in a file, as XML or as CSV, and comes back out as the same value it
went in as. Four commands do it: `to-xml` and `to-csv` write, `from-xml` and `from-csv`
read. A document is an ordinary file in the terminal: it shows up in `ls`, `read` shows
it as text, and `undo` takes a write away like any other.

From a fresh tab, and continuing in the same one to the end of this section:

```
$ mkdir stock
stock
$ in stock
stock
$ <items><item sku=A1 name=bolts qty=120 min=50/><item sku=B2 name=nuts qty=12 min=40/><item sku=C3 name=washers qty=0 min=20/></items> | to-xml items.xml
items.xml
$ read items.xml
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
$ read reorder.csv
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
$ read short.xml
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
$ read memo-copy.xml
<memo to="ann">Back at ten.</memo>
```

That round trip is exact for elements with only attributes, only text, or both. It is
not for *mixed* content, where text and child elements interleave: the text is
gathered, trimmed, into one attribute and written back before the children. Comments
and processing instructions are dropped, a prefixed name such as `x:item` is kept as
written, and a document with a DTD is refused.

## Reading a tree with `pick`

A tag whose children have children of their own is a tree, not a table, and the table
functions refuse it. `pick <selector>` reaches inside one. The selector is a CSS
selector, the kind a stylesheet uses to pick elements out of a web page, and the answer
is a table of every element it matches, which `where`, `select` and `sort` then work on
([decision 0049](decisions/0049-pick-selects-elements-with-css-selectors.md)). Selectors
pick by shape; comparing values stays with `where`.

### A tree typed as a tag

From a fresh tab. A library holds two books, each with an author, and an empty shelf:

```
$ set d <library city=paris><book title=dune year=1965><author name=herbert/></book><book title=emma year=1815><author name=austen/></book><shelf/></library>
<library city=paris><book title=dune year=1965><author name=herbert/></book><book title=emma year=1815><author name=austen/></book><shelf/></library>

$ $d | count
<library> is not a table: child 1 has children of its own.

$ $d | pick book
@tag  title  year  @children
book  dune   1965  1 child
book  emma   1815  1 child

$ $d | pick "book > author" | select name
name
herbert
austen
```

The table always has the same shape. `@tag`, the element's name, comes first. Then the
attributes of the elements matched, in the order they first appear, with a gap where
an element lacks one. Then `@children`, the element's children as a list. A cell is
one line, so a list in one says how many items it holds, `1 child` or `3 children`, and
an element with none has an empty cell
([decision 0051](decisions/0051-a-list-in-a-table-cell-is-summarised.md)). The cell
still holds the whole list: `$row.@children` reads it.

The rows are in document order, the order the elements are written in, and the element
piped in is one of them. `*` matches every element:

```
$ $d | pick "*"
@tag     city   title  year  name     @children
library  paris                        3 children
book            dune   1965           1 child
author                       herbert
book            emma   1815           1 child
author                       austen
shelf

$ $d | pick "shelf, author"
@tag    name     @children
author  herbert
author  austen
shelf
```

A group, several selectors joined by commas, still answers in document order and names
each element once, however many of its selectors match it. A selector that matches
nothing answers a table with only `@tag` and `@children`. Names and values are compared
exactly, as XML compares them, so `BOOK` is not `book`:

```
$ $d | pick BOOK
@tag  @children
```

### The selector

`pick` reads a subset of CSS. Every example is over `$d` above:

| Selector | Matches | In `$d` |
| --- | --- | --- |
| `book` | every element of that name | the two books |
| `*` | every element | all six, the library first |
| `[year]` | elements that have the attribute | the two books |
| `[year=1965]` | the attribute is exactly that | `dune` |
| `[title^=du]` | it starts with that | `dune` |
| `[title$=ma]` | it ends with that | `emma` |
| `[title*=m]` | it contains that | `emma` |
| `book[year][title]` | all of those together, with no spaces between them | the two books |
| `library author` | an `author` anywhere inside a `library`: the space is the descendant combinator | both authors |
| `book > author` | an `author` whose parent is a `book`: the child combinator | both authors; `library > author` matches none |
| `shelf, author` | anything any of them matches, in document order | both authors, then the shelf |

An attribute is compared as the text it shows, so `[year=1965]` finds the number
`1965`. A value may be bare or quoted with `'`, as in `[title='dune']`. A prefix, suffix
or part that is empty matches nothing, as in CSS. Spaces around `>`, `=` and inside the
brackets do not matter: `book>author` and `book > author` are the same selector.

A selector with a space, `>`, `[` or `,` in it goes in double quotes, since otherwise
it would be split into several arguments or not parse; a plain name, and `*`, need none.
Anything outside the subset (`#id`, `.class`, `:first-child`, `+`, `~`) is a fault of
kind `syntax` that says where the selector stopped and why:

```
$ $d | pick "book >"
The selector 'book >' stops at its end: an element name, '*' or '[' is expected after '>'.
```

Completion knows the document: `$d | pick ` offers the element names of what flows in,
each with how many there are, as `book · 2 elements`
([Completions](web-terminal.md#completions)).

### Where, select and sort

What `pick` answers is an ordinary table, so the rest of a question is the table
functions. The columns named with `@` are written as they are shown, with no quotes,
and a predicate reads them off `$row` like any other column. Still in the same tab:

```
$ $d | pick book | where $row.year gt 1900 | select title
title
dune

$ $d | pick book | sort year | select title year
title  year
emma   1815
dune   1965

$ $d | pick "*" | group @tag
key      rows
library  1 row
book     2 rows
author   2 rows
shelf    1 row

$ $d | pick "*" | where $row.@tag eq author | select name
name
herbert
austen
```

`group @tag` is a quick way to see what a document you have never looked at is made of.

### Picking from a pick

A table whose rows have an `@tag` column is read back as the elements they were made
from, so `pick` works on what `pick` answered. `$d.@children`, a list of tags, is read
as that many documents, each with its own root
([Members](language.md#members)):

```
$ $d | pick book | pick author | select name
name
herbert
austen

$ $d | pick "*" | pick "[year^=19]" | select title
title
dune

$ $d.@children | pick "*" | select @tag
@tag
book
author
book
author
shelf
```

The rows of `pick "*"` overlap: a book is a row of its own and also inside the library's
row. An element is still answered once, in the order of the first document it was
found in, which is why `dune` appears once above
([decision 0052](decisions/0052-pick-answers-each-element-once.md)). Two elements that
are equal but separate, such as the same tag typed twice, are still two.

### A document read from a file

`from-xml` reads a file into the same tree a tag makes, so `pick` reads it the same way.
From a fresh tab, and continuing in the same one to the end of this section, a shop's
catalogue, with a department inside another. The XML is written with `'` around its
attribute values, since a string cannot hold a `"`:

```
$ write shop.xml "<shop><dept name='tools'><item sku='A1' price='4.5'>Hammer</item><item sku='A2' price='12'>Saw</item></dept><dept name='garden'><item sku='B1' price='3'>Trowel</item><dept name='seeds'><item sku='B7' price='1.2'>Basil</item></dept></dept></shop>"
shop.xml

$ from-xml shop.xml | table
<shop> is not a table: child 1 has children of its own.

$ from-xml shop.xml | pick item
@tag  sku  price  text    @children
item  A1   4.5    Hammer
item  A2   12     Saw
item  B1   3      Trowel
item  B7   1.2    Basil

$ from-xml shop.xml | pick dept
@tag  name    @children
dept  tools   2 children
dept  garden  2 children
dept  seeds   1 child
```

An element's text is its `text` attribute, as it is everywhere a document is read
([Text inside an element](#text-inside-an-element)), and `price` is a number column
because every price reads as one. The descendant and child combinators tell the
garden's own items from the seeds department's:

```
$ from-xml shop.xml | pick "dept[name=garden] item" | select sku text
sku  text
B1   Trowel
B7   Basil

$ from-xml shop.xml | pick "dept[name=garden] > item" | select sku text
sku  text
B1   Trowel

$ from-xml shop.xml | pick item | where $row.price lt 5 | sort price | select text price
text    price
Basil   1.2
Trowel  3
Hammer  4.5

$ from-xml shop.xml | pick dept | where $row.name eq garden | pick item | select sku text
sku  text
B1   Trowel
B7   Basil
```

The last line picks the departments, keeps the one `where` chose by value, and picks
inside it: a selector cannot compare numbers, and `where` cannot see inside a tree, so
between them they ask either kind of question.

A picked table is written to a file like any other, once it has only the columns a
document can hold. XML has no name that starts with `@`, so `select` the ones you want
first:

```
$ from-xml shop.xml | pick item | to-xml items.xml
'@tag' is not a name XML allows.

$ from-xml shop.xml | pick item | select sku price text | to-xml items.xml -root items -row item
items.xml

$ read items.xml
<items>
  <item sku="A1" price="4.5">Hammer</item>
  <item sku="A2" price="12">Saw</item>
  <item sku="B1" price="3">Trowel</item>
  <item sku="B7" price="1.2">Basil</item>
</items>

$ from-xml items.xml | sort price desc
sku  price  text
A2   12     Saw
A1   4.5    Hammer
B1   3      Trowel
B7   1.2    Basil
```

`items.xml` is table-shaped, so it is a table again without `pick`.

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

An empty answer is not a failure, and is not in this table: its note says why nothing
was kept, as [An empty answer says why](#an-empty-answer-says-why) describes. The two
predicates that are not questions carry a note offering the corrected line. A column
that `select`, `sort`, `distinct` or `group` cannot find is named without one.

| Message | What happened |
| --- | --- |
| `'count' needs a table, not text.` | Something that is not a table, and cannot be read as one, reached a table function. |
| `'select' has no column named 'nowhere'.` | A column name that is not in the table. `columns` lists what is. |
| `'select' needs at least one column.` | `select` with nothing to select. The pipe is the table, not the column list. |
| `'sort' takes 'desc' or 'asc' for 'desc', not 'up'.` | A word after the column that is neither direction. |
| `<items> is not a table: child 2 is <other> where the first is <item>.` | A tag whose children disagree about their type, or one that has children of its own. |
| `Column 5: 'eq' is an operator; write "eq" to pass it as text` | A reserved word in argument position. The column is where the word starts. |
| `'echo' takes a value for 'text', not an expression.` | A comparison was written for a command that does not take a predicate. |
| `kind eq folder never reads $row, so it is the same for every row.` | A predicate that compares two fixed words. Name the column with `$row.`: the note under it offers the line that does. |
| `$row.kind is text (folder), not true or false.` | A predicate that is a value rather than a question. Compare it with something: the note under it offers a comparison. |
| `$row is the row a predicate is testing. It exists only inside where, find, in and save-view: ls \| where $row.kind eq folder.` | `$row` read outside a predicate, where no row is being tested. |
| `Column 16: a column name belongs after the stop, as in $row.kind` | `$row.` with no column after the full stop. |
| `Column 23: eq needs a value to compare with, such as folder` | A comparison with nothing on its right. |
| `Not well-formed XML : /stock/broken.xml line 1, position 16` | `from-xml` was given a file that is not XML. The line and position are where the parser gave up. |
| `'to-xml' needs a tag, a table or a list of tags, not text.` | Only something with elements in it can be written as a document. |
| `'first name' is not a name XML allows.` | A column name that XML cannot hold, often from a CSV header with a space in it. |
| `/stock/r.csv line 3 has 1 fields where the header has 2.` | A CSV record of a different width from its header. A table has one width. |
| `The selector 'book >' stops at its end: an element name, '*' or '[' is expected after '>'.` | A selector `pick` cannot read. It says where it stopped, a character or the end, and what it expected there. [Error reference](errors.md#selector-errors) has them all. |
| `'pick' needs a tag, a list of tags or a table with a @tag column, not text.` | Something with no elements in it was piped into `pick`, such as text or a listing. A listing's rows are records, not elements. |
| `'@tag' is not a name XML allows.` | A table from `pick` was written with `to-xml` as it stands. `select` the attribute columns first. |
