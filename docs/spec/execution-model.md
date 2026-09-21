# Execution model

Normative semantics: the value model, how arguments bind, how a pipeline runs, how tags
evaluate, and what undo and cancellation guarantee.

Reference implementation: `CommandLine/Execution` (assembly `Terminal`).

## Runtime values

A command returns a `RuntimeValue`. Every value answers two questions: how it reads to
a person, and what it means as an argument to another command.

| Value | Carries | Display string | Argument string |
| --- | --- | --- | --- |
| `EmptyValue` | nothing | empty | empty |
| `TextValue` | `Text` | the text | the text |
| `NumberValue` | `Number` (double) | `0.###`, invariant culture | same |
| `BooleanValue` | `Boolean` | `true` or `false` | same |
| `PathValue` | `Path`, `Kind` | the entry name, plus `\` for a directory | the full path |
| `ListValue` | `Items` | items' display strings, space separated | same |
| `ObjectValue` | `TypeName`, `Attributes`, `Children` | the tag as written | same |
| `ComponentValue` | `TypeName`, `Attributes`, `Children` | the tag as written | same |

`PathKind` is `File`, `Directory` or `Parent`. A `Parent` path displays as `up`.

The two string forms **must** differ for paths: display gives the entry's name, and the
argument form gives the full path. This is what makes `ls | cd` work with no quoting
rule. Implementations **must not** collapse them.

`ObjectValue.Attributes` and `ComponentValue.Attributes` are keyed case-insensitively.
`ObjectValue.Children` holds objects; `ComponentValue.Children` holds any value.

`RuntimeValue.Empty` is a shared `EmptyValue`. A command with nothing to return
**must** return it rather than null.

## The command model

| Type | Purpose |
| --- | --- |
| `CommandDefinition` | `Name`, `Description`, `KeyWords`, `Parameters`, `CommandActionType`. |
| `CommandParameter` | `Name`, `Description`, `IsOptional`, `AcceptsPipedInput`. |
| `OptionalCommandParameter` | Adds `Flag` and `Default`; `IsOptional` is true. |
| `CommandActionSync` | `Invoke(CommandInvocation) → RuntimeValue`, `InvokeUndo(CommandInvocation)`. |
| `CommandActionAsync` | `BeginInvoke → Task<RuntimeValue>`, `EndInvoke`, `FailedInvoke`, `BeginInvokeUndo`. |
| `CommandInvocation` | `Definition`, `Arguments`, `Input`, `Output`, `Scope`, `Cancellation`. |
| `CommandParameterValue` | A parameter paired with the bound value; `Text` is the value's argument string. |

A command **must** declare its parameters in the order positional arguments fill them.

An implementation **must** create a command instance per execution. Commands keep undo
state in their own fields, so a shared instance makes the second undo of the same
command replay the first one's saved state.

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

### Executing one command

```
definition = resolve(name)
bound      = bind(arguments, definition, input, scope)
action     = service provider resolves definition.CommandActionType
invocation = new CommandInvocation(definition, bound, input, output, scope, cancellation)
```

For a synchronous action: register the invocation on the history, then `Invoke`.

For an asynchronous action: register the invocation, create a cancellation source
linked to the pipeline's token, call `BeginInvoke` and await it. On success call
`EndInvoke` and return the value. If the task cancels or faults, call `FailedInvoke`
and rethrow. Clear the source and the task afterwards either way.

An implementation **must** register the invocation before running it, so a command that
fails part-way can still be undone.

An implementation **must** await an asynchronous command before the next stage, so a
pipe carries a finished value. Keeping a user interface responsive is the host's
problem, not the evaluator's.

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
- A tag that names a variable binds it in the scope after the value is built. The name
  is not part of the value.

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

## Undo

Every executed command is pushed onto a history stack with its invocation. Undo pops
one entry and reverses it, and **must** return the name of the command it reversed, or
nothing when the stack is empty.

| Action kind | First undo | Second undo |
| --- | --- | --- |
| Synchronous | `InvokeUndo`, then clear the command's output | — |
| Asynchronous, still running | Cancel it, then `BeginInvokeUndo`; the entry stays on the stack | Clear the command's output |
| Asynchronous, finished | `BeginInvokeUndo`; the entry stays on the stack | Clear the command's output |

Clearing output applies only when the output sink can withdraw what it wrote.

Guarantees an implementation **must** provide:

- Undo is per invocation. Undoing two invocations of one command unwinds both.
- Undo is one command at a time. Commands that changed nothing are on the stack and
  undoing one **must** succeed and change nothing.
- A command's undo sees the arguments, scope and output of the invocation being
  reversed.

Commands **should** reverse only what they did: restore previous file contents, recreate
a deleted entry, return to the previous directory, restore a previous binding, or unbind
a name that was new.

## Cancellation

Cancellation is cooperative. A long-running command **must** observe its invocation's
token and **should** check it at least once per step.

A host cancels by cancelling the token it passed in. A cancelled command's task is
cancelled, `FailedInvoke` runs, and the exception propagates to the caller, which
**should** report it as a stop rather than as a failure.

## Errors

A command reports a problem by raising `ConsoleError` with a message for the user. The
evaluator does not catch it; the host does, at the boundary where it builds a response.

An implementation **must not** let a command failure end the session, and **should**
report an unexpected exception with its type so a defect is visible rather than silent.
