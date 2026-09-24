# 0039. After a complete stage, completion offers the pipe first

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

After `vars ` completion offered file names, although `vars` takes nothing, and after
`ls ` it offered paths, although the likelier next step is to question the listing.
Everything a command answers is data that can be piped on, and nothing on the screen
said so.

## Options

1. **Leave the arguments as they are.** Correct for a command that still wants one, and
   misleading for one that does not.
2. **Offer the pipe among the rest.** Easy to miss in a row of file names.
3. **Offer the pipe first** when the stage has all its required arguments, and nothing
   else when the command has no parameter left to take.

## Decision

Option 3, chosen by the owner. With the word under the cursor empty and every required
argument of the stage written, the first chip is `|`, detail `send the result on`. A
command with nothing left to take, `vars` for one, offers only `|`. A command still
missing a required argument, `sort ` for one, offers what that argument takes and no
pipe.

## Consequences

The first Tab after `ls ` writes `| ` and the chips move on to the commands that take a
table, which is the whole of how a listing becomes a question.
