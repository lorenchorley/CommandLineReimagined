# 0022. A command's name may be several words joined by hyphens

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-22 |

## Context

Phase 4 adds `save-view`, which keeps a predicate as a file. Phase 6 adds `from-xml`,
`to-xml`, `from-csv` and `to-csv`. The grammar takes a command's name as an
`Identifier`, and `{IdentifierCharacter}` has never included the hyphen, so `save-view`
parsed as the command `save` followed by the flag `-view` — a line that binds an
argument nobody wrote to a command that was never asked for.

Every one of these names is two words. Nothing in the language makes them one.

## Options

1. **Run the words together**: `saveview`, `fromxml`, `tocsv`. No grammar change, and
   names that read as a single mistyped word. `tocsv` is not a word anybody would guess
   at, and completion cannot help with a spelling nobody expects.
2. **Underscores**: `save_view`. Already an identifier character, so no change at all.
   It is the wrong shift key on a phone, which is the objection decision
   [0007](0007-notation-conflicts.md) raised against every other punctuation choice.
3. **A hyphen inside the name, with the hyphen adjacent on both sides.** `save-view` is
   one name; `ls -l` is a command and a flag, because the space before the hyphen ends
   the name. The command-name rule becomes `Identifier ( "-" Identifier )*` and nothing
   else in the grammar moves.

## Decision

Option 3. It is how every shell in the world spells a two-word command, it is one
character on the phone keyboard's first layer, and the adjacency rule keeps flags and
negative numbers exactly as they were: `ls -l` is still a flag, `echo -5` is still
minus five, and both are pinned by tests.

## Consequences

`CommandExpression_CLINotation` and `FunctionExpression` take a command name rather
than an identifier; nothing else does, so a hyphen is still not an identifier character
anywhere else in the grammar — a variable, an attribute and a parameter name are
unchanged. A word written with a hyphen adjacent on both sides where a command name is
expected is one name, which means `ls-l` is now an unknown command rather than `ls`
with a flag. That reading was never intended and was never documented.

A name the command list does not have still reports `Unknown command : save-view`,
which is the message that makes a misspelling findable.
