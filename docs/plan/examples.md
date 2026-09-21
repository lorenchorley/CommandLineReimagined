# Example programs: the proof

Unit tests prove parts. These four programs prove the whole: each is a script in the
language, one screen long, that exercises one pillar of the design end to end. They
are written now, before the code, so that the implementation is built against them
rather than the other way round.

The scripts live in [`examples/`](../../examples/README.md). They are embedded in the
core assembly and seeded into every fresh terminal under `/examples`, so the hosted
build runs them as they stand: `run examples/journal.clr`.

| Program | Pillar | Complete after |
| --- | --- | --- |
| `tables.clr` | Listings are tables; predicates with word operators and `$row`; table functions | Phase 3 |
| `journal.clr` | Attribute records; views as places; undo and history from the event log; atomic lines | Phase 4 |
| `resilient.clr` | Failure as a value: `else`, `try`, `??`, nested pipelines | Phase 5 |
| `inventory.clr` | Tag to table coercion; XML and CSV as real files | Phase 6 |

## How they are verified

Three ways, all required:

1. **`Core.Tests/ExampleProgramTests.fs`.** For each program, a fresh session runs each
   line in turn through `Session.Execute` and asserts the golden result below for that
   line. Timestamps are matched with `*`. Then a second fresh session runs the whole
   file through `run` and asserts no fault and the same final projection.
2. **`tools/browser-check.mjs`.** Runs `run examples/<name>.clr` in Chromium and asserts
   the last rendered entry, plus a handful of the individual lines rendered as their
   own entries so table and fault rendering is seen.
3. **`docs/examples.md`.** Each program is reproduced with its real output pasted in,
   as the user documentation's worked examples already are.

The golden results are normative. A phase is not complete while its program's golden
results differ, and a program is never edited to match the implementation without a
decision record saying why.

## The `run` command

Added in Phase 3, when the first program becomes runnable. Decision record 0021.

| Field | Value |
| --- | --- |
| Name | `run` |
| Parameters | `path` (piped) |
| Returns | the value of the last line executed |
| Meta | yes: `run` itself commits nothing; each line it executes commits its own transaction |

Rules:

- The file is read as text and split on line breaks. Blank lines and lines whose first
  non-space character is `#` are skipped.
- Each remaining line is parsed and executed exactly as if typed, with
  `Transaction.Source` set to the line's text. A line with no events commits nothing.
- Execution stops at the first fault. The fault is re-raised with the message
  `<path> line <n>: <message>` and the original kind, where `n` counts every line in
  the file including skipped ones.
- Each line's source is written to the output as `> <line>`, followed by the display
  string of its result, so the scrollback shows the run.
- Cancellation is observed between lines. A nested `run` is allowed to a depth of 8;
  deeper is an `Invalid` fault.
- Scripts have kind `script`; the extension `.clr` infers it.

## Golden results

Each program starts from a fresh terminal: root `/` containing folders `documents`,
`examples`, `projects` and the file `readme.txt`, with `documents/notes.txt`. Results
are display strings; a table is shown as its rows. `*` matches anything.

### tables.clr

```
ls
  name        kind    folder  size  modified
  documents   folder  /       0     *
  examples    folder  /       0     *
  projects    folder  /       0     *
  readme.txt  text    /       41    *

ls | where $row.kind eq folder | count
  3

ls | sort name desc | first
  <row name=readme.txt kind=text folder=/ size=41 modified=*/>

ls | select name kind | take 2
  name       kind
  documents  folder
  examples   folder

set greeting hello
  hello

set answer 42
  42

vars
  name      value
  answer    42
  greeting  hello

help | where $row.name eq set | select name description
  name  description
  set   Bind a value, or whatever was piped in, to a variable
```

From Phase 3, `ls` has no parent row: a table of records has nowhere for one. The
page offers `up` in the location line instead.

### journal.clr

```
mkdir journal                                     journal
cd journal                                        journal
save <note name=monday mood=good tag=work/>       monday
save <note name=tuesday mood=tired tag=work/>     tuesday
save <note name=saturday mood=great tag=home/>    saturday
echo "Stand-up moved to ten." | write monday      monday
cat monday                                        Stand-up moved to ten.
attr tuesday mood=better                          tuesday

ls
  name      kind  folder    size  modified  mood    tag
  monday    note  /journal  22    *         good    work
  saturday  note  /journal  0     *         great   home
  tuesday   note  /journal  0     *         better  work

find $row.kind eq note and $row.tag eq work | count
  2

cd $row.mood eq great                             $row.mood eq great

ls
  name      kind  folder    size  modified  mood   tag
  saturday  note  /journal  0     *         great  home

up                                                /journal
rm tuesday                                        Removed tuesday
undo                                              Undone: rm tuesday
history | where $row.undone eq true | count       1
```

After the run, `tuesday` exists again with `mood = better`: undo restored the record
as it was, attributes included.

### resilient.clr

```
cat notes-from-yesterday.txt else echo "starting fresh"
  starting fresh

cat notes-from-yesterday.txt else echo "starting fresh" | write today.txt
  today.txt

cat today.txt
  starting fresh

try cat nowhere.txt | set problem
  File does not exist : /nowhere.txt              (a fault value, not an error)

echo $problem.kind                                NotFound
echo $problem.message                             File does not exist : /nowhere.txt

first (ls | where $row.kind eq view) ?? "no views yet"
  no views yet

first (ls | where $row.kind eq view) ?? "no views yet" | set latest
  no views yet

echo $latest                                      no views yet

mkdir today | cd nowhere else echo "the whole line was rolled back"
  the whole line was rolled back

ls
  name        kind    folder  size  modified
  documents   folder  /       0     *
  examples    folder  /       0     *
  projects    folder  /       0     *
  readme.txt  text    /       41    *
  today.txt   text    /       14    *
```

No folder named `today` exists: the left side of the `else` failed at `cd`, so the
`mkdir` before it was never committed.

### inventory.clr

```
mkdir stock                                       stock
cd stock                                          stock

<items><item sku=A1 name=bolts qty=120 min=50/><item sku=B2 name=nuts qty=12 min=40/><item sku=C3 name=washers qty=0 min=20/></items> | to-xml items.xml
  items.xml

from-xml items.xml | count                        3

from-xml items.xml | where $row.qty lt $row.min | sort qty | select sku name qty min
  sku  name     qty  min
  C3   washers  0    20
  B2   nuts     12   40

from-xml items.xml | where $row.qty lt $row.min | select sku qty | to-csv reorder.csv
  reorder.csv

from-csv reorder.csv | count                      2

cat reorder.csv
  sku,qty
  C3,0
  B2,12

from-xml items.xml | sort qty desc | first
  <row sku=A1 name=bolts qty=120 min=50/>
```

`qty` and `min` are number columns because every value parses as a number, which is
what makes `lt` numeric and `sort qty` numeric rather than textual.
