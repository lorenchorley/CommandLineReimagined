# 0031. Completion reads the line, and may run what comes before the cursor

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-23 |

## Context

`Core/Completion.fs` guesses from the text. It takes the last word, checks whether a
`$` appears earlier in the stage, and otherwise offers the files in the current
folder. It never asks the parser or the binder where the cursor is, although both
already know. An audit of about fifty partial lines, recorded in
[Phase 8](../plan/phase-8-intellisense.md#findings), found the consequences:
`ls | sort ` offers file names, `$v.` offers a listing's columns for a number, and
`$row` is offered where it can only fail.

Two questions follow. How does completion learn where the cursor is? And how does it
learn what flows into the stage being typed, which is what columns and column values
depend on?

## Options

1. **Keep guessing from the text.** Cheap, and wrong in the ways the audit lists.
2. **Read the line, and infer what flows in statically.** Parse with a placeholder
   for the word being typed, then derive each stage's columns from declarations on
   every command, saying how its output relates to its input. Precise where the
   declarations are right, and every command, present and future, has to carry one.
3. **Read the line, and run the stages before the cursor.** Parse the same way, then
   run the upstream stages the way a live listing re-runs a line
   (`Session.Refresh`): read-only commands only, nothing committed, nothing in the
   history, nothing on the screen, under a time budget.

## Decision

Option 3. Completion replaces the word under the cursor with a placeholder, closes
whatever the line left open, parses, and walks the tree to the placeholder; the node
it sits in names the place (command name, argument and parameter, predicate state,
variable member, tag). To learn what flows in, it runs the stages before it:

- read-only commands only;
- nothing committed, nothing in the history, nothing written to the screen;
- a time budget of 150 ms, abandoned when a newer keystroke arrives.

When no variant of the line parses, or the budget runs out, or a stage is not
read-only, completion falls back to today's lexical answer. Today's rules are kept
as that fallback, not deleted.

## Consequences

Completion becomes asynchronous: `Session.Complete(text, cursor)` returns an
`Async`, and the page drops any answer older than the latest keystroke. Results are
cached by upstream text and store sequence number, so typing inside the last stage
does not re-run the ones before it. Commands need no new declaration to be completed
well, only the `Takes` annotation on their parameters. This changes no language, so
it was recorded as Accepted when Phase 8 was planned, without waiting on the owner.
