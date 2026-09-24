# 0037. Places are entered with `in`, left with `out`, retraced with `back`; files are read with `read`

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

The navigation commands kept the shell's names: `cd` to go somewhere, `up` to come
out, `cat` to show a file. Since [0013](0013-attribute-filesystem.md) a place is a
folder or a question, and going into one reads more like zooming in on a view than
changing directory. The owner asked for names that read as moves, and for a way back
to where you were.

## Options

1. **Keep the shell's names.** Familiar to anyone who knows a shell, and unexplained to
   anyone who does not.
2. **New names, the old ones kept as aliases.** Nothing breaks, and there are two names
   for everything.
3. **New names, the old ones as keywords.** `in`, `out`, `back` and `read`; typing an old
   name finds the new one, in completion and in the fault for an unknown command.

## Decision

Option 3, chosen by the owner. `in` goes into a folder, a saved view or a question
written out; `out` comes out of the view, or up out of the folder; `back` goes to where
you were before the last move, one step further each time, like a browser's back
button; `read` shows a file. `cd`, `up` and `cat` are the first keywords of `in`, `out`
and `read`, so `cd` in completion offers `in` (`in · matches "cd"`) and running it says
`Unknown command : cd. Did you mean in?`. `undo` takes a `back` back like any move.
The location line's `up` button becomes `out`.

## Consequences

Every document, test, example program and guide file that used the old names is
rewritten, and the example programs' golden results with them. Decision records
before this one keep the names they were decided with.
