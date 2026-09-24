# Execution model

Normative semantics: the value model, how arguments bind, how a line runs and commits,
how tags evaluate, what undo, redo and history guarantee, how scripts run, and how
cancellation behaves.

Reference implementation: `Core` (assembly `CommandLineReimagined.Core`), in F#
([decision 0006](../decisions/0006-functional-core-in-fsharp.md)). The files named
below are in that project.

## Values

A command returns a `Value` (`Values.fs`). Every value answers two questions: how it
reads to a person, and what it means as an argument to another command.

| Value | Carries | Display string | Argument string |
| --- | --- | --- | --- |
| `Empty` | nothing | empty | empty |
| `None` | nothing | empty | empty |
| `Text` | the text | the text | the text |
| `Number` (double) | the number | `0.###`, invariant culture | same |
| `Boolean` | the flag | `true` or `false` | same |
| `File` | `Id`, `Name`, `Kind`, `Folder` | the name | the full path, `folder/name` |
| `List` | items | items' display strings, space separated | same |
| `Object` | `TypeName`, `Attributes`, `Order`, `Children` | the tag, on one line | same |
| `Component` | `TypeName`, `Attributes`, `Order`, `Children` | the tag, on one line | same |
| `Table` | `Columns`, `Rows` | the header and one line per row, aligned | same |
| `Query` | an `Expr` | the expression as it was written | same |
| `Fault` | a `Fault` | its message | same |

`Empty` means "this command returns nothing"; `None` means "the answer is that there is
nothing". They read alike and are distinct, and an implementation **must** keep them so.

The two string forms **must** differ for files: display gives the name, and the
argument form gives the full path. This is what makes `mkdir scratch | in` land in the
new folder, and `read $row.name` read the right file from a row of a listing taken
elsewhere, with no quoting rule. Implementations **must not** collapse them. Only a
`File` value argues differently from how it displays: a list, a tag or a table argues
its display string, so `ls | in` hands `in` the whole listing as text and fails.

A third form, the *data* string, is what a value becomes as text inside a file that is
meant to be read back (`to-csv`, `to-xml`). It is the display string except for a
number, which is written in its shortest exact form rather than rounded to three
places, so writing a table out and reading it in again does not change it.

An object or component displays as the tag notation: `<type a=1 b=two/>`, or with
children `<type a=1><child/></type>`, braces for a component. Attributes are written as
`name=value` in order (see below), with each value on one line as a
[table cell](#displaying-a-table) is.

A `Fault` value is a failure a line kept going past: what `try` turns a stage's failure
into, and what `else` pipes into the branch after it ([Recovery](#recovery)). Its
members are readable like a tag's attributes: `message` and `kind` as `Text` (the kind
as its word, `NotFound` and so on), `path` as `Text` or `None`, `stage` as `Number` or
`None`, and `cause` as a `Fault` or `None`. A name it does not have is `None`.

A tag's `Order` is its attribute names in the order they were written. Attributes
**must** be written out in that order, with any the order does not name after them in
name order, so that a tag reads back the way it was typed and a row built from a table
reads in column order.

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

A column's type is read off its cells, not declared. A `Number` cell is `number`, a
`Boolean` is `boolean`, a `File` is `file`, an object or component is `object`, and any
other present value is `text`. The column's type is the one type every cell that is not
`Empty` or `None` has, `mixed` when they disagree, and `text` when there is nothing to
go on. Absent cells **must not** affect it.

The type describes the column; it does not drive comparison. Comparing and sorting go
value by value, by the rule in [Comparison](#comparison), so a `mixed` column still
sorts its numbers numerically among themselves.

### Reading a tag as a table

A tag is *table-shaped* when every child is a tag, every child has the same type name,
and no child has children of its own
([decision 0009](../decisions/0009-table-coercion.md)). A table-shaped tag, object or
component, **must** read as a table wherever a table is expected. So **must** a list
whose items are all tags of one type with no children; an empty list reads as the empty
table. The columns are the union of the children's attribute names in the order they
first appear; each row is that child's values, with `None` where it has none.

A tag or list that is not table-shaped **must** produce a fault of kind `Invalid` naming
the first child that broke the shape and why, counting children from one:

```
<list> is not a table: child 2 is <j> where the first is <i>.
<list> is not a table: child 1 has children of its own.
the list is not a table: child 1 is text, not a tag.
```

Any other value where a table is expected is a `Binding` fault naming its kind:
`'columns' needs a table, not text.`

A document read by `from-xml` is a tag, so the same rule reads it as a table; nothing
about a tag's origin changes how it coerces. An element's text content arrives as an
attribute named `text`
([decision 0025](../decisions/0025-xml-text-content.md)). A CSV file read by `from-csv`
is a table already. See [Documents](command-catalogue.md#documents).

A row goes the other way as an object of type `row` whose attributes are the row's
cells in column order, with absent cells left out. That is what `$row` is in a
predicate over a table, and what `first` and `last` answer.

### Comparison

Two values compare with one of `eq ne gt ge lt le like has` (`Expr.fs`).

- If either value is absent (`Empty` or `None`), the comparison is `false`, except that
  `eq` on two absent values is `true`. That includes `ne`: a gap is not unequal to 100
  any more than it is equal to it.
- `eq`, `ne`, `gt`, `ge`, `lt` and `le` order the two values. When both read as numbers
  — a `Number`, or `Text` that parses as one with the invariant culture — the ordering
  is numeric. Otherwise it is an ordinal comparison of the display strings.
- `like` is `true` when the right value's display string occurs in the left's, ignoring
  case; when the right contains `*`, it is a whole-string match with `*` standing for
  anything, ignoring case.
- `has` is `true` when the left is a `List` with an item whose display string equals the
  right's, a `Table` with such a cell, or a tag with such an attribute value. For any
  other left value, it is `true` when the left's display string contains the right's,
  with case significant.

So `eq` is numeric equality when both sides are numbers, and `1`, `1.0` and the text
`01` are all `eq` to one another; otherwise it is ordinal equality of display strings,
so a `Boolean` is `eq` to `true` or `false` written as a word and a `File` is `eq` to
its name.

The same ordering **must** drive `sort`, so a column that compares as numbers also
sorts as numbers. An absent value orders before everything.

`and`, `or` and `not` combine expressions. Inside them, only `Boolean true` is true;
every other value, absent or not, is false, and none of them is a fault. `and` and `or`
**must** short-circuit, so an unknown variable on the right of a false `and` is never
looked up. What a whole predicate's value must be is a rule of its own
([Predicates](#predicates)).

A tag cannot be an operand: `where <t/> eq 1` is a `Binding` fault,
`A TagValue cannot be part of an expression.`

## The command model

The contract is in `Commands/Spec.fs`.

| Type | Purpose |
| --- | --- |
| `CommandSpec` | `Name`, `Description`, `Keywords`, `Parameters`, `Meta`, `ReadOnly`. |
| `Parameter` | `Name`, `Description`, `Optional`, `Flag`, `Default`, `AcceptsPipe`, `Kind`, `Takes`. |
| `ParamKind` | `Single`, `Predicate`, `Rest`, `Assignments`. |
| `Invocation` | `Spec`, `Args`, `Assignments`, `Input`, `Output`, `Scope`, `Projection`, `Location`, `Blobs`, `Cancel`. |
| `CommandResult` | `Value` for the next stage, and `Events` describing what changed. |
| `Command` | A `CommandSpec` and `Run: Invocation -> Async<Outcome<CommandResult>>`. |

A parameter's kind says how an argument reaches it. `Single` takes one value.
`Predicate` takes an expression unevaluated ([Predicates](#predicates)). `Rest` takes
every remaining positional argument as a `List`
([decision 0021](../decisions/0021-variadic-parameters.md)). `Assignments` takes every
`name=value` on the line, in order
([decision 0017](../decisions/0017-assignment-arguments.md)); it is filled by that
notation alone and takes no positional argument.

A command **must** declare its parameters in the order positional arguments fill them,
and **should** let at most one accept the pipe, or a pipe becomes ambiguous.

`Takes` says what a parameter's argument is: `Anything`, the default, `Path`, `Place`,
`NewName`, `Url`, `Column`, `Count`, `Number`, `Text`, `Switch(on, off)`,
`VariableName`, `CommandName` or `Value`. It is for completion, the signature hint and
`help <command>`, and binding **must not** read it: two commands that differ only in
what their parameters take bind alike. What each one offers is in
[Host interfaces](host-interfaces.md#what-a-parameter-takes).

`Output` is where a command writes while it is still running, such as a progress
figure; its result is its return value. `Blobs` lets a command put and get file content
by hash, and nothing else of the log.

**A command does not change anything.** It reads `Projection` and `Location`, returns a
value and a list of events, and the evaluator commits them
([decision 0010](../decisions/0010-undo-by-event-sourcing.md)). An implementation **must
not** give a command a way to append to the log. Because of this, running a command
twice against the same projection **must** produce the same result, and there is no
per-command undo state for a second invocation to replay.

`Meta` marks a command as being about the log or the session rather than about the
world: `undo`, `redo`, `history`, `help`, `reset`, `run`, `exit`, and the internal
`UnknownCommand`. Meta commands run outside the transaction
([decision 0015](../decisions/0015-atomic-lines.md)): whatever events one returns are
not the line's to commit. `undo`, `redo`, `history`, `reset`, `run` and `exit` are
built with a `StoreAccess` capability — undo, redo, history, reset, run a line, exit —
that no other command can reach.

`ReadOnly` marks a command that can only ever read, and is what a refresh is allowed to
re-run (see [Refreshing](#refreshing)). It **must** be declared rather than inferred. A
command that emits events under any arguments **must not** be marked: `attr`, which
writes with assignments and reads without, is not, and neither is a command that only
moves the location, such as `in`, `out` or `back`, because `LocationChanged` is an
event.

## Resolving a command

Look up the name case-insensitively among the registered definitions, so `ECHO hi` runs
`echo`.

If there is no match, the implementation **must** resolve the definition named
`UnknownCommand` instead, bind the written name to its first parameter and the nearest
command names to the rest, and execute it with no pipe input. That command fails with
kind `UnknownCommand` and the message `Unknown command : <name>`, followed, when any
command name is near, by the nearest names, at most three:

```
Unknown command : lss. Did you mean ls?
Unknown command : rum. Did you mean rm or run?
Unknown command : rn. Did you mean in, rm or run?
```

Reporting through a command rather than directly means an unknown name renders like
any other failure. `UnknownCommand` is not listed by `help`, and **must not** itself be
resolvable by name: typing `UnknownCommand` is an unknown command,
`Unknown command : UnknownCommand`.

The nearest names are found in two ways, the first before the second (`Nearest.fs`,
`Nearest.commands`), over every registered command but `UnknownCommand`:

1. **By keyword.** A written name that is one of a command's keywords was not mistyped:
   it says what the command does, or what it was called before
   ([decision 0037](../decisions/0037-in-out-back-and-read.md)). A name of three letters
   or more is a keyword of every command that has a keyword equal to it, ignoring case.
   A shorter one is a keyword of a command only when it equals that command's *first*
   keyword and no other command has it among its keywords, because a short word shared
   by two commands describes them rather than naming either (`Nearest.keywordOf`).
   When the name is a keyword of any command, those commands are the nearest, in name
   order, and slips **must not** be looked for.
2. **By slip.** Otherwise a name is near a command's name when the edit distance between
   them, ignoring case, is at least one and at most one for a written name of up to four
   letters, or two for a longer one. The distance counts an insertion, a deletion, a
   substitution and a swap of two neighbouring letters as one each (Damerau–Levenshtein,
   optimal string alignment), so `sotr` is one from `sort`. The nearest come first,
   equally near ones in name order.

```
Unknown command : cd. Did you mean in?
Unknown command : cat. Did you mean read?
Unknown command : delete. Did you mean rm?
Unknown command : navigate. Did you mean back, in or out?
Unknown command : by
Unknown command : as. Did you mean ls?
```

`cd` is `in`'s first keyword and nobody else's, so it names `in`, although it is one slip
from `cp`. `by` is `group`'s first keyword and one of `sort`'s as well, so it names
neither, and nothing is one slip from it. `as` is a keyword of `table` but not its first,
so it is corrected as a slip. The same nearest names answer `help` for a name that is not
a command, and completion finds a command by keyword by the same rule for a word of one
or two letters ([Host interfaces](host-interfaces.md#reading-the-place)).

If `UnknownCommand` is not registered, fail with the same message directly.

A hyphenated name is one name: `ls-l` is an unknown command, not `ls` with a flag
([decision 0022](../decisions/0022-hyphenated-command-names.md)).

## Argument binding

Given the written arguments, the definition, the piped input and the scope, produce the
bound arguments (`Binder.fs`). An implementation **must** follow this order. Every
failure here is a fault of kind `Binding` unless it says otherwise.

**Step 1: named arguments, flags and assignments.** Walk the written arguments left to
right.

- A named argument (`name: value`, written in the function form: `take(count: 2)`)
  resolves `name` against parameter names and against parameters' flags,
  case-insensitively. No match fails with
  `'<command>' has no argument named '<name>'.` Otherwise bind the value, by the
  parameter's kind.
- A flag (`-name`) resolves the same way, with the same failure. If the next written
  argument is a plain value — not a flag, a named argument or an assignment — bind it
  to the flag's parameter and consume it. Otherwise bind `Boolean true`.
- An assignment (`name=value`) is collected, in order, with its value evaluated, when
  the command declares an `Assignments` parameter. When it declares none, fail with
  `'<command>' does not take '<name>=' assignments.`
- Anything else joins the positional queue, in order.

**Step 2: positional and fallback.** For each declared parameter except the
`Assignments` one, in declaration order, skipping those already bound:

1. If the parameter is `Rest`, bind a `List` of every remaining positional value,
   possibly empty, and empty the queue. It **must not** take the piped input.
2. Otherwise, if the positional queue is not empty, bind its next value. Optional
   parameters take part: `progress 20 50` fills two optional parameters positionally.
3. Otherwise, if the parameter accepts piped input and the input is neither `Empty` nor
   `None`, bind the input.
4. Otherwise, if the parameter is optional, bind its default.
5. Otherwise fail with `'<command>' needs an argument for '<parameter>'.`

A command **may** declare at most one `Rest` parameter, and it **must** be the last one
that can take a positional argument.

**Step 3: leftovers.** If the positional queue is not empty, fail with
`'<command>' takes N arguments, but M were given.` (`but 1 was given` for one) where N is the number of parameters
step 2 considered and M is N plus the number left over. The word `argument` is singular
when N is 1. A command that declares a `Rest` parameter can never reach this step, and
**must** report its own arity in its own words.

**Step 4: result.** Return the bound values by parameter name, the assignments in the
order written, and the events evaluating the arguments produced.

Evaluating an argument can bind a variable (a named tag), so the scope is threaded
through the walk: a variable a tag binds is visible to the arguments evaluated after
it, and its `VariableChanged` event is part of what binding returns.

### A wrong call carries its help

Decision [0038](../decisions/0038-a-wrong-call-shows-its-help.md). A line that fails
because it called a command wrongly fails with the same fault as before, and its
response **must** also carry that command's help as its `Guide` (`Session.Response`).
Every other response **must** carry none.

A line called a command wrongly when all of these hold (`Session.calledWrongly`):

- the line was executed, not refreshed: a [refresh](#refreshing) carries no guide;
- the line failed, with a fault that carries a stage number, of kind `Binding`, or of
  kind `Invalid` when its message begins with the name of one of that command's
  parameters in quotes followed by a space, as `'count' must be a whole number, not 'x'.`
  does: a value of the wrong kind for a parameter, refused by the command rather than
  the binder;
- that stage, in the line's pipeline, or in its last pipeline when it has `else`
  branches, since the fault is then the last branch's, is a command written in the
  function or the command-line form, not a tag, a value stage or a pipeline in
  parentheses;
- the stage names a registered command, ignoring case;
- for a `Binding` fault, its message begins with that command's name in quotes,
  `'sort'`, compared ordinally, or it is the fault of a predicate that never reads `$row`
  and the command has a `Predicate` parameter.

The last test keeps out a binding fault that the stage carries but did not make. A
fault from inside a pipeline in parentheses names the inner command, and one from a line
of a script `run` ran begins with the script's path, so neither carries a guide:
`echo (read) | count` fails with `'read' needs an argument for 'path'.` and no guide. A
line that `try` or `else` recovered did not fail, and carries none. An unknown name is
not of kind `Binding`, and nor is a fault raised while a command runs over what it was
given, such as `File does not exist : /missing.txt`. A binding fault a command raises
about its own arguments is a wrong call when its message begins with the command's
name, as `'select' needs at least one column.` and
`'find' needs a predicate, such as $row.kind eq note.` do, and so is a predicate that
never reads `$row`, `kind eq folder never reads $row, so it is the same for every row.`,
whose own sentence names the fix while the help says the rest.

The guide **must** be what `help <command>` answers, asked the way a refresh asks, so
that it cannot drift from `help`, commits nothing and writes nothing: a `List` of the
lines `help` wrote to its output, each as `Text`, followed by the table it answered. For
`read`, whose wrong call fails with `'read' needs an argument for 'path'.`, the guide
holds the text and the table of

```
$ help read
Show what a file says
name  required  piped  takes   description
path  true      true   a path  The file to read
```

For `help where extra`, the command called wrongly is `help`, so the guide is `help`'s
own help. A host carries the guide as `guide` on the wire
([Execution response](host-interfaces.md#execution-response)).

### Evaluating a written value

| Written | Becomes |
| --- | --- |
| String literal | `Text` of the string's value |
| Identifier or word | `Number` if it parses as a number with the invariant culture, otherwise `Text` |
| `$name` | The variable's value, or fail with `Unknown variable: $<name>`, kind `NotFound`, path `$<name>`. `$row` outside a predicate fails differently; see [The row outside a predicate](#the-row-outside-a-predicate). |
| `$name.member` | The member read off the variable's value, and so on for each member written. See below. |
| `( pipeline )` | The value the nested pipeline answered. See [Nested pipelines](#nested-pipelines). |
| A tag | The object or component it builds, binding its variable if it names one. See [Evaluating tags](#evaluating-tags). |
| An expression | Bound only to a parameter declared `Predicate`, unevaluated, as a `Query`. For any other parameter, fail with `'<command>' takes a value for '<parameter>', not an expression.` |
| Anything else | Fail with `Unsupported argument value : <node>`, kind `Internal` |

A member is read off a value like this: from an object or component, the attribute of
that name, matched exactly and with case significant; from a `File`, `name`, `kind`,
`folder`, `path` or `id`; from a `Fault`, `message`, `kind`, `path`, `stage` or
`cause`. A member that is not there is `None`, not a fault, and a member of any other
value is `None`.

Number parsing — of a word here, and of text in a [comparison](#comparison) — **must**
use the invariant culture and allow surrounding white space, a leading sign, a decimal
point and an exponent. The reference implementation also
accepts the invariant culture's names for the special values, so `NaN` and `Infinity`
are numbers.

A written argument counts as an expression only when it actually wrote an operator. A
bare operand **must** bind as the value it is, whatever the parameter's kind.

### Predicates

A parameter declared as a predicate receives the expression unevaluated, as a `Query`.
The command evaluates it once per item, in a child scope with `$row` bound to that item
([decision 0008](../decisions/0008-explicit-row-variable.md)), so a variable called
`row` outside the predicate **must** be unaffected. Over a table, the item is the row as
an object of type `row` (see [Reading a tag as a table](#reading-a-tag-as-a-table)).

A nested pipeline inside a predicate is the exception to "unevaluated": it has already
run, and the predicate holds the value it answered as a constant. Every row is compared
against that one value, and a predicate kept as a view keeps the value, not the
pipeline.

A predicate is a yes-or-no question about the row
([decision 0033](../decisions/0033-a-predicate-is-a-question-about-the-row.md)). Two
checks hold it to that, one when it is bound and one when it runs.

**Bound: it must read the row.** When the binder hands an argument to a *predicate*
parameter, and the argument wrote an operator, the expression **must** read `$row`
somewhere, or the stage fails with a `Binding` fault before anything runs. A `$row`
inside a nested pipeline does not count: that pipeline runs once for the line, not once
per row. The check is made on the expression as written, before any nested pipeline's
value is put in its place, so the message quotes what was typed:

```
kind eq folder never reads $row, so it is the same for every row. Did you mean $row.kind eq folder?
3 lt size never reads $row, so it is the same for every row. Did you mean 3 lt $row.size?
not done never reads $row, so it is the same for every row. Did you mean not $row.done?
$v eq 5 never reads $row, so it is the same for every row.
```

The suggestion reads the bare words the predicate compared as columns: in a comparison,
its left side when that is a word, and otherwise its right; under `and`, `or` and
`not`, an operand that is a word on its own. A word here is a letter or `_` followed by
letters, digits and `_`. When nothing can be read that way, the sentence ends after
`every row.`.

Because the check is where every *predicate* parameter is bound, `where`, `find`, `in`
and `save-view` all make it. Going `in` a saved view **must** make it too, on the
predicate read back from the view's record, so a view whose text was written over with
`kind eq folder` is refused with the same fault rather than listing nothing. An
argument with no operator in it is not checked, so `in documents` is still a path
([decision 0013](../decisions/0013-attribute-filesystem.md)) and `where $flag` is still
a question.

**Run: it must answer true or false.** A filter — `where`, `find`, a view — tests each
item in order, and what the whole predicate answers for it decides:

- `Boolean true` keeps the item, and `Boolean false` drops it.
- An absent value, `Empty` or `None`, drops it, and is not a fault. A gap read bare is
  false, so `where $row.done` keeps the rows whose `done` is true and passes over the
  rows that have no `done` at all.
- The text `true` keeps it and the text `false` drops it, in any case
  ([decision 0034](../decisions/0034-what-answers-a-predicate.md)). `attr f done=true`
  stores the word, and `where $row.done` asks whether it is done.
- Anything else **must** fail the stage with an `Invalid` fault on the first item that
  answers it, naming what the predicate answered and how to ask a question of it:

```
$row.kind is text (folder), not true or false. Compare it: $row.kind eq folder.
$row.size is number (0), not true or false. Compare it: $row.size eq 0.
kind is text (kind), not true or false. Did you mean $row.kind?
```

The value is shown by its display string, its first line only, cut to 40 characters.
The fix is `Compare it: <predicate> eq <value>.` when the predicate reads `$row`, with
the value quoted when it would not read back as one word, `Did you mean $row.<word>?`
when the predicate is a bare word, and nothing otherwise.

The same rule **must** hold for each operand of `and`, `or` and `not`
([decision 0034](../decisions/0034-what-answers-a-predicate.md)), and the fault names
the operand rather than the whole predicate. `where not $row.kind` fails on the first
row with `$row.kind is text (folder), not true or false. Compare it: $row.kind eq
folder.`, as does `where $row.kind eq text or $row.kind`. `and` and `or` still short
circuit, so an operand that is never read is never checked:
`where $row.kind eq nothing and $row.kind` answers an empty table.

## Running a line

```
Execute(tree, output, scope, cancellation):
    if tree is EmptyCommand:            return Empty
    if tree is PipedCommandList:        return Branches([tree])
    if tree is RecoveryLine:            return Branches(tree.Pipelines)
    otherwise:                          fail "Cannot evaluate a <node>."

Pipeline(input, stages):
    current = input
    for stage, n in stages:
        fail Cancelled if cancellation requested
        current = Stage(n, current, stage)     -- a fault here is stamped with stage n
    return current
```

Each stage receives the previous stage's value. The last stage's value is the result.
A line's first pipeline is given `Empty` as its input.

Cancellation **must** be observed between stages. A command that throws **must** stop
the pipeline like a command that failed: the exception becomes a fault of kind
`Internal` naming the exception's type, or `Cancelled` if it is an operation-cancelled
exception.

A stage is one of:

- a function expression, executed as a command;
- a command line expression, executed as a command;
- an instance tag, evaluated to a value without calling a command, ignoring its input;
- a pipeline in parentheses, run as a pipeline with the stage's input as its input;
- a variable reference, a *value stage*
  ([decision 0032](../decisions/0032-a-stage-may-be-a-value.md)).

A value stage's value is the variable's value with its members read off it, exactly as
the same reference written as an argument evaluates
([Evaluating a written value](#evaluating-a-written-value)). It calls no command and
emits no events. Like a tag standing alone it ignores its input
([decision 0035](../decisions/0035-a-value-stage-ignores-its-input.md)), so `ls | $v`
answers `$v`, and in a pipeline it is how a variable feeds the stages after it:

```
$ $v
5
$ $files | count
4
$ $problem.kind
NotFound
```

A value stage is a stage like any other for recovery: a default applies when it answers
`None` or `Empty`, `try` makes its failure a value, and `else` runs when it fails. So
`$nope else echo fallback` answers `fallback`, and `$maybe ?? "default"` still fails
with `Unknown variable: $maybe` when `maybe` is not set, because a missing variable is a
failure, not an empty answer (see [Conformance](conformance.md#known-deviations)).

### The row outside a predicate

`$row` is not a variable anyone sets: a predicate binds it for each item it tests
([decision 0008](../decisions/0008-explicit-row-variable.md)). Read anywhere else — as
a value stage, as an argument, in a variable tag, or with members — it **must** fail
with a fault of kind `NotFound`, path `$row`, that says where it exists rather than
that it is unknown:

```
$row is the row a predicate is testing. It exists only inside where, find, in and save-view: ls | where $row.kind eq folder.
```

`$row`, `echo $row`, `$row.kind` and `echo <$row>` all answer it. A variable that a
person did bind as `row` is read as any other variable outside a predicate, and is
shadowed inside one.

### Recovery

Decisions [0014](../decisions/0014-recovery-operator.md) and
[0024](../decisions/0024-stop-is-not-recoverable.md). A **recoverable** fault is one of
any kind but `Cancelled`; `Internal` is recoverable.

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
  an answer: `try x ?? y` keeps the fault. A stage that failed without `try` fails the
  line whatever its default;
- evaluate a default as a written value, running any pipeline in parentheses in it
  only when the default is used;
- never recover from `Cancelled`, with `try` or with `else`.

### Nested pipelines

A pipeline in parentheses in operand position — an argument, either side of a
comparison, under `and`, `or` or `not`, or a default — **must** be run before the
stage's arguments are bound, in the order written, with `Empty` as its input, in the
line's working projection ([decision 0023](../decisions/0023-adjacent-function-parenthesis.md)).
Its value is what the binder sees in its place, and its events join the line's
transaction. It **must** run once per evaluation of the stage it is written in, not
once per row of a predicate. A pipeline in parentheses gets no pipe input:
`ls | first (count)` hands `count` nothing.

A fault from inside a nested pipeline fails the stage it is written in, keeping its
message and kind. Its stage number is cleared on the way out, so the stage reported is
the outer stage the parenthesis is written in: `try echo (echo a | read nowhere)` reports
stage 1.

A pipeline in parentheses standing as a stage is part of the line's own sequence of
stages, and a fault from inside it **must** likewise report the stage the parenthesis
stands at: `try (echo a | read nowhere) | set f` leaves `$f.stage` at 1.

A nested pipeline reached where no line is running — in the text of a view file that
was written by hand — **must** fail with `A pipeline in parentheses only runs as part of
a line : (...)`, kind `Invalid`, when evaluated.

### A line is one transaction

Decision [0015](../decisions/0015-atomic-lines.md). The stages of a line share a
**working projection**: the committed projection with the events produced so far in
this line folded into it. Each stage reads that, so a later stage sees an earlier one's
effect and `mkdir scratch | in` lands in the new folder.

Nothing is appended to the log until the whole line has succeeded. Then the accumulated
events are validated against the committed projection ([The store](#the-store)) and
appended as a single transaction whose source is the line as it was written. A line
that fails validation fails as a whole and appends nothing.

An implementation **must**:

- append nothing when any stage fails, so a failed line leaves no trace, not even in
  `history`;
- append nothing when the accumulated events are empty, so a read-only line leaves no
  transaction and `undo` reaches past it;
- keep meta commands out of the transaction, since their business is the log itself.

### Executing one command

```
spec       = resolve(name)
nested     = run the stage's nested pipelines
bound      = bind(arguments, spec, input, scope, nested)
record bound.events in the working projection
invocation = { spec, bound, input, output, scope, working projection, location, blobs, token }
result     = await spec.Run(invocation)
record result.events, unless spec is Meta
```

An implementation **must** fold the events produced by evaluating the arguments into
the working projection before running the command, so a tag that bound a variable is
visible to it.

An implementation **must** await a command before the next stage, so a pipe carries a
finished value. Keeping a user interface responsive is the host's problem, not the
evaluator's.

An implementation **must** convert an unexpected exception from a command into a fault
rather than letting it escape (see [Running a line](#running-a-line)).

## Evaluating tags

| Tag | Result |
| --- | --- |
| `ObjectInstance` | `Object` with evaluated attributes and children |
| `ComponentInstance` | `Component` with evaluated attributes and children |
| `VariableTag` | The named variable's value, or fail with `Unknown variable: $<name>`, or for `<$row>` outside a predicate the fault in [The row outside a predicate](#the-row-outside-a-predicate) |

Rules:

- Attribute values evaluate as written values, in the order written, so `$name`
  resolves against the scope and `3` becomes a number.
- Attribute names are kept as written and are case-sensitive: `<t A=1/>` has an
  attribute `A`, and `$x.a` reads `None` from it.
- Every child that is an instance tag — an object, a component or a variable tag — is
  evaluated, in order, and becomes a child of the tag being built, whether that tag is
  an object or a component.
- A property assignment parses but does not evaluate: an implementation **must** fail
  with `Cannot evaluate a child PropertyAssignment.` (kind `Internal`) rather than
  ignoring one.
- A tag that names a variable binds it after the value is built, and **must** do so by
  producing a `VariableChanged` event rather than by mutating a scope, so the binding is
  committed and undone with the rest of the line. The name is not part of the value.
  The scope is threaded through the evaluation, so a later attribute, child or argument
  sees the binding.

When the same attribute name is written twice, the later value wins, and the tag holds
the name once: `<t a=1 a=2/>` displays as `<t a=2/>`.

## Variables and scope

A scope (`Scope.fs`) is a frame of variables with an optional parent. Lookup walks to
the parent; binding writes to the frame it is asked to write to, shadowing any binding
of the same name in a parent. Scopes are immutable values: binding returns a new scope.

An implementation **must** provide: look up by name, bind, unbind, push a child frame,
and enumerate everything visible with the innermost binding winning, ordered by name.

A line runs in one frame: the store's variables projection. A variable changes only
through a `VariableChanged` event, so `set` is undone with its line and survives a
reload. A predicate pushes a child frame for `$row` and discards it afterwards.

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
- `out` **must** clear the view when one is set, and move to the parent otherwise, so
  two `out`s leave a view over a subfolder in the order they were entered.
- Renaming a folder at or above `Folder` with `attr` **must** move `Folder` with it,
  in the same line, so the session is never left in a folder that no longer exists.
- Every change travels as `LocationChanged`, so undo restores the whole location.

A view's predicate is evaluated once per candidate record, in a child scope with `$row`
bound to a row of the same shape `ls` produces — so `$row.kind`, `$row.folder`,
`$row.size` and any attribute the record carries all mean in a view what they mean
after `ls |`.

### The trail

Decision [0037](../decisions/0037-in-out-back-and-read.md). The projection holds the
**trail**, the places `in` and `out` have left, the most recent first
(`Projection.Trail`). It is what [`back`](command-catalogue.md#back) retraces, and like
the rest of the projection it is a fold over the log, so it survives a reload and
`undo` changes it.

- Every move `in` or `out` makes **must** emit `TrailPushed` of the location it leaves,
  then `LocationChanged`, in that order, in the same line. A move to where you already
  are emits neither.
- `back` **must** emit a `TrailPopped` for each place it takes off, most recent first,
  then its `LocationChanged`. It **must not** push.
- No other command touches the trail. Renaming a folder you are in moves the location
  without a step on the trail, and a place on the trail under the old name is one whose
  folder no longer exists, which `back` passes over.
- Folding `TrailPushed p` puts `p` on top of the trail. Folding `TrailPopped p` removes
  the most recent entry that is the same place as `p`, and changes nothing when there is
  none, so the fold stays total.
- Two places are the same when their folders are equal and their views' display texts
  are equal (`Projection.samePlace`). A view is compared by its text because that is how
  it is stored: a view read back from the log is parsed again, and a nested pipeline in
  it would not compare equal to the tree it was parsed from.

`TrailPushed` and `TrailPopped` are each other's inverse ([The store](#the-store)), so
undoing a move takes its place off the trail, and undoing a `back` puts back every place
it took, in order. A fresh session, and one after `reset`, starts with an empty trail.

## Refreshing

An implementation **may** offer a way to re-run a line without committing it, so a host
can keep a listing on screen up to date as the store changes. Where it does:

- Every stage of the line **must** name a registered command whose spec is `ReadOnly`,
  or be a value stage, which only reads. That includes every branch after `else`,
  every nested pipeline, every stage in parentheses and every default, and an instance
  tag standing as a stage is not read-only, because it can bind a variable. A line that fails this **must** be refused with a fault of kind `Invalid`,
  `A live refresh only re-reads : <line>`, *before* any of it runs. An unregistered
  name is not read-only.
- The run **must** commit nothing, whatever events it gathers, and **must** leave the
  history and the undo chain untouched. What it writes to its output while it runs goes
  nowhere.

## The store

The filesystem, the variables and the current location are a projection folded from an
append-only log of transactions (`Events.fs`, `Projection.fs`, `Store.fs`). A
transaction carries a sequence number, a timestamp, its source line, its events, the
sequence number of the transaction it compensates if any, and whether it is undoable.

An implementation **must** be able to reach the same projection by replaying the same
log into a fresh store. Folding an event **must** be total: an event that names a record
the projection does not hold is ignored rather than failing, because validation happened
before the event was appended and a replay that could fail would be a log that could not
be read back.

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
| `TrailPushed p` | `TrailPopped p` |
| `TrailPopped p` | `TrailPushed p` |

A trail event carries only the place: a push and a pop of the same place are each
other's record of both sides, and a whole trail before and after would make every move
cost the length of the trail ([The trail](#the-trail)).

The identity holds for events that describe a change that happened: an event's `before`
side **must** be what the projection holds. Commands build events by reading the
projection, so they cannot produce anything else.

Before appending, an implementation **must** validate the events in order against the
state each will land on — the committed projection with the earlier events of the same
batch applied. A `FileCreated`, and an `AttributesChanged` that changes a record's
`name` or `folder`, **must** be rejected when the name is empty
(`A file must have a name.`, `Invalid`), contains `/` or is `.` or `..`
(`'<name>' is not a valid file name: …`, `Invalid`), or is already used by another record
in its folder (`Target file already exists : <path>`, `Conflict`). An `AttributesChanged`
that moves a record into a folder that does not exist **must** be rejected with
`Directory does not exist : <folder>` (`NotFound`). A change that leaves `name` and
`folder` alone is not judged, so a record from an older log can still be given an
attribute. The same name rule governs every command that names a record
([command catalogue](command-catalogue.md#rules-every-command-follows)).

## Undo

Decisions [0010](../decisions/0010-undo-by-event-sourcing.md) and
[0015](../decisions/0015-atomic-lines.md). The log only grows. Undo **must not** remove
a transaction; it appends a compensating transaction whose events are the target's
inverted and reversed, marked as compensating the target, and carrying the target's
source line.

A transaction is **compensated** when some later transaction compensates it and is not
itself compensated. That recursion is what makes undo, redo, undo behave.

| Operation | Target | Result |
| --- | --- | --- |
| `undo` | The latest undoable, non-compensating, uncompensated transaction | `Undone: <line it reversed>` |
| `redo` | The latest undoable, uncompensated **undo** | `Redone: <line the undo had reversed>` |

A redo is itself a compensation, so "the latest compensation" is the wrong rule for
redo: it finds the redo just appended and reverses it, and pressing redo twice puts a
change back and then takes it away again. An implementation **must** distinguish an
undo, which compensates a line someone ran, from a redo, which compensates an undo.

Neither is a failure when there is nothing to do: an implementation **must** return a
plain result saying so — `Nothing to undo.`, `Nothing to redo.` — not a fault, because
nothing went wrong.

Undo and redo are meta commands: the compensation is appended when the command runs,
not with the line around it.

A transaction may be marked not undoable. The seeded filesystem is
([decision 0018](../decisions/0018-the-seed-is-not-a-line-anyone-typed.md)): it is
recorded and replayed so the store stays a pure fold of its log, and `undo` **must**
skip it while `history` still shows it.

`history` is the transactions oldest first, each with a derived `Undone`. A compensation
**must not** itself be marked undone. The command answers a table with the columns
`seq`, `at`, `source`, `undone` and `compensates`, the last being the sequence number of
the transaction a compensation reverses and `None` otherwise.

## Starting and starting over

A session (`Session.fs`) replays its log before it will run anything, and **must**
refuse to execute before that has completed, with a fault of kind `Internal`:
`The session has not been initialised. Call Initialize first.` An empty filesystem and
a lost one look identical to a user, so a host that forgets to wait is told rather than
shown nothing.

Seeding happens **only** when the replayed log is empty, or a reload would lay a second
copy of the seed over what was restored. The seed is one transaction, with the source
`seed`, marked not undoable.

A log begun before the seed had a `/guide` folder is given it once, after replay
(`Session.BringUpToDate`, [decision 0036](../decisions/0036-the-guide-is-in-the-filesystem.md)):
one transaction with the source `guide`, marked not undoable, creating the folder and its
files. It **must not** be added to a log that has ever created a `/guide`, so one deleted
on purpose stays deleted, and `/readme.txt` is given the new text **only** if it still
holds the old seed's text exactly.

Then every seeded file nobody has changed is brought to the seed's current content
([decision 0040](../decisions/0040-seeded-files-follow-the-seed.md), `Seed.updatesFor`).
For each file the standard seed describes with content, the record at that folder and
name in the projection **must** have its content replaced by the seed's when:

- there is a record at that path, and it is not a folder;
- no undoable transaction in the log has named that record, by creating, deleting,
  renaming, moving, tagging or writing to it. `undo` and `redo` commit undoable
  transactions, so a file written to and put back by `undo` has been named, and so has
  every file in a folder renamed and renamed back. A record no undoable transaction
  has named was made by a system transaction, the seed's or the guide's;
- its content's hash differs from the hash of the seed's text.

The replacements are one `ContentChanged` each, in the seed's order, committed as one
transaction with the source `seed update`, marked not undoable, so `undo` reaches past
it. A record's attributes, `modified` included, are left as they were. A seeded file that
was deleted is not brought back, one that was renamed or moved is not at the path any
more, and a file made again at a seeded path is the user's. When nothing differs, no
transaction is committed, so a second load commits nothing. For a log seeded with older
texts, in which one guide file was then written to and one example program tagged, the
load after it gives:

```
$ history
seq  at        source                                undone  compensates
1    09:30:00  seed                                  false
2    09:30:00  echo mine | write guide/1-start.txt   false
3    09:30:00  attr examples/tables.clr level=first  false
4    09:30:00  seed update                           false

$ undo
Undone: attr examples/tables.clr level=first
```

`BringUpToDate` answers how many records it created, which only the guide can. A host
that seeds the standard filesystem calls it after `Initialize`; on a fresh log, or a
second time, it finds nothing to do and commits nothing.

A session runs one line at a time. A line submitted while another is running **must**
be refused with a fault of kind `Invalid`, `A command is already running. Stop it
first.`, rather than interleaved.

`reset` empties the log and seeds it again. It is the only operation that removes
anything from the log, and it is not undoable: the transactions that would have been
reversed are the ones it threw away. An implementation **must** say so in the command's
description, so `help` warns before rather than after.

## Scripts

Decision [0020](../decisions/0020-scripts-and-run.md). A script is a text file of
command lines, of kind `script` and conventionally named `.clr`. `run path` (the path
may be piped) executes it:

- Lines are split on line feeds, with a carriage return before one ignored, and each is
  trimmed. A blank line, or one whose first character is `#`, is skipped, and still
  counted.
- Before each line, cancellation is checked. The line is written to the output as
  `> <line>`, then run exactly as if it had been typed: parsed, evaluated, and
  committed as its own transaction. A result with a non-empty display string is written
  to the output after it.
- The first failing line stops the script. Its fault is re-raised with its message
  prefixed, `<path> line <n>: <message>`, where `n` counts every line in the file, and
  everything else about it, the kind included, unchanged.
- `run` answers the value of the last line it ran, or `Empty`.
- A script may run a script, to a depth of eight. Deeper fails with
  `Scripts are only allowed to run scripts 8 deep.`, kind `Invalid`.

`run` is a meta command and commits nothing of its own, which is what lets each line it
runs commit its own. A script is therefore **not** atomic: lines that succeeded before a
failure stay committed, `undo` after a script undoes its last line, and recovering from
`run` with `try` or `else` recovers from the failed line, not from the whole script. The
same holds when the line that ran the script fails in a later stage: the script's
transactions have already been appended.

## Cancellation

Cancellation is cooperative. A long-running command **must** observe its invocation's
token and **should** check it at least once per step.

A host cancels by cancelling the token it passed in. A cancelled command **must** return
a fault of kind `Cancelled` with the message `Stopped.`, and the line commits nothing,
like any other failed line. Neither `try` nor `else` recovers from it.

Cancellation **must** also be observed between stages, and between the lines of a
script.

## Faults

Failure is a value (`Faults.fs`). A command returns `Error fault`; no command raises,
and no exception crosses a module boundary.

```
Fault = { Kind; Message; Stage; Path; Cause }
Kind  = Syntax | Binding | UnknownCommand | NotFound | Conflict | Invalid | Cancelled | Internal
```

Every message in the [error reference](../errors.md) is preserved word for word as
`Message`. The kind is additional and **must not** replace it. `Path` names the path or
variable a fault is about, where there is one: `/nowhere` for a missing file, `$x` for
an unknown variable.

The evaluator **must** stamp `Stage` with the one-based position of the failing stage,
and **must not** overwrite a stage already set, so the innermost failure keeps its own
position. A fault leaving a nested pipeline, in operand position or standing as a
stage, **must** have its stage cleared first, so that the stage reported is the one in
the line that was written (see [Nested pipelines](#nested-pipelines)).

A fault is also a value: see [Recovery](#recovery). The kind's word, as `$f.kind` reads
it and a host carries it, is the case name exactly: `Syntax`, `Binding`,
`UnknownCommand`, `NotFound`, `Conflict`, `Invalid`, `Cancelled`, `Internal`.

A line that does not parse fails with kind `Syntax`. Its message is the parser's
explanation when it has one (`'eq' is an operator; write "eq" to pass it as text`), the
parser's messages for a structural failure such as a mismatched closing tag, and
otherwise `Could not parse the command.`, which a host **may** replace with the
position and expected symbols (see [Errors](lexical-grammar.md#errors)).

An implementation **must not** let a failure end the session. `Session.Execute` **must
not** raise: a parse failure is `Syntax`, a cancellation is `Cancelled` with the message
`Stopped.`, and an unexpected exception is `Internal`, with the message
`<exception type> : <exception message>`.
