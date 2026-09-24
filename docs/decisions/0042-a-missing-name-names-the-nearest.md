# 0042. A missing file or column names the nearest ones

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

Phase 8 made a mistyped command say what it was probably meant to be. A mistyped file,
folder, variable or column still says only that it is not there: `read notes` at the
root answers `File does not exist : /notes`, though `documents/notes.txt` is one folder
away.

## Options

1. **Commands only.** As today.
2. **The current folder only.** Cheap, and misses the commonest case, a file that is
   somewhere else.
3. **Near names anywhere.** First a name in the current folder within the edit distance
   `Nearest` uses for commands; then, anywhere in the filesystem, the same name, or a
   name it is the start of (`notes` for `notes.txt`).

## Decision

Option 3, chosen by the owner. `read notes` fails as it does now, and its response
names the nearest paths, at most three, with a fix for each (0044). A missing folder is
treated the same way, among folders. An unknown variable names the nearest variables.
A column a predicate reads that no row has is named with the nearest columns, as part
of the explanation of an empty answer (0043).

## Consequences

The suggestions are notes (0041), so the fault itself, its kind and its message are
unchanged, and so is everything a script sees.
