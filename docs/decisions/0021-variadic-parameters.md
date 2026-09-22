# 0021. A command may declare one parameter that collects the rest of the arguments

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-22 |

## Context

`select name kind` names two columns, and `select sku name qty min` names four. Until
now every parameter took exactly one argument and anything left over was
`'select' takes 1 argument, but 4 were given.` The binder had no way to say "and the
rest".

## Options

| Option | What it does | Cost |
| --- | --- | --- |
| A. One argument, comma separated | `select name,kind`. | A comma already separates arguments in the function form, so `select(name,kind)` would mean something different from `select name,kind`. |
| B. A list value | `select <columns><c v=name/>...`. | Unwritable by hand for the commonest operation there is. |
| C. A variadic parameter | The last declared parameter may collect every remaining positional argument as a `List`. | One more `ParamKind`, and a command that declares one can never report "too many arguments". |
| D. Fixed arity, several optional parameters | `select` declares `column1` to `column8`. | An arbitrary limit, eight parameters in `help`, and a wrong count reported as a missing `column9`. |

## Decision

Option C. `ParamKind.Rest`: at most one parameter, declared last, binds a `Value.List`
of every positional argument that the earlier parameters did not take. It is optional by
construction — no arguments left means the empty list — so a command that needs at least
one says so itself, with a message about columns rather than about arity.

A rest parameter takes the pipe like any other when nothing positional was written, so
`ls | select` is a binding fault from `select` rather than a silent empty listing.

## Consequences

`select` reads the way a person would write it. The binder's step 3, which reports
extra arguments, is unreachable for a command that declares a rest parameter, so such a
command owns its own arity message. `help` shows a rest parameter as `name...`.
