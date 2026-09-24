# 0038. A command called wrongly shows its help

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

A call with the wrong number of arguments, a missing one, an unknown flag or a value of
the wrong kind fails with a sentence such as `'help' takes 1 argument, but 2 were
given.`. The sentence says what is wrong and not what is right, and the person has to
think of running `help help` to find out.

## Options

1. **Keep the sentence.** Short, and a dead end.
2. **Put the usage in the sentence.** `Usage: sort <column> [desc] [table]` appended.
   Better, and still one line where the parameters each have something to say.
3. **Answer the help with the fault.** The line fails as before, and the response also
   carries the table `help <command>` answers, which the page draws under the error.

## Decision

Option 3, chosen by the owner. It applies where binding fails for a command that
exists. A fault raised while a command runs, such as a file that is not there, is not a
call made wrongly, and carries no help. Nor does an unknown command, which already
says what it probably meant.

## Consequences

`Session.Response` and the web response gain a `guide`, absent on every line that did
not fail to bind. Scripts are unaffected: `else` and `try` see the same fault.
