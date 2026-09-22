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
| `Object` | `TypeName`, `Attributes`, `Order`, `Children` | the tag as written | same |
| `Component` | `TypeName`, `Attributes`, `Order`, `Children` | the tag as written | same |
| `Table` | `Columns`, `Rows` | the header and one line per row, aligned | same |
| `Query` | an `Expr` | the expression as it was written | same |
| `Fault` | a `Fault` | its message | same |

`Empty` means "this command returns nothing"; `None` means "the answer is that there is
nothing". They read alike and are distinct, and an implementation **must** keep them so.

The two string forms **must** differ for files: display gives the name, and the
argument form gives the full path. This is what makes `ls | cd` work with no quoting
rule. Implementations **must not** collapse them.

A `Fault` value is a failure a line kept going past: what `try` turns a stage's failure
into, and what `else` pipes into the branch after it ([Recovery](#recovery)). Its
members are readable like a tag's attributes: `message` and `kind` as `Text` (the kind
as its word, `NotFound` and so on), `path` as `Text` or `None`, `stage` as `Number` or
`None`, and `cause` as a `Fault` or `None`. A name it does not have is `None`.

A tag's `Order` is its attribute names in the order they were written. Attributes
**must** be written out in that order, with any the order does not name after them, so
that a tag reads back the way it was typed and a row built from a table reads in column
order.

### Displaying a table

A table's display string **must** be the column names, then one line per row, each cell
padded to the width of the widest thing in its column and separated by two spaces, with
no trailing padding on a line.

A cell **must** occupy one line. A cell holding a table **must** read as
`<n> row` or `<n> rows`, and a cell holding text with a line break in it **must** have
the breaks folded to spaces. The same rule applies to a tag's attribute values, because
the tag notation is one line as well.

### Tables

```
ColumnType ::= text | number | boolean | file | object | mixed
Column     ::= { Name, Type }
Table      ::= { Columns: Column list, Rows: Value list list }
```

Every row **must** have exactly as many cells as there are columns. A missing cell is
`None`; an implementation **must not** represent it as a short row.

A column's type is read off its cells, not declared. It is the one type every cell that
is not `None` has, `mixed` when they disagree, and `text` when there is nothing to go
on. Cells that are `None` **must not** affect it.

### Reading a tag as a table

A tag is *table-shaped* when every child is a tag, every child has the same type name,
and no child has children of its own. A table-shaped tag **must** read as a table
wherever a table is expected. Its columns are the union of the children's attribute
names in the order they first appear; each row is that child's values, with `None`
where it has none.

A tag that is not table-shaped **must** produce a fault of kind `invalid` naming the
first child that broke the shape and why.

### Comparison

Two values compare with one of `eq ne gt ge lt le like has`.

- If either value is absent (`Empty` or `None`), the comparison is `false`, except that
  `eq` on two absent values is `true`.
- `eq`, `ne`, `gt`, `ge`, `lt` and `le` order the two values. When both read as numbers
  with the invariant culture — including text that does — the ordering is numeric.
  Otherwise it is an ordinal comparison of the display strings.
- `like` is `true` when the right value's display string occurs in the left's, ignoring
  case; when the right contains `*`, it is a whole-string match with `*` standing for
  anything, ignoring case.
- `has` is `true` when the left is a `List` containing the right, a `Table` with a cell
  equal to it, a tag with an attribute equal to it, or text containing it.

Equality throughout is equality of display strings, so a `Number` 1 and the text `1`
are `eq`, and a `Boolean` is `eq` to `true` or `false` written as a word.

The same ordering **must** drive `sort`, so a column that compares as numbers also
sorts as numbers. An absent value orders before everything.

`and`, `or` and `not` combine expressions. Only `Boolean true` is true; every other
value, absent or not, is false. `and` and `or` **must** short-circuit.

## The command model

| Type | Purpose |
| --- | --- |
| `CommandSpec` | `Name`, `Description`, `Keywords`, `Parameters`, `Meta`, `ReadOnly`. |
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

`ReadOnly` marks a command that can only ever read, and is what a refresh is allowed to
re-run (see [Refreshing](#refreshing)). It **must** be declared rather than inferred: a
command that writes under some arguments and not others — `attr` is the one — **must
not** be marked.

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

1. If the parameter collects the rest, bind a `List` of every remaining positional
   value and empty the queue. It **must not** take the piped input.
2. Otherwise, if the positional queue is not empty, bind its next value. Optional
   parameters take part: `progress 20 50` fills two optional parameters positionally.
3. Otherwise, if the parameter accepts piped input and the input is not empty, bind the
   input.
4. Otherwise, if the parameter is optional, bind its default.
5. Otherwise raise `'<command>' needs an argument for '<parameter>'.`

A command **may** declare at most one parameter that collects the rest, and it **must**
be the last one that can take a positional argument.

**Step 3: leftovers.** If the positional queue is not empty, raise
`'<command>' takes N arguments, but M were given.` where N is the number of declared
parameters and M is N plus the number left over. The word `argument` is singular when
N is 1. A command that declares a parameter collecting the rest can never reach this
step, and **must** report its own arity in its own words.

**Step 4: order.** Return the bound values in declaration order, so a command may index
them positionally.

### Evaluating a written value

| Written | Becomes |
| --- | --- |
| String literal | `TextValue` of the text inside the quotes |
| Identifier or word | `NumberValue` if it parses as a number with the invariant culture, otherwise `TextValue` |
| `$name` | The variable's value, or raise `Unknown variable : $<name>` |
| `$name.member` | The member read off the variable's value: an attribute of a tag, `name`, `kind`, `folder`, `path` or `id` of a file, or `message`, `kind`, `path`, `stage` or `cause` of a fault. A member that is not there is `None`, not a fault. |
| `( pipeline )` | The value the nested pipeline answered. See [Nested pipelines](#nested-pipelines). |
| A tag | The object or component it builds, binding its variable if it names one |
| An expression | Bound only to a parameter declared as a predicate, unevaluated, as a `Query`. For any other parameter, raise `'<command>' takes a value for '<parameter>', not an expression.` |
| Anything else | Raise `Unsupported argument value : <node>` |

Number parsing **must** use the invariant culture and allow a leading sign, a decimal
point and an exponent.

A written argument counts as an expression only when it actually wrote an operator. A
bare operand **must** bind as the value it is, whatever the parameter's kind.

### Predicates

A parameter declared as a predicate receives the expression unevaluated. The command
evaluates it once per item, in a child scope with `$row` bound to that item, so a
variable called `row` outside the predicate **must** be unaffected.

A nested pipeline inside a predicate is the exception: it has already run, and the
predicate holds the value it answered as a constant. Every row is compared against that
one value, and a predicate kept as a view keeps the value, not the pipeline.

## Running a pipeline

```
Execute(tree, output, scope, cancellation):
    if tree is EmptyCommand:            return Empty
    if tree is PipedCommandList:        return Branches([tree])
    if tree is RecoveryLine:            return Branches(tree.Pipelines)
    otherwise:                          raise "Cannot evaluate a <node>."

Pipeline(input, stages):
    current = input
    for stage, n in stages:
        fail Cancelled if cancellation requested
        current = Stage(n, current, stage)     -- a fault here is stamped with stage n
    return current
```

Each stage receives the previous stage's value. The last stage's value is the result.
A line's first pipeline is given `Empty` as its input.

Cancellation **must** be observed between stages, and a stage that throws **must** stop
the pipeline and propagate.

A stage is one of:

- a function expression, executed as a command;
- a command line expression, executed as a command;
- an instance tag, evaluated to a value without calling a command;
- a pipeline in parentheses, run as a pipeline with the stage's input as its input.

### Recovery

Decisions [0014](../decisions/0014-recovery-operator.md) and
[0024](../decisions/0024-stop-is-not-recoverable.md). A **recoverable** fault is one of
any kind but `Cancelled`.

```
Branches(pipelines):
    start = the working projection and the pending events
    result = Pipeline(Empty, pipelines[0])
    for next in pipelines[1..]:
        if result is Ok, or its fault is not recoverable:  stop
        restore start
        result = Pipeline(Fault(fault), next)
    return result

Stage(n, input, stage):
    before = the working projection and the pending events
    result = run the command expression with input
    if stage has "try" and result is Error f and f is recoverable:
        restore before
        result = Ok(Fault(f stamped with stage n))
    if stage has a default and result is Ok v and v is Empty or None:
        result = evaluate the default
    return result
```

An implementation **must**:

- run a pipeline after `else` only when the one before it failed with a recoverable
  fault, with that fault as its input;
- restore the working projection and the pending events before it does, so the events
  that commit are those of the branch that produced the value and no other;
- when every branch fails, fail the line with the last branch's fault;
- on a failed `try` stage, discard what the stage recorded, and hand the next stage a
  `Fault` value stamped with the stage's position;
- evaluate a default only when the stage answered `Empty` or `None`. A `Fault` value is
  an answer: `try x ?? y` keeps the fault;
- never recover from `Cancelled`, with `try` or with `else`.

### Nested pipelines

A pipeline in parentheses in operand position — an argument, either side of a
comparison, under `and`, `or` or `not`, or a default — **must** be run before the
stage's arguments are bound, in the order written, with `Empty` as its input, in the
line's working projection. Its value is what the binder sees in its place, and its
events join the line's transaction. It **must** run once per evaluation of the stage it
is written in, not once per row of a predicate.

A fault from inside a nested pipeline fails the stage it is written in, keeping its
message and kind; the stage number is the outer stage's.

A nested pipeline reached where no line is running — in the text of a view file that
was written by hand — **must** fail with `A pipeline in parentheses only runs as part of
a line : (...)` when evaluated.

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

## Location

```
Location = { Folder: string; View: Expr option }
```

`Folder` is the absolute path of the current directory, `/` for the root. `View` is a
predicate, and when it is set the session is *in* that query.

The two are independent, and an implementation **must** keep them so:

- A view **must not** change `Folder`. A record created while a view is set **must**
  be created in `Folder`.
- `ls` with a view set and no path written **must** list every record in the store the
  view matches, across folders. With a path written it **must** list that folder and
  leave the view set.
- `pwd` **must** answer `Query` when a view is set and `Text` of `Folder` otherwise.
- `up` **must** clear the view when one is set, and move to the parent otherwise, so
  two `up`s leave a view over a subfolder in the order they were entered.
- Both changes travel as `LocationChanged`, so undo restores the whole location.

A view's predicate is evaluated once per candidate record, in a child scope with `$row`
bound to a row of the same shape `ls` produces — so `$row.kind`, `$row.folder`, `$row.size`
and any attribute the record carries all mean in a view what they mean after `ls |`.

## Refreshing

An implementation **may** offer a way to re-run a line without committing it, so a host
can keep a listing on screen up to date as the store changes. Where it does:

- Every stage of the line **must** name a registered command whose spec is `ReadOnly`.
  That includes every branch after `else`, every nested pipeline, every stage in
  parentheses and every default, and an instance tag standing as a stage is not
  read-only. A line that fails this **must** be refused with a fault of kind `Invalid`,
  *before* any of it runs. An unregistered name is not read-only.
- The run **must** commit nothing, whatever events it gathers, and **must** leave the
  history and the undo chain untouched.

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

## Starting and starting over

A session replays its log before it will run anything, and **must** refuse to execute
before that has completed with a fault of kind `Internal`. An empty filesystem and a
lost one look identical to a user, so a host that forgets to wait is told rather than
shown nothing.

Seeding happens **only** when the replayed log is empty, or a reload would lay a second
copy of the seed over what was restored. The seed is one transaction, marked not
undoable.

`reset` empties the log and seeds it again. It is the only operation that removes
anything from the log, and it is not undoable: the transactions that would have been
reversed are the ones it threw away. An implementation **must** say so in the command's
description, so `help` warns before rather than after.

## Cancellation

Cancellation is cooperative. A long-running command **must** observe its invocation's
token and **should** check it at least once per step.

A host cancels by cancelling the token it passed in. A cancelled command **must** return
a fault of kind `Cancelled` with the message `Stopped.`, and the line commits nothing,
like any other failed line. Neither `try` nor `else` recovers from it.

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
position. A fault leaving a nested pipeline **must** have its stage cleared first, so
that the stage reported is the one in the line that was written.

A fault is also a value: see [Recovery](#recovery). The kind's word, as `$f.kind` reads
it and a host carries it, is the case name exactly: `Syntax`, `Binding`,
`UnknownCommand`, `NotFound`, `Conflict`, `Invalid`, `Cancelled`, `Internal`.

An implementation **must not** let a failure end the session. `Session.Execute` **must
not** raise: a parse failure is `Syntax`, a cancellation is `Cancelled` with the message
`Stopped.`, and an unexpected exception is `Internal` naming the exception's type.
