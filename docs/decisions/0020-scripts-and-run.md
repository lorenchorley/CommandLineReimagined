# 0020. Scripts are files of command lines, and `run` executes them one line at a time

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-22 |

## Context

The [four example programs](../plan/examples.md) are the proof that the design works,
and they have to be runnable in the hosted build rather than only in a test. That needs
a file format and a command that executes one.

It also needs an answer to a question [0015](0015-atomic-lines.md) left open: a line is
one transaction, so what is a *file* of lines? One transaction for the whole file would
make a script an all-or-nothing operation and would mean a failure halfway through
silently undoing work the user watched succeed.

## Options

| Option | What it does | Cost |
| --- | --- | --- |
| A. One transaction per line | Each line commits as if it had been typed. Undo steps back through the script line by line. | A script that fails halfway leaves its first half in place. |
| B. One transaction for the whole file | The file is atomic: it all happens or none of it does. | `undo` after a script undoes the entire script, which for a long one is not what anybody wants; and a line that reads what an earlier line wrote is already served by the working projection. |
| C. A script is a value, not a file | `run` takes text from a pipe. | Nothing to name in `history`, nothing to `cat`, and no way to keep one. |
| D. A script language | Loops, conditionals, functions. | A different project. The pillars are tables, views, failure-as-a-value and XML; none of them needs control flow. |

## Decision

Option A. A script is a text file of kind `script`, extension `.clr`, one command line
per line. Blank lines and lines whose first non-space character is `#` are skipped.
`run path` executes each remaining line exactly as if it had been typed, each as its own
transaction, and stops at the first fault, re-raising it as
`<path> line <n>: <message>` with the original kind and `n` counting every line in the
file including the skipped ones.

`run` is a meta command: it commits nothing of its own, which is what lets the lines it
runs commit theirs. It writes each line to the output as `> <line>` followed by its
result, observes cancellation between lines, and refuses to nest deeper than eight.

The four example programs are embedded in the core assembly and seeded into `/examples`,
so the hosted build runs them as they stand.

## Consequences

`undo` after a script undoes its last line, and running it again after a failure repeats
the part that already succeeded — so the example programs are written to be run from a
fresh terminal, which is what `reset` is for. There is no control flow, so a script is
a recording rather than a program; if that turns out to be too little, it is a later
decision to take and not one this forecloses.
