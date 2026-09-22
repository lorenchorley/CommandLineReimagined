# 0023. The function form needs its parenthesis against the name; a spaced parenthesis is a nested pipeline

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-22 |

## Context

Phase 5 lets a pipeline in parentheses stand as an operand: `first (ls | where
$row.kind eq view)` hands `first` whatever the inner pipeline produced. The grammar
already had a use for a parenthesis after a command name — the function form,
`write(notes.txt, "hello")` — and allowed space between the two, so `first (ls)` read
as `first` called with one argument, `ls`. Worse, `first (ls | count)` did not parse at
all: the function form committed at the `(`, and a pipe is not a function argument.

The two readings of `name (` cannot both stand. Something has to decide which one a
line means.

## Options

1. **Keep the space optional in the function form; spell the nested pipeline some
   other way**, such as `$(ls)`. A shell user would recognise it. It is two shifted
   characters on a phone for the commonest nesting there is, and `$` already means "a
   variable" everywhere else in the language.
2. **Decide by what is inside**: a function call if the contents parse as a function
   argument list, a nested pipeline otherwise. `first (ls)` would be a function call and
   `first (ls | count)` a nested pipeline, so adding a stage to the inner pipeline would
   change what the outer one means. That is the kind of rule nobody can keep in their
   head.
3. **Decide by adjacency.** `name(` with nothing between is the function form; `name (`
   with a space is a command whose first argument is a parenthesised pipeline. It is the
   same rule decision [0022](0022-hyphenated-command-names.md) uses to keep `ls -l` a
   flag: a space ends the name.

## Decision

Option 3. It is how most languages that allow both already read (`f(x)` against
`f (x)` in any shell), it costs nothing to type, and a line means the same thing
whatever the inner pipeline holds.

## Consequences

`command (value)` changed meaning: it was a function call with one argument and is now
`command` with one argument, the result of running the line `value`. No documented
example used the spaced form, and no test did either; the equivalence corpus pins only
the adjacent one. The serialiser already wrote the function form tight against its name,
so a round trip is unchanged.

A parenthesised pipeline gets no pipe input: `ls | first (count)` hands `count`
nothing, because the pipe belongs to the stage the parenthesis is in, not to the
pipeline inside it. Its events join the line's transaction, so undoing the line undoes
them too.
