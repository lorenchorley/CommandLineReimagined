# Execution model

Normative semantics: the value model, how arguments bind, how a line runs and commits,
how tags evaluate, what undo, redo and history guarantee, how scripts run, how
cancellation behaves, and what the terminal says about a line beside its answer.

Reference implementation: `Core` (assembly `CommandLineReimagined.Core`), in F#
([decision 0006](../decisions/0006-functional-core-in-fsharp.md)). The files named
below are in that project.

Transcripts marked `$` are pasted from `tools/transcript.fsx`, which runs lines against
the real core. After a failed line's message it prints the fault's kind in brackets;
after the answer or the fault it prints each [note](#notes) as `suggestion:` or
`explanation:`, and each of the note's fixes as `fix:`. Those markers are the script's,
not the screen's: a page draws a note as guidance and a fix as a chip.

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
kind `UnknownCommand` and the message `Unknown command : <name>`, and nothing more
([decision 0041](../decisions/0041-guidance-is-drawn-apart-from-output.md)). When any
command name is near, the fault **must** carry a `suggestion` [note](#notes) naming the
nearest, at most three, `Did you mean <names>?`, with a fix for each that writes it in
place of the name as written ([Suggestions](#suggestions)):

```
$ lss
Unknown command : lss
  [UnknownCommand]
  suggestion: Did you mean ls?
  fix: ls

$ rum
Unknown command : rum
  [UnknownCommand]
  suggestion: Did you mean rm or run?
  fix: rm
  fix: run

$ rn
Unknown command : rn
  [UnknownCommand]
  suggestion: Did you mean in, rm or run?
  fix: in
  fix: rm
  fix: run
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
$ cd documents
Unknown command : cd
  [UnknownCommand]
  suggestion: Did you mean in?
  fix: in documents

$ cat documents/notes.txt
Unknown command : cat
  [UnknownCommand]
  suggestion: Did you mean read?
  fix: read documents/notes.txt

$ delete x
Unknown command : delete
  [UnknownCommand]
  suggestion: Did you mean rm?
  fix: rm x

$ navigate
Unknown command : navigate
  [UnknownCommand]
  suggestion: Did you mean back, in or out?
  fix: back
  fix: in
  fix: out

$ by
Unknown command : by
  [UnknownCommand]

$ as
Unknown command : as
  [UnknownCommand]
  suggestion: Did you mean ls?
  fix: ls
```

`cd` is `in`'s first keyword and nobody else's, so it names `in`, although it is one slip
from `cp`. `by` is `group`'s first keyword and one of `sort`'s as well, so it names
neither, and nothing is one slip from it, so it has no note. `as` is a keyword of `table`
but not its first, so it is corrected as a slip. The fix keeps the rest of the line:
`cd documents` offers `in documents`. The same nearest names answer `help` for a name
that is not a command, and completion finds a command by keyword by the same rule for a
word of one or two letters ([Host interfaces](host-interfaces.md#reading-the-place)).

If `UnknownCommand` is not registered, fail with the same fault directly, notes and all.

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
whose [suggestion](#suggestions) names the fix while the help says the rest. A line that
carries a guide carries its notes as well.

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
| `$name` | The variable's value, or fail with `Unknown variable: $<name>`, kind `NotFound`, path `$<name>`, whose response names the nearest variables ([Suggestions](#suggestions)). `$row` outside a predicate fails differently; see [The row outside a predicate](#the-row-outside-a-predicate). |
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
value is put in its place, so the message quotes what was typed. The message is
`<predicate> never reads $row, so it is the same for every row.` and nothing more; what
was probably meant is a `suggestion` [note](#notes) on the fault, with a fix that writes
it in place of the predicate as written:

```
$ ls | where kind eq folder
kind eq folder never reads $row, so it is the same for every row.
  [Binding]
  suggestion: Did you mean $row.kind eq folder?
  fix: ls | where $row.kind eq folder

$ ls | where 3 lt size
3 lt size never reads $row, so it is the same for every row.
  [Binding]
  suggestion: Did you mean 3 lt $row.size?
  fix: ls | where 3 lt $row.size

$ ls | where not done
not done never reads $row, so it is the same for every row.
  [Binding]
  suggestion: Did you mean not $row.done?
  fix: ls | where not $row.done

$ ls | where $v eq 5
$v eq 5 never reads $row, so it is the same for every row.
  [Binding]
```

The suggestion reads the bare words the predicate compared as columns: in a comparison,
its left side when that is a word, and otherwise its right; under `and`, `or` and
`not`, an operand that is a word on its own. A word here is a letter or `_` followed by
letters, digits and `_`. When nothing can be read that way, the fault has no note.

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
  answers it, `<predicate> is <kind> (<value>), not true or false.`, naming what the
  predicate answered. How to ask a question of it is a `suggestion` [note](#notes) on
  the fault, worded like every other suggestion, with a fix that writes it in place of
  the expression as written:

```
$ ls | where $row.kind
$row.kind is text (folder), not true or false.
  [Invalid]
  suggestion: Did you mean $row.kind eq folder?
  fix: ls | where $row.kind eq folder

$ ls | where $row.size
$row.size is number (0), not true or false.
  [Invalid]
  suggestion: Did you mean $row.size eq 0?
  fix: ls | where $row.size eq 0

$ ls | where kind
kind is text (kind), not true or false.
  [Invalid]
  suggestion: Did you mean $row.kind?
  fix: ls | where $row.kind
```

The value is shown by its display string, its first line only, cut to 40 characters.
The suggestion is `Did you mean <predicate> eq <value>?` when the predicate reads
`$row`, with the value quoted when it would not read back as one word,
`Did you mean $row.<word>?` when the predicate is a bare word, and there is no note
otherwise.

The same rule **must** hold for each operand of `and`, `or` and `not`
([decision 0034](../decisions/0034-what-answers-a-predicate.md)), and the fault names
the operand rather than the whole predicate, so the fix writes the question in place of
the operand. `and` and `or` still short circuit, so an operand that is never read is
never checked, and `where $row.kind eq nothing and $row.kind` answers an empty table,
which [explains itself](#an-empty-filter-explains-itself). An operand written twice is
suggested and offers no fix, because the fault does not say which of the two it was
about ([Resolving the fixes](#resolving-the-fixes)):

```
$ ls | where not $row.kind
$row.kind is text (folder), not true or false.
  [Invalid]
  suggestion: Did you mean $row.kind eq folder?
  fix: ls | where not $row.kind eq folder

$ ls | where $row.kind eq text or $row.kind
$row.kind is text (folder), not true or false.
  [Invalid]
  suggestion: Did you mean $row.kind eq folder?

$ ls | where $row.kind eq nothing and $row.kind
name  kind  folder  size  modified
  explanation: kind is folder or text
```

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
shadowed inside one. The fault carries no note: it already says where `$row` exists,
and `row` is never named as a variable near another ([Suggestions](#suggestions)).

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
- never recover from `Cancelled`, with `try` or with `else`;
- make a fault a value only without its notes, at any depth of `cause`
  (`Fault.asValue`), so the `Fault` that `try` hands on and the one `else` pipes in
  carry none, and `$problem` is the same whether or not the terminal had something to
  say ([decision 0041](../decisions/0041-guidance-is-drawn-apart-from-output.md));
- restore the notes gathered so far with the working projection, so a stage that `try`
  rolled back, and a branch that `else` rolled back, take their notes with them
  ([Gathering notes](#gathering-notes)).

```
$ try read notes | set p
File does not exist : /notes

$ $p.message
File does not exist : /notes

$ ls | where $row.knd eq folder | read zzz else echo recovered
recovered
```

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
- Its response **must** carry the line's [notes](#notes) exactly as executing it would,
  so a live listing that keeps no row carries its explanation, and loses it when a row
  comes back. It carries no guide.

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

A fault from a line of the script keeps its notes, so a mistake in a script is still
suggested. Its fixes are resolved against the line that ran the script, which does not
hold the mistake, so it offers none ([Resolving the fixes](#resolving-the-fixes)). The
notes of a script's lines that succeeded are not carried: `run` answers with none of
its own.

```
$ write bad.clr "read notes"
bad.clr

$ run bad.clr
> read notes
/bad.clr line 1: File does not exist : /notes
  [NotFound]
  suggestion: Did you mean documents/notes.txt?
```

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
Fault = { Kind; Message; Stage; Path; Cause; Notes }
Kind  = Syntax | Binding | UnknownCommand | NotFound | Conflict | Invalid | Cancelled | Internal
```

Every message in the [error reference](../errors.md) is preserved word for word as
`Message`. The kind is additional and **must not** replace it. `Path` names the path or
variable a fault is about, where there is one: `/nowhere` for a missing file, `$x` for
an unknown variable.

A message says what went wrong and nothing more
([decision 0041](../decisions/0041-guidance-is-drawn-apart-from-output.md)). What was
probably meant is never part of it: no message ends in `Did you mean …?`, and a
message **must** read the same whether or not the terminal found anything near. The
suggestions are `Notes` ([Notes](#notes)), which are guidance and not part of the fault
as a value: a fault made a value by `try` or `else`, or kept in the log, has none.

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

## Notes

Decisions [0041](../decisions/0041-guidance-is-drawn-apart-from-output.md) to
[0045](../decisions/0045-a-near-value-offers-a-fix.md). Output is what a command
answered: a table, a file's text, a value, and a fault's message. A **note** is
something the terminal says of its own about a line, beside the answer: what was
probably meant, or why an answer is empty (`Faults.fs`).

```
Note = { Kind; Text; Fixes }
Kind = "suggestion" | "explanation"
Fix  = Line of string                   -- the whole corrected line
     | Replace of written * corrected    -- what was written, as it should have been
```

A `suggestion` says what was probably meant; an `explanation` says why a filter kept
nothing. `Text` is one sentence. `Fixes` are corrected lines, which a host offers to put
in the input without running them
([decision 0044](../decisions/0044-a-fault-may-carry-fixes.md)).

A note is **guidance**, and an implementation **must** keep it out of every value:

- A command's result carries its notes beside its value and events (`CommandResult.Notes`,
  `Invocation.withNotes`), and a fault carries its own (`Fault.Notes`,
  `Fault.withNotes`). The next stage sees neither: the pipe carries the value.
- A fault that `try` or `else` makes into a value **must** have no notes, at any depth
  of `cause` (`Fault.asValue`), so `$problem.message` is the message alone.
- A stored fault carries no notes, and one read back from the log has none
  ([Host interfaces](host-interfaces.md#the-stored-shape)).
- `else`, `try`, `??` and pipes **must** behave exactly as they would if no note had been
  made, and a line with notes commits exactly what it would without them.

### Gathering notes

The evaluator gathers the notes of the commands a line ran, in the order they gave them,
into `Execution.Notes`, and the session carries them in its response
(`Session.Response.Notes`, [Host interfaces](host-interfaces.md#the-sessions-response)):

- A line that **succeeded** carries the notes of every stage whose work stood, the stages
  of a pipeline in parentheses included. Notes are saved and restored with the working
  projection, so a stage that `try` rolled back, and every stage of a branch that `else`
  rolled back, take theirs with them ([Recovery](#recovery)).
- A line that **failed** carries its fault's notes, with the nearest names the session
  adds for a missing path or variable ([Suggestions](#suggestions)), and no other: the
  notes of the stages before the one that failed go with the line.
- A [refresh](#refreshing) carries notes by the same rules.
- A line with nothing to say carries none.

```
$ echo (ls | where $row.knd eq folder)
name  kind  folder  size  modified
  explanation: No row has knd; did you mean kind?
  fix: echo (ls | where $row.kind eq folder)

$ echo (ls | where $row.knd eq folder) (read zzz)
File does not exist : /zzz
  [NotFound]

$ try echo (ls | where $row.knd eq folder) (read zzz)
File does not exist : /zzz
```

### Suggestions

A suggestion names what was probably meant, nearest first and at most three, in one
sentence: `Did you mean a?`, `Did you mean a or b?`, `Did you mean a, b or c?`
(`Fault.nearestNote`). With nothing near there is no note, and the fault is as it would
be without one. The fixes, when there are any, are one per name, in the same order, and
at most three.

| What failed | What is named | The fix for each |
| --- | --- | --- |
| `Unknown command : <name>` | The nearest commands, by [Resolving a command](#resolving-a-command) | The command in place of the name as written |
| `<predicate> never reads $row, so it is the same for every row.` | The predicate with its bare words read as columns ([Predicates](#predicates)) | That predicate in place of the one written |
| `<expression> is <kind> (<value>), not true or false.` | `<expression> eq <value>`, or `$row.<word>` ([Predicates](#predicates)) | That in place of the expression written |
| `File does not exist : <path>`, `Nothing exists at : <path>` | The nearest records | The path in place of the argument that named the missing one |
| `Directory does not exist : <path>`, `Target directory does not exist : <path>` | The nearest folders | As for a file |
| `Unknown variable: $<name>` | The nearest variables | The variable in place of `$<name>` |

The first three are made with the fault. A missing path or variable is found by a
command that cannot see what else there is, so the session names the nearest when it
builds the response (`Session.faultNotes`), reading what is missing from the fault's
kind, `NotFound`, its `Path` and its message, which a script's prefix may stand in front
of (`Fault.missing`). It looks in the projection as it stands after the line: after a
failed line, what there was before it. `$row` is never a missing variable in this sense.

**Nearest records** (`Nearest.paths`,
[decision 0042](../decisions/0042-a-missing-name-names-the-nearest.md)). The missing
path is made absolute against the current folder first, since `ls` and `in` name it as
written. It splits into a folder and a name; a missing path that is `/`, or whose name is
empty, names nothing. Then:

1. **Here.** The records in that folder whose name is within `Nearest.threshold` of the
   name and is not the name itself, a name that differs only in case included, ordered by
   distance and then by name, ordinally. For a name written on its own the folder is the
   current one, so `read note` in `/documents` names `notes.txt`.
2. **Anywhere.** Every record whose name is the name, ignoring case, or begins with it,
   ignoring case (`notes` for `notes.txt`, `doc` for `documents`): the same name before
   a name it begins, then the shallower before the deeper, then by path, ordinally. The
   missing path itself is left out.
3. The two lists joined, each path once, and at most three.

A folder is looked for among folders only, for `Directory does not exist` and `Target
directory does not exist`. A file is looked for among every record, since `rm` takes
either.

A path is named as a person would write it standing in the current folder: relative
when it is inside the current folder, absolute otherwise, and quoted when it would not
read back as one word, as a value on the right of `eq` is quoted. Its fix is the line
with that written path in place of the argument that named the missing path, or a path
under it, whose remainder the fix keeps: `mkdir documnts/x` offers `mkdir documents/x`.
The argument is found by splitting the line at white space, `|`, `(`, `)` and `=`,
reading a quoted word whole, and passing over the first word of the line and of each
stage, and the word after `try`, `else` or `??`, since each names a command; a word
beginning `$`, `-` or `<` is not a path. The first argument that resolves, against the
current folder, to the missing path or a path under it is the place. A line without one,
such as one that ran a script with the mistake, gets the note and no fix.

```
$ read notes
File does not exist : /notes
  [NotFound]
  suggestion: Did you mean documents/notes.txt?
  fix: read documents/notes.txt

$ read zzz
File does not exist : /zzz
  [NotFound]

$ in documnts
Directory does not exist : documnts
  [NotFound]
  suggestion: Did you mean documents?
  fix: in documents

$ rm readme.tx
Nothing exists at : /readme.tx
  [NotFound]
  suggestion: Did you mean readme.txt?
  fix: rm readme.txt

$ cp readme.txt doc
Target directory does not exist : /doc
  [NotFound]
  suggestion: Did you mean documents?
  fix: cp readme.txt documents

$ mkdir documnts/x
Directory does not exist : /documnts
  [NotFound]
  suggestion: Did you mean documents?
  fix: mkdir documents/x

$ read guide/1-strat.txt
File does not exist : /guide/1-strat.txt
  [NotFound]
  suggestion: Did you mean guide/1-start.txt?
  fix: read guide/1-start.txt

$ echo (read notes)
File does not exist : /notes
  [NotFound]
  suggestion: Did you mean documents/notes.txt?
  fix: echo (read documents/notes.txt)
```

and, standing in `/documents`:

```
$ read readme
File does not exist : /documents/readme
  [NotFound]
  suggestion: Did you mean /readme.txt?
  fix: read /readme.txt
```

**Nearest variables** (`Nearest.variables`). Among the variables that are set, `row`
excepted: first those within `Nearest.threshold` of the name and not the name itself,
nearest first, a name that differs only in case included; then those the name is the
start of, ignoring case; equals in name order, each once, and at most three. Each is
named with its `$`. Its fix is the line with the variable in place of the first
`$<name>` written as a whole variable, with or without members read off it, so
`echo $fles.name` offers `echo $files.name`. A line without one gets no fix. After
`ls | set files`:

```
$ echo $fles
Unknown variable: $fles
  [NotFound]
  suggestion: Did you mean $files?
  fix: echo $files

$ echo $fil
Unknown variable: $fil
  [NotFound]
  suggestion: Did you mean $files?
  fix: echo $files

$ echo $fles.name
Unknown variable: $fles
  [NotFound]
  suggestion: Did you mean $files?
  fix: echo $files.name

$ echo $zzzz
Unknown variable: $zzzz
  [NotFound]
```

**The distance.** Every nearness here is `Nearest.distance`, the edit distance
[Resolving a command](#resolving-a-command) defines, ignoring case, within
`Nearest.threshold` of the word written: one for a word of up to four letters, two for a
longer one.

### Resolving the fixes

Whatever finds a mistake rarely knows the line it was written in, so it may say a fix as
`Replace(written, corrected)`. Before a response leaves the session, every fix **must**
be a whole line made of the typed line, `Source` (`Note.resolve`, `Fix.apply`):

- `Replace(written, corrected)` becomes the line with the first place that holds
  `written` as a whole word written as `corrected`. A place is whole when the characters
  either side of it, where there are any, do not continue a word: a letter, a digit,
  `_`, `-`, `.` or `$`. So replacing `kind` reaches neither `kinds` nor `$row.kind`. An
  empty `written` makes no line.
- A replacement the line has no place for is dropped: a mistake inside a script that
  `run` ran, or inside a view's predicate read back from its record.
- A fix that makes the line it was given, and a fix that makes the same line as an
  earlier one, are dropped.
- The note stays when every fix is dropped: what it says is still true.

**Written twice.** A fault's replacement whose `written` is in the line, as a whole word,
in more than one place **must** be dropped (`Session.faultNotes`): the fault does not
say which of them it was about, and the first can be the wrong one.
`ls | where $row.kind eq text or $row.kind` is suggested and offers no fix
([Predicates](#predicates)). A fix that is already a whole line, as the fix for a path
or a variable is, is not held to this. An explanation's replacement is not held to it
either, and is resolved against the first place (see
[Conformance](conformance.md#known-deviations)).

### An empty filter explains itself

Decisions [0043](../decisions/0043-an-empty-filter-explains-itself.md) and
[0045](../decisions/0045-a-near-value-offers-a-fix.md). When `where`, `find` or a view's
listing (`ls` while a view is set) keeps no row of a table that had some, its result
**must** carry at most one `explanation` note (`Expr.explainEmpty`), and the empty
table stays the answer, so a pipe, `try` and `else` see what they saw before. An empty
table in, or a filter that keeps a row, says nothing. The table read is, for `where`,
the table it was given, and for `find` and a view, every record in the store as a row of
the shape `ls` gives.

A column **has** a value when the table lists it and at least one row's cell in it is not
a gap; a column is matched by name exactly, as `$row.` reads it. The explanation is the
first of these that applies:

1. **A column no row has.** The first column the predicate reads through `$row.`,
   anywhere in it and in the order written, that no row has. The text is
   `No row has <column>; did you mean <names>?`, the names being the columns some row
   has that differ from it only in case, then those `Nearest.names` finds near it, each
   once and at most three, joined as a suggestion's are; or `No row has <column>.` with
   none. It offers one fix, for the first name: `$row.<column>`, with any members read
   after it, replaced by `$row.<name>` with the same members. One column at a time: a
   fix changes one place.
2. **A comparison that keeps no row.** Otherwise, the first operand of the predicate's
   top-level `and`s, taken in order, that has an explanation below: one that compares
   one column, read as `$row.<column>` with no member after it, with an operand that
   does not read `$row`, and that on its own keeps no row, every cell that is not a gap
   comparing false. A predicate with no `and` is its own one operand. The column is put on
   the left, `100 lt $row.size` being asked as `$row.size gt 100`; `like` and `has` are
   not turned, and with the column on their right are not explained. The other operand
   is evaluated in the line's scope, so it may be a constant, a variable or the value of
   a pipeline in parentheses; one that fails to evaluate is not explained. By operator:
   - `eq` and `like`: `<column> is <values>`, the values the column does have;
   - `gt`, `ge`, `lt` and `le`, when the operand and every cell read as numbers:
     `<column> runs from <least> to <greatest>`, or `<column> is <value>` when they are
     all one number. An ordering of words is not explained;
   - `ne` and `has` have no explanation. `ne` keeping nothing means every row holds the
     one value the question named, and what `has` looks inside is not a column's values.
3. Otherwise nothing. An `or` or a `not` keeping nothing has no one part to name, a bare
   `$row.done` was compared with nothing, and a read deeper than a column
   (`$row.name.length`) is not the column's values.

**The values** are the column's cells that are not gaps, distinct by display string,
most frequent first, ties in the order the rows of the table read had them, each written
as it would be on the right of `eq`: its first line, cut to 40 characters, quoted when it
would not read back as one word. At most five are named, joined `a`, `a or b`,
`a, b or c` and so on up to five. With more, the first five are joined with commas and
the sentence ends `or <n> more`, where `n` is how many were left out.

**A near value** ([decision 0045](../decisions/0045-a-near-value-offers-a-fix.md)). For
`eq` only, when the compared operand is written in the line as a constant and
`Nearest.names` finds values of the column near its display string, the explanation
offers one fix: the value, written as it would be on the right of `eq`, replaced by the
nearest, written the same way. The text is the same with or without it. A value held in
a variable has no place in the line and offers none, and nor does a value with nothing
near.

```
$ ls | where $row.knd eq folder
name  kind  folder  size  modified
  explanation: No row has knd; did you mean kind?
  fix: ls | where $row.kind eq folder

$ ls | where $row.Kind eq folder
name  kind  folder  size  modified
  explanation: No row has Kind; did you mean kind?
  fix: ls | where $row.kind eq folder

$ ls | where $row.zzz eq 1
name  kind  folder  size  modified
  explanation: No row has zzz.

$ ls | where $row.kind eq foldr
name  kind  folder  size  modified
  explanation: kind is folder or text
  fix: ls | where $row.kind eq folder

$ ls | where $row.kind eq "foldr"
name  kind  folder  size  modified
  explanation: kind is folder or text
  fix: ls | where $row.kind eq "folder"

$ ls | where $row.kind eq zzzzzz
name  kind  folder  size  modified
  explanation: kind is folder or text

$ ls | where $row.kind eq $k
name  kind  folder  size  modified
  explanation: kind is folder or text

$ ls | where $row.name eq zzz
name  kind  folder  size  modified
  explanation: name is documents, examples, guide, projects or readme.txt

$ ls | where $row.size eq 7
name  kind  folder  size  modified
  explanation: size is 0 or 193
  fix: ls | where $row.size eq 0

$ ls | where $row.size gt 1000
name  kind  folder  size  modified
  explanation: size runs from 0 to 193

$ ls | where 1000 lt $row.size
name  kind  folder  size  modified
  explanation: size runs from 0 to 193

$ ls documents | where $row.size gt 100
name  kind  folder  size  modified
  explanation: size is 50

$ ls | where $row.kind eq foldr and $row.size gt 1000
name  kind  folder  size  modified
  explanation: kind is folder or text
  fix: ls | where $row.kind eq folder and $row.size gt 1000

$ ls | where $row.kind eq folder | where $row.name eq zz
name  kind  folder  size  modified
  explanation: name is documents, examples, guide or projects

$ find $row.kind eq foldr
name  kind  folder  size  modified
  explanation: kind is text, folder or script
  fix: find $row.kind eq folder
```

where `$k` was set to `foldr`. Over the whole store, with more than five names:

```
$ find $row.name eq zzz
name  kind  folder  size  modified
  explanation: name is documents, tables.clr, 1-start.txt, 2-values.txt, 3-tables.txt or 12 more
```

Each of these keeps nothing and says nothing:

```
$ ls documents | where $row.kind ne text
name  kind  folder  size  modified

$ ls | where $row.name has zz
name  kind  folder  size  modified

$ ls | where $row.kind eq x or $row.kind eq y
name  kind  folder  size  modified

$ ls | where not $row.size ge 0
name  kind  folder  size  modified

$ ls | where $row.name.lenght eq 3
name  kind  folder  size  modified

$ ls | take 0 | where $row.kind eq x
name  kind  folder  size  modified
```

A view's listing is `ls`, which does not hold the view's predicate, so its explanation
offers no fix. With the view `$row.kind eq foldr` set:

```
$ ls
name  kind  folder  size  modified
  explanation: kind is text, folder or script
```

Each filter in a line may explain itself, and a filter given an empty table says
nothing, so it is the filter that kept nothing that is explained. `try` and `else` do
not change what a filter says:

```
$ ls | where $row.knd eq folder else echo x
name  kind  folder  size  modified
  explanation: No row has knd; did you mean kind?
  fix: ls | where $row.kind eq folder else echo x

$ ls | where $row.knd eq folder | count
0
  explanation: No row has knd; did you mean kind?
  fix: ls | where $row.kind eq folder | count
```
