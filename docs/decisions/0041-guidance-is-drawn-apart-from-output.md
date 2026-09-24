# 0041. What the terminal says of its own is drawn apart from output

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

Phase 9 put the help under a wrong call, the banner and the `copied` note on the page,
and gave them a panel of their own. A fault's suggestions stayed inside its sentence:
`Unknown command : lss. Did you mean ls?` is drawn in red, as the error, and read out
by `$problem.message` in a script. The owner asked that anything that is not the
command's intended output look very different from it.

## Options

1. **Leave suggestions in the message.** One place to read, and the suggestion is drawn
   as an error and becomes part of a value a script compares.
2. **Style the sentence's second half.** The page would have to find it in the text.
3. **Separate output from guidance.** Output is what a command answered: a table, a
   file's text, a value, and a fault's message. Everything else the terminal says is
   guidance, carried beside the answer as notes and drawn in one style of its own.

## Decision

Option 3, chosen by the owner. Guidance is a fault's suggestions and fixes, the help
under a wrong call, an explanation of an empty answer, the banner and `copied`. It is
drawn as an accent-bordered panel with a small label, in the interface face rather
than the terminal's monospace. Output keeps the look it has.

A fault's message says what went wrong and nothing more: `Did you mean …?` moves out
of `Unknown command`, out of the predicate that never reads `$row` and out of the one
that is not true or false, into a note.

A script never sees guidance. It is not in any value: a fault that `try` or `else`
makes into a value has no notes, and `else`, `try` and pipes behave as they did.

## Consequences

`Session.Response` gains `Notes`, each note a kind (`suggestion` or `explanation`), a
text and its fixes (0044), and the web response gains `notes`. The messages that lose
their suggestion change in the error reference, and `$problem.message` reads the
shorter sentence.
