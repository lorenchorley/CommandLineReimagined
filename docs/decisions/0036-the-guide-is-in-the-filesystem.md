# 0036. The guide to the terminal lives in its filesystem, and the banner points to it

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-23 |

## Context

The banner at the top of the page had grown, phase by phase, into a paragraph that was
part change log and part manual: tables, views, `else`, `try`, `??`, live listings,
tags, components and the function form, all before anything had been typed. On a phone
it filled most of the screen, and it said too little about any one idea to teach it.

## Options

1. **Keep the banner, and trim it.** Short enough to fit, and then too short to
   explain anything.
2. **A help page beside the terminal.** A second surface to keep in step with the
   first, reached by leaving the terminal.
3. **Put the guide in the filesystem.** `readme.txt` says where to start, and a
   `guide` folder holds one file per idea, read with `cat`. The banner only points to
   the readme.

## Decision

Option 3, chosen by the owner. The seed gains a `/guide` folder of seven files, one
per fundamental idea: getting around, values and pipes, tables, records and views,
failure as a value, undo and history, and files and scripts. Each ends by naming the
next. `readme.txt` points to the first, and the banner says only to run
`cat readme.txt` (the `readme` key does it), and whether this browser keeps your files.

The guides are text files in the repository's `guide/` folder, embedded in the core
the way the example programs are, so the browser build carries them. Every line a
guide shows as an example is run by a test from a fresh session, and must succeed,
except the three shown failing on purpose, which must fail.

## Consequences

Learning the terminal is done with the terminal, which is its own first exercise.
The seed now has four folders at the root and a longer readme, so every listing of the
root changes: the golden results of `tables.clr` and `resilient.clr` in
[the example programs](../plan/examples.md) gain a `guide` row and a 192-byte readme,
and four folders rather than three. The programs themselves are unchanged.
Phase 8's acceptance table, written before this, still reads `table · 4 rows`; it
records what that phase checked.
