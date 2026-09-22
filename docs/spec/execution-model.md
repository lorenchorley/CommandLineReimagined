# Execution model

Normative semantics: the value model, how arguments bind, how a pipeline runs, how tags
evaluate, and what undo and cancellation guarantee.

Reference implementation: `Core` (assembly `CommandLineReimagined.Core`).

## Values

A command returns a `Value`. Every value answers two questions: how it reads to a
person, and what it means as an argument to another command.

| Value | Carries | Display string | Argument string |
| --- | --- | --- | --- |
| `Empty` | nothing | empty | empty |
| `None` | nothing | empty | empty |
| `Text` | the text | the text | the text |
| `Number` (double) | the number | `0.###`, invariant culture | same |
| `Boolean` | the flag | `true` or `false` | same |
| `File` | `Id`, `Name`, `Kind`, `Folder` | the name | the full path, `folder/name` |
| `List` | items | items' display strings, space separated | same |
| `Object` | `TypeName`, `Attributes`, `Children` | the tag as written | same |
| `Component` | `TypeName`, `Attributes`, `Children` | the tag as written | same |

`Empty` means "this command returns nothing"; `None` means "the answer is that there is
nothing". They read alike and are distinct, and an implementation **must** keep them so.

A `File` whose `Kind` is `parent` is the entry `ls` puts at the head of a listing below
the root. It displays as `up` and **must** argue the folder it points at, not a path
built from its name.

The two string forms **must** differ for files: display gives the name, and the
argument form gives the full path. This is what makes `ls | cd` work with no quoting
rule. Implementations **must not** collapse them.

## The command model

| Type | Purpose |
| --- | --- |
| `CommandSpec` | `Name`, `Description`, `Keywords`, `Parameters`, `Meta`. |
| `Parameter` | `Name`, `Description`, `Optional`, `Flag`, `Default`, `AcceptsPipe`, `Kind`. |
| `ParamKind` | `Single`, `Predicate` (Phase 3), `Assignments`. |
| `Invocation` | `Spec`, `Args`, `Assignments`, `Input`, `Output`, `Scope`, `Projection`, `Location`, `Blobs`, `Cancel`. |
| `CommandResult` | `Value` for the next stage, and `Events` describing what changed. |
| `Command` | A `CommandSpec` and `Run: Invocation -> Async<Outcome<CommandResult>>`. |

A command **must** declare its parameters in the order positional arguments fill them.

**A command does not change anything.** It reads `Projection` and `Location`, returns a
value and a list of events, and the evaluator commits them. An implementation **must
not** give a command a way to append to the log. Because of this, running a command
twice against the same projection **must** produce the same result, and there is no
per-command undo state for a second invocation to replay.

`Meta` marks a command as being about the log rather than about the world. `undo`,
`redo`, `history` and `exit` are meta; they run outside the transaction and receive a
`StoreAccess` capability that no other command can reach.

## Resolving a command

Look up the name case-insensitively among the registered definitions.

If there is no match, the implementation **must** resolve the definition named
`UnknownCommand` instead, bind the written name to its first parameter, and execute it.
That command raises `Unknown command : <name>`. Reporting through a command rather than
by throwing directly means an unknown name renders like any other failure.

If `UnknownCommand` is not registered, raise `Unknown command : <name>` directly.

## Argument binding

Given the written arguments, the definition, the piped input and the scope, produce
bound values. An implementation **must** follow this order.

**Step 1: named arguments and flags.** Walk the written arguments left to right.

- A named argument (`name: value`) resolves `name` against parameter names and against
  optional parameters' flags, case-insensitively. No match raises
  `'<command>' has no argument named '<name>'.` Otherwise bind the evaluated value.
- A flag (`-name`) resolves the same way. If the next written argument is a plain value,
  bind it and consume it. Otherwise bind `BooleanValue(true)`.
- Anything else joins the positional queue, in order.

**Step 2: positional and fallback.** For each declared parameter in declaration order,
skipping those already bound:

1. If the positional queue is not empty, bind its next value. Optional parameters take
   part: `progress 20 50` fills two optional parameters positionally.
2. Otherwise, if the parameter accepts piped input and the input is not empty, bind the
   input.
3. Otherwise, if the parameter is optional, bind its default.
4. Otherwise raise `'<command>' needs an argument for '<parameter>'.`

**Step 3: leftovers.** If the positional queue is not empty, raise
`'<command>' takes N arguments, but M were given.` where N is the number of declared
parameters and M is N plus the number left over. The word `argument` is singular when
N is 1.

**Step 4: order.** Return the bound values in declaration order, so a command may index
them positionally.

### Evaluating a written value

| Written | Becomes |
| --- | --- |
| String literal | `TextValue` of the text inside the quotes |
| Identifier or word | `NumberValue` if it parses as a number with the invariant culture, otherwise `TextValue` |
| `$name` | The variable's value, or raise `Unknown variable : $<name>` |
| A tag | The object or component it builds, binding its variable if it names one |
| Anything else | Raise `Unsupported argument value : <node>` |

Number parsing **must** use the invariant culture and allow a leading sign, a decimal
point and an exponent.

## Running a pipeline

```
Execute(tree, output, scope, cancellation):
    if tree is EmptyCommand:            return Empty
    if tree is PipedCommandList:
        if no commands:                 return Empty
        current = Empty
        for expression in commands:
            throw if cancellation requested
            current = ExecuteExpression(expression, current, output, scope, cancellation)
        return current
    otherwise:                          raise "Cannot execute a <node>."
```

Each stage receives the previous stage's value. The last stage's value is the result.

Cancellation **must** be observed between stages, and a stage that throws **must** stop
the pipeline and propagate.

A stage is one of:

- a function expression, executed as a command;
- a command line expression, executed as a command;
- an instance tag, evaluated to a value without calling a command.

### A line is one transaction

The stages of a line share a **working projection**: the committed projection with the
events produced so far in this line folded into it. Each stage reads that, so a later
stage sees an earlier one's effect and `mkdir scratch | cd` lands in the new folder.

Nothing is appended to the log until the whole line has succeeded. Then the accumulated
events are appended as a single transaction whose source is the line as it was written.

An implementation **must**:

- append nothing when any stage fails, so a failed line leaves no trace;
- append nothing when the accumulated events are empty, so a read-only line leaves no
  transaction and `undo` reaches past it;
- keep meta commands out of the transaction, since their business is the log itself.

### Executing one command

```
spec       = resolve(name)
bound      = bind(arguments, spec, input, scope)
invocation = { spec, bound, input, output, scope, working projection, location, blobs, token }
result     = await spec.Run(invocation)
```

An implementation **must** fold the events produced by evaluating the arguments into
the working projection before running the command, so a tag that bound a variable is
visible to it.

An implementation **must** await a command before the next stage, so a pipe carries a
finished value. Keeping a user interface responsive is the host's problem, not the
evaluator's.

An implementation **must** convert an unexpected exception from a command into a fault
of kind `Internal` rather than letting it escape.

## Evaluating tags

| Tag | Result |
| --- | --- |
| `ObjectInstance` | `ObjectValue` with evaluated attributes and object children |
| `ComponentInstance` | `ComponentValue` with evaluated attributes and children |
| `VariableTag` | The named variable's value, or raise `Unknown variable : $<name>` |

Rules:

- Attribute values evaluate as written values, so `$name` resolves against the scope.
- Attribute keys are case-insensitive.
- An object's object children become its children. An object's component children
  **must** be evaluated, for their variable bindings, and are not attached to the
  object. Any other child raises `Cannot evaluate a child <node>.`
- A component's children **must** be instance tags; anything else raises the same error.
- A tag that names a variable binds it after the value is built, and **must** do so by
  producing a `VariableChanged` event rather than by mutating a scope, so the binding is
  committed and undone with the rest of the line. The name is not part of the value.

Property assignments parse but do not evaluate. An implementation **must** raise
`Cannot evaluate a child PropertyAssignment.` rather than ignoring one.

## Variables and scope

A scope holds variables, commands and types, and may have a parent. Lookup walks to the
parent; binding always writes to the scope it is asked to write to, shadowing any
binding of the same name in a parent.

An implementation **must** provide: look up by name, bind, unbind, and enumerate
everything visible with the innermost binding winning.

The terminal uses one global scope per session. Nested scopes are supported by the
model and not yet created by any host.

## The store

The filesystem, the variables and the current location are a projection folded from an
append-only log of transactions. An implementation **must** be able to reach the same
projection by replaying the same log into a fresh store.

Every event carries both sides of its change, so `invert` needs nothing but the event
and is its own inverse. A transaction's inverse is its events inverted **and reversed**,
because a later event can depend on an earlier one having happened.

| Event | Inverse |
| --- | --- |
| `FileCreated r` | `FileDeleted r` |
| `FileDeleted r` | `FileCreated r` |
| `AttributesChanged (id, b, a)` | `AttributesChanged (id, a, b)` |
| `ContentChanged (id, b, a)` | `ContentChanged (id, a, b)` |
| `VariableChanged (n, b, a)` | `VariableChanged (n, a, b)` |
| `LocationChanged (b, a)` | `LocationChanged (a, b)` |

The identity holds for events that describe a change that happened: an event's `before`
side **must** be what the projection holds. Commands build events by reading the
projection, so they cannot produce anything else.

Before appending, an implementation **must** validate the events against the state they
will land on, and **must** reject a `FileCreated` whose name is already used in its
folder with a fault of kind `Conflict`.

## Undo

The log only grows. Undo **must not** remove a transaction; it appends one whose events
are the target's inverted and reversed, marked as compensating the target.

A transaction is **compensated** when some later transaction compensates it and is not
itself compensated. That recursion is what makes undo, redo, undo behave.

| Operation | Target | Result |
| --- | --- | --- |
| `undo` | The latest undoable, non-compensating, uncompensated transaction | Names the line it reversed |
| `redo` | The latest uncompensated **undo** | Names the line the undo had reversed |

A redo is itself a compensation, so "the latest compensation" is the wrong rule for
redo: it finds the redo just appended and reverses it, and pressing redo twice puts a
change back and then takes it away again. An implementation **must** distinguish an
undo, which compensates a line someone ran, from a redo, which compensates an undo.

Neither is a failure when there is nothing to do: an implementation **must** return a
plain result saying so, not a fault, because nothing went wrong.

A transaction may be marked not undoable. The seeded filesystem is
([decision 0018](../decisions/0018-the-seed-is-not-a-line-anyone-typed.md)): it is
recorded and replayed so the store stays a pure fold of its log, and `undo` **must**
skip it while `history` still shows it.

`history` is the transactions oldest first, each with a derived `Undone`. A compensation
**must not** itself be marked undone.

## Cancellation

Cancellation is cooperative. A long-running command **must** observe its invocation's
token and **should** check it at least once per step.

A host cancels by cancelling the token it passed in. A cancelled command **must** return
a fault of kind `Cancelled` with the message `Stopped.`, and the line commits nothing,
like any other failed line.

Cancellation **must** also be observed between stages.

## Faults

Failure is a value. A command returns `Error fault`; no command raises, and no exception
crosses a module boundary.

```
Fault = { Kind; Message; Stage; Path; Cause }
Kind  = Syntax | Binding | UnknownCommand | NotFound | Conflict | Invalid | Cancelled | Internal
```

Every message in the [error reference](../errors.md) is preserved word for word as
`Message`. The kind is additional and **must not** replace it.

The evaluator **must** stamp `Stage` with the one-based position of the failing stage,
and **must not** overwrite a stage already set, so the innermost failure keeps its own
position.

An implementation **must not** let a failure end the session. `Session.Execute` **must
not** raise: a parse failure is `Syntax`, a cancellation is `Cancelled` with the message
`Stopped.`, and an unexpected exception is `Internal` naming the exception's type.
