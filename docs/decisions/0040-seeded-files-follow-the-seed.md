# 0040. A seeded file nobody has changed follows the seed

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

The seed runs only on an empty log, so the files it wrote stay as they were written.
[0036](0036-the-guide-is-in-the-filesystem.md) added the guide to older logs once, and
brought `readme.txt` up to date only when it held one known old text. Then
[0037](0037-in-out-back-and-read.md) renamed `cd`, `up` and `cat`, and every guide file
and the readme changed with it. A returning visitor would read a guide that tells them
to type commands that no longer exist, and the owner asked for the guide to be kept up
to date.

## Options

1. **Leave returning visitors' files as they are.** They are told `cat readme.txt` by
   a terminal that answers `Unknown command : cat`.
2. **Match known old texts.** Keep every earlier version of every seeded file, and
   replace a file whose content is one of them. Correct, and a list that grows with
   every edit to the guide.
3. **Follow the log.** A seeded file that no line anyone typed has ever changed is still
   the terminal's, and is brought to the seed's current content on load. One the user
   has written to, renamed, moved or tagged is theirs, and is left alone.

## Decision

Option 3. On load, after replay, `Session.BringUpToDate` compares each file the seed
describes with the record at its path. When the record was created by a system
transaction (the seed, or 0036's guide), no undoable transaction has touched it since,
and its content differs from the seed's, its content is replaced, in one system
transaction with the source `seed update`, which nobody can undo. A seeded file the
user deleted is not brought back. 0036's rule for adding the guide to a log that never
had one is kept.

## Consequences

Editing the guide or the example programs reaches every returning visitor who has not
changed those files. A visitor who has edited one keeps their edit, and sees the new
text only after `reset`.
