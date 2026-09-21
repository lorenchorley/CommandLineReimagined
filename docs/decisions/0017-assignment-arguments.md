# 0017. `name=value` in argument position is data; `name: value` binds a parameter

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-21 |

## Context

The attribute filesystem ([0013](0013-attribute-filesystem.md)) needs a way to write
attributes onto a file from the command line: `attr notes.txt tag=work due=2026-10-01`.
The grammar already has two notations that look like this. A tag attribute is
`name=value` inside `<>` or `{}`, and an optional command argument is `name: value`,
which binds a value to a declared parameter by name.

If `name=value` bound parameters too, then `attr` could not take arbitrary attribute
names, because the binder would reject every name the command had not declared. If it
did not bind parameters, a user who writes `progress steps=20` expecting it to work
gets a surprise.

## Options

1. **`name=value` binds a parameter, like `name: value`.** One notation for naming an
   argument; arbitrary attribute names then need a different spelling, such as a tag:
   `attr notes.txt <attrs tag=work/>`. Verbose for the common case.
2. **`name=value` is data, `name: value` binds.** Two notations that look similar and
   mean different things, told apart by one character.
3. **`name=value` binds when the name is declared and is data otherwise.** Reads well
   until a command adds a parameter and a user's existing line changes meaning.

## Decision

Option 2. An argument of the form `name=value`, written with no spaces around the
`=`, is an *assignment*: a name and a value carried as data. It binds to a command's
single `Assignments` parameter if the command declares one, and is a `Binding` fault
naming the command otherwise, so `progress steps=20` says what is wrong rather than
silently doing nothing. `name: value` keeps its meaning and is the only way to bind a
declared parameter by name.

Option 3 is rejected because it makes a command's parameter list part of the language's
syntax: adding a parameter would silently change what existing lines mean.

The no-spaces rule is what keeps `echo a = b` three ordinary words.

## Consequences

`attr` and `save` take any attribute name without quoting or a tag. The grammar gains
an `AssignmentArgument` node, and the binder gains an `Assignments` parameter kind
that collects every assignment in order. A command that declares no `Assignments`
parameter rejects assignments with a message that names it, which is a better failure
than the silent one. Tag attributes and assignments now share a spelling, which is the
point: a tag with attributes is exactly a file record ([0013](0013-attribute-filesystem.md)).
