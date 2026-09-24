# 0046. What the page says about restoring a session is guidance

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

Decision 0041 draws what the terminal says of its own apart from output, and names the
banner and `copied` among it. When a stored session cannot be restored, the page says
so (`N stored line(s) could not be read and were skipped. reset starts over.`,
`Could not restore the session…`) in red, as the error of a line nobody typed.

## Options

1. **Keep them red.** An error, though no command failed.
2. **Draw them as guidance**, in the banner's style.

## Decision

Option 2, chosen by the owner. They are the terminal speaking, not a command's answer,
so they are drawn in the one guidance style, labelled as notes.

## Consequences

The only red lines left are faults of lines someone ran.
