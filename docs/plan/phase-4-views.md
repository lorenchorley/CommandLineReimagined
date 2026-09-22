# Phase 4: queries as views

**Goal.** A predicate over file attributes is a place you can be in. `cd` accepts a
predicate, `ls` lists it, `pwd` shows it, `find` runs one ad hoc, and the browser
refreshes a listed view when the store changes. Decision
[0013](../decisions/0013-attribute-filesystem.md), option 3, in full.

## Semantics

- `Location = { Folder: string; View: Expr option }`. `cd <folder-path>` sets `Folder`
  and clears `View`, returning the folder as a `File`. `cd <predicate>` sets `View` and
  leaves `Folder`, returning the predicate as a `Query`. `up` with a view set clears
  the view and returns `Text` of the folder path; without one, moves to the parent
  folder. Both emit `LocationChanged`, so undo restores where you were.
- Binding: `cd`'s single parameter has kind `Predicate`. A plain operand arrives as
  `Operand v` and is treated as a folder path; anything containing an operator is a
  view. The same rule serves any command that wants "a name or a query".
- `ls` with a view set returns every record in the store matching the view, with a
  `folder` column so rows from different folders are distinguishable. Without a view it
  lists `Folder` as in Phase 1.
- `pwd` returns a `Query` value when a view is set (`display` is the predicate text),
  otherwise `Text` of the folder path.
- `find <predicate>` is `ls` over the whole store with an ad hoc predicate, without
  changing location.
- `save-view name <predicate>` stores the predicate as a file of kind `view` in the
  current folder, with the predicate text as content; `cd name` on a view file enters
  it. Views therefore appear in `ls`, can be tapped, undone and deleted like any file.
- New files are still created in `Folder`, never "in a view".

## Live views

`Store.Changed` fires after every commit. The bridge forwards it as
`window.terminal.storeChanged(seq)`. The page marks the most recent `ls` or `find`
entry as live; on `storeChanged` it re-runs that line silently through a new
`Refresh(source)` bridge method that executes without creating a scrollback entry or a
transaction (meta execution: only read-only commands are allowed, otherwise the refresh
is refused). The refreshed table replaces the old one in place with a brief highlight.
Only the latest listing is live; older ones freeze. A toggle in the entry turns live
refresh off.

## Browser

The location line shows `folder` or `view: <predicate>`. Tapping a `folder` cell in a
table inserts `cd <path>`. Completion after `cd ` offers folders and view files.

## Tests

- Core: `cd` predicate then `ls` lists across folders; `up` clears the view; undo
  restores the previous location; `find` does not change location; `save-view` then
  `cd name` enters the view; `Refresh` refuses a mutating line.
- Browser check: `run examples/journal.clr` completes; `cd $row.kind eq folder` then
  `ls` shows the folders; `mkdir x` in another entry makes the live listing gain a
  row; `up` returns to `/`.
- `ExampleProgramTests`: every golden result of `journal.clr`, and afterwards
  `tuesday` exists with `mood = better`.

## Documentation

`docs/filesystem.md` (new): the attribute model, reserved attributes, folders as
records, views, `find`, `save-view`, live listings; `docs/commands.md`; `docs/spec/`
execution model (Location) and command catalogue; the design doc's "Data storage"
section rewritten around the log and projections.

**Record to add.** 0013 covers the semantics, and the refresh rules were built as
written, so nothing here needed one. One turned up from underneath:
[0022](../decisions/0022-hyphenated-command-names.md), because `save-view` is two
words and the grammar took a command's name as an identifier, which has never
contained a hyphen. Phase 6's four commands need the same rule.

This document said to number a refresh-rules record 0021, which was taken during
Phase 3 by variadic parameters. Records are numbered in the order they are taken, not
reserved in advance; the [architecture](architecture.md#expected-new-decision-records)
table of expected numbers has been shifted to match.
