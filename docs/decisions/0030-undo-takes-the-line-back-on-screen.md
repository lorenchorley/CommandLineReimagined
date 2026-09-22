# 0030. On screen, undo takes the line back rather than adding one

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-22 |

## Context

Undo is a command ([0010](0010-undo-by-event-sourcing.md)), so on the page it was
drawn like every other line: `mkdir alpha`, then `undo`, left both entries on screen,
the second one saying `Undone: mkdir alpha`. That is exactly what the log holds, but
it is not what the person at the prompt meant by undo. They meant that the last thing
they did did not happen, and the screen showed them the event record instead: a line
they had taken back still sitting there, and a line about taking it back beneath it.
Redo made it worse, because a third entry then described putting back something that
looked as if it had never gone away.

## Options

1. **Leave it.** The screen is a faithful record of the log. It is honest, and it
   shows the mechanism instead of the effect.
2. **Erase the entry.** Undo removes the line it reversed from the screen for good, and
   leaves nothing of its own. Redo then has nothing to put back, and would have to
   draw the line again at the bottom, out of place and without its original output.
3. **Hide the entry, and let redo show it.** Undo hides the entry for the line it
   reversed and leaves none of its own; redo shows that entry again, where it was, as
   it was. The log is unchanged, and `history` still lists the undo and the redo as
   lines of their own.

## Decision

Option 3. The session's response says which lines a line committed, undid and redid,
by the sequence number of the line itself rather than of the compensation, so a host
that remembers the numbers each entry committed can find the entry an undo is about
without looking at the log. An entry is hidden once every line it committed is undone,
which is one line usually and one per script line for `run`
([0020](0020-scripts-and-run.md)).

The undo keeps its entry when there is nothing on screen to act on: when there was
nothing to undo, when the line it reversed was cleared or came from before a reload,
or when the undo also committed or wrote something. Otherwise nothing at all would
show that it had done anything.

## Consequences

The screen shows the effect and `history` shows the record, and the two are no longer
the same thing. The response carries one more field, and `reset`, after which sequence
numbers start again, has to say so in it or a host would hide the wrong entry. The
desktop shell is unchanged ([0012](0012-browser-first.md)): the numbers reach it too,
but it still draws the undo as a line of its own.
