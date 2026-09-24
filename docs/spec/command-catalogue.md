# Command catalogue

The normative contract of each built-in command: its parameters, its result, the
events it emits and how it fails. The [user-facing reference](../commands.md) covers
the same ground with examples. How a line is parsed, bound, run and committed is the
[execution model](execution-model.md); this document assumes it.

## Reading an entry

Each entry has a table:

| Field | Meaning |
| --- | --- |
| Parameters | The declared parameters, in declaration order, which is the order positional arguments fill them. |
| Returns | The value handed to the next stage. |
| Events | What the command emits. "None" means a line of it alone commits nothing and leaves nothing for `undo`. |
| Marks | `ReadOnly`, `Meta`, both or neither (see [The command model](execution-model.md#the-command-model)). |

A parameter is annotated with how it binds:

| Annotation | Declared as |
| --- | --- |
| *piped* | `AcceptsPipe`: takes the previous stage's value when nothing was written for it. |
| *optional* | `Optional`, with its default in parentheses when it is not `Empty`. |
| *predicate* | Kind `Predicate`: arrives unevaluated as a `Query`, and the command evaluates it. |
| *rest* | Kind `Rest`: collects every remaining positional argument as a `List` ([decision 0021](../decisions/0021-variadic-parameters.md)). |
| *assignments* | Kind `Assignments`: collects every `name=value` written on the line, in order ([decision 0017](../decisions/0017-assignment-arguments.md)). |

A parameter can also be bound by name, `name: value`, or by flag, `-name` or
`-name value`; both match the parameter's name case-insensitively, and the flag also
matches a declared `Flag`. Errors are written as the message, then the fault kind in
parentheses.

What a parameter's argument is, its `Takes`, is not part of how it binds, and is listed
for every command in [What each parameter takes](#what-each-parameter-takes).

## Rules every command follows

These hold for every entry below, and an entry does not repeat them.

**Commands describe; the store applies.** A command reads the working projection and
returns a value and a list of events. It **must not** change anything itself. The
evaluator folds a line's events into one transaction and commits it only when the whole
line succeeds ([decision 0015](../decisions/0015-atomic-lines.md)), so a failed stage
leaves no trace. There is no per-command undo: reversing a line appends its events
inverted ([decision 0010](../decisions/0010-undo-by-event-sourcing.md)), so what an
`undo` does is a consequence of the events an entry lists.

**Binding faults.** These come from the binder, not the command, and apply to every
entry ([Argument binding](execution-model.md#argument-binding)):

- `'<command>' needs an argument for '<parameter>'.` (`Binding`) for a missing required
  parameter. A piped `Empty` or `None` does not count as an argument.
- `'<command>' takes N argument(s), but M were given.` (`Binding`) for leftovers; `was given` when M is 1.
- `'<command>' has no argument named '<name>'.` (`Binding`) for an unknown `name:` or
  `-name`.
- `'<command>' does not take '<name>=' assignments.` (`Binding`) for an assignment
  written to a command with no *assignments* parameter.
- `'<command>' takes a value for '<parameter>', not an expression.` (`Binding`) for an
  operator written where a parameter is not a *predicate*.

A line that fails with one of these carries the command's help beside the fault, as
[A wrong call carries its help](execution-model.md#a-wrong-call-carries-its-help)
describes ([decision 0038](../decisions/0038-a-wrong-call-shows-its-help.md)). So does a
fault of kind `Binding` that a command raises about its own arguments and words with its
name first, such as `'select' needs at least one column.`

**Paths.** The filesystem is the store's projection, not a disk
([decision 0013](../decisions/0013-attribute-filesystem.md)). A path is resolved
against the current folder, `Location.Folder`, unless it begins with `/`, and is
normalised as text: `.` is dropped, and `..` removes a segment, staying at `/` at the
root. A `File` value given as a path means its absolute path, so a file piped from one
command names the same record in the next. Names are compared ordinally and are
case-sensitive; command names are resolved case-insensitively.

Messages quote the resolved, absolute path, except `Directory does not exist : <path>`
raised by `ls` and `in`, which quotes the path as written.

**A missing path names the nearest.** Every fault for a path that is not there,
`File does not exist`, `Nothing exists at`, `Directory does not exist` and `Target
directory does not exist`, keeps its message and kind, and the line's response carries a
`suggestion` naming the nearest paths, folders only for the last two, with a fix for
each that writes it where the missing path was written
([Suggestions](execution-model.md#suggestions),
[decision 0042](../decisions/0042-a-missing-name-names-the-nearest.md)). An entry below
lists the message only.

**Records.** A folder is a record of kind `folder` with no content, and the root `/` is
implicit: it is not a record, has no attributes and cannot be deleted
([decision 0016](../decisions/0016-folders-as-records.md)). Every record a command
creates carries `name`, `kind`, `folder` (its parent's path), `created` and `modified`,
the last two as ISO 8601 round-trip timestamps from the session's clock. `size` is never
stored; it is the content's length, computed when listed. A file created without a kind
is given one from its extension, case-insensitively:

| Extension | `kind` |
| --- | --- |
| `.xml` | `xml` |
| `.csv` | `csv` |
| `.json` | `json` |
| `.clr` | `script` |
| `.md` | `markdown` |
| anything else, or none | `text` |

**Names.** One rule decides whether a name can be given to a record, whether the
record is being created or renamed:

| Name | Fault |
| --- | --- |
| empty | `A file must have a name.` (`Invalid`) |
| contains `/` | `'<name>' is not a valid file name: '/' separates directories.` (`Invalid`) |
| `.` or `..` | `'<name>' is not a valid file name: it already names a directory.` (`Invalid`) |
| already used in the folder, by a file or a folder | `Target file already exists : <path>` (`Conflict`) |

`attr`, `save` and `save-view` apply the rule themselves, so a refusal is their stage
failing and `else` can recover from it.

**Store validation.** Before committing, the store checks the events in order against
the state each lands on. A `FileCreated`, and an `AttributesChanged` that changes a
record's `name` or `folder`, **must** satisfy the name rule; this is how `mkdir ""`,
`mkdir /` and `write "" x` fail. An `AttributesChanged` that moves a record into a
folder that does not exist is refused with `Directory does not exist : <folder>`
(`NotFound`). A change that leaves both `name` and `folder` alone is not judged, so a
record from an older log whose name today's rule refuses can still be given an
attribute.

## Registration

The session registers every command in this document, and a host adds none. There are
no host-specific commands: `debug`, which needed the entity component system, was
dropped when the command layer moved to the F# core.

| Group | Commands |
| --- | --- |
| [Navigation](#navigation) | `ls`, `in`, `out`, `back`, `pwd`, `find`, `save-view` |
| [Files](#files) | `read`, `write`, `rm`, `cp`, `mkdir` |
| [Attributes](#attributes) | `attr`, `save` |
| [Values](#values) | `echo`, `set`, `vars`, `is-fault` |
| [Long-running commands](#long-running-commands) | `progress`, `download` |
| [Table functions](#table-functions) | `where`, `select`, `sort`, `take`, `skip`, `first`, `last`, `count`, `distinct`, `group`, `columns`, `rows`, `table` |
| [Documents](#documents) | `from-xml`, `to-xml`, `from-csv`, `to-csv`, `pick` |
| [The log and the session](#the-log-and-the-session) | `undo`, `redo`, `history`, `reset`, `run`, `help`, `exit`, `UnknownCommand` |

A command's name is one or more identifiers joined by hyphens with nothing either side
of each hyphen ([decision 0022](../decisions/0022-hyphenated-command-names.md)).

`UnknownCommand` **must** be registered by any session that wants an unrecognised name
reported as a normal failure; see
[Resolving a command](execution-model.md#resolving-a-command). It is not listed by
`help` or offered by completion.

## What each parameter takes

Every parameter declares what its argument is, as `Takes`
([The command model](execution-model.md#the-command-model)). Completion offers by it
([Host interfaces](host-interfaces.md#what-a-parameter-takes)), and `help <command>`
writes it in words in its `takes` column. A *predicate* parameter is written
`a predicate` and an *assignments* parameter `name=value pairs`, whatever they declare,
and a *rest* parameter adds `, any number`. A parameter that declares nothing takes
`Anything`.

| Command | Parameter | `Takes` | `help` says |
| --- | --- | --- | --- |
| `ls` | `path` | `Place` | a folder or a view |
| `in` | `TargetPath` | `Place` | a predicate |
| `find` | `predicate` | `Anything` | a predicate |
| `save-view` | `name` | `NewName` | a new name |
| `save-view` | `predicate` | `Anything` | a predicate |
| `read` | `path` | `Path` | a path |
| `write` | `path` | `Path` | a path |
| `write` | `text` | `Value` | a value |
| `rm` | `path` | `Path` | a path |
| `cp` | `sourcePathAndFile` | `Path` | a path |
| `cp` | `targetPath` | `Place` | a folder or a view |
| `mkdir` | `FolderName` | `NewName` | a new name |
| `attr` | `path` | `Path` | a path |
| `attr` | `assignments` | `Anything` | name=value pairs |
| `save` | `tag` | `Value` | a value |
| `echo` | `text` | `Value` | a value |
| `set` | `name` | `VariableName` | a variable name |
| `set` | `value` | `Value` | a value |
| `is-fault` | `value` | `Value` | a value |
| `progress` | `steps` | `Count` | a count |
| `progress` | `delay` | `Number` | a number |
| `download` | `url` | `Url` | a URL |
| `download` | `into` | `Place` | a folder or a view |
| `where` | `predicate` | `Anything` | a predicate |
| `select` | `columns` | `Column` | a column, any number |
| `sort` | `column` | `Column` | a column |
| `sort` | `desc` | `Switch("desc", Some "asc")` | desc or asc |
| `take`, `skip` | `count` | `Count` | a count |
| `distinct`, `group` | `column` | `Column` | a column |
| every table function | `table` | `Value` | a value |
| `from-xml` | `path` | `Path` | a path |
| `to-xml` | `path` | `Path` | a path |
| `to-xml` | `value` | `Value` | a value |
| `to-xml` | `root`, `row` | `Text` | text |
| `to-xml` | `declaration` | `Switch("declaration", None)` | declaration |
| `from-csv` | `path` | `Path` | a path |
| `from-csv`, `to-csv` | `delimiter` | `Text` | text |
| `to-csv` | `path` | `Path` | a path |
| `to-csv` | `value` | `Value` | a value |
| `pick` | `selector` | `Selector` | a selector |
| `pick` | `document` | `Value` | a value |
| `run` | `path` | `Path` | a path |
| `help` | `command` | `CommandName` | a command name |
| `UnknownCommand` | `name`, `nearest` | `Anything` | not listed |

`out`, `back`, `pwd`, `vars`, `undo`, `redo`, `history`, `reset` and `exit` have no
parameters.
A command added later **should** declare what each of its parameters takes; one that
does not is offered files and folders for every argument.

## Navigation

### ls

| Field | Value |
| --- | --- |
| Parameters | `path` (*optional*) |
| Returns | `Table` |
| Events | None |
| Marks | `ReadOnly` |

Lists `path`; or, when a view is set and no path is written, every record in the store
the view matches; or the current folder. The rows **must** be ordered: folders, then
files, each group ordered by name with an ordinal comparison. A folder's listing holds
only the records directly inside it.

A view's listing **must** be built from the matching records alone, so its columns are
their attributes rather than every attribute in the store. The view's predicate is
evaluated once per record, with `$row` bound to that record's row, as for `find`, and a
listing that keeps no record carries the explanation `find` would
([An empty filter explains itself](execution-model.md#an-empty-filter-explains-itself)).
The line is `ls`, which does not hold the predicate, so the explanation offers no fix.
Writing a path **must** list that folder and **must not** clear the view.

The columns **must** be `name`, `kind`, `folder`, `size` and `modified`, followed by
every other attribute any listed record carries, ordered by name with an ordinal
comparison. `created` **must not** be a column. The `name` cell **must** be the record
as a `File`, so it carries the path it argues; `size` **must** be a `Number`, the length
of the record's content in characters, and 0 where there is none. A record that lacks
one of the extra attributes has `None` in that cell.

A listing **must not** contain a parent entry. Every row of a table of records is a
record; coming out is `out`, and the host's affordance for it.

Errors: `Directory does not exist : <path>` (`NotFound`), quoting `path` as written,
also when it names a file.

### in

| Field | Value |
| --- | --- |
| Parameters | `TargetPath` (*piped*, *predicate*) |
| Returns | `File` of the folder entered, `Text` `/` for the root, or `Query` of the view entered |
| Events | `TrailPushed` of the location left, then `LocationChanged`; none when the location does not change |
| Marks | none |

Its first keyword **must** be `cd`, the name it had before
[decision 0037](../decisions/0037-in-out-back-and-read.md), so completion finds it from
the old name.

The parameter is a *predicate*, so the argument arrives unevaluated and `in` decides
what it is:

- An argument that used an operator is a **view**. `Location.View` **must** be set to
  it and `Location.Folder` **must** be left unchanged, so new files still land in the
  folder.
- An argument that is a plain operand, or a piped value, is a **name**. It is evaluated
  and resolved. A record of kind `view` **must** be entered as the view its content
  parses to, and its content **must** be a predicate, an expression with an operator in
  it, as `find` and `save-view` require; anything else **must** be resolved as a folder
  path.
- Entering a folder **must** clear `Location.View`.

The target **must** be normalised, so `in ..` yields the parent's real path. Entering
the location already held, folder and view together, **must** emit no event, so the
line commits nothing, `undo` reaches past it and the [trail](execution-model.md#the-trail)
gains nothing. Any other entry **must** emit `TrailPushed` of the location it leaves
before the `LocationChanged`, so [`back`](#back) can return there. Reversing an `in`
restores the previous location, view included, and takes that place off the trail.

Errors: `Directory does not exist : <target>` (`NotFound`), quoting the target as
written, also when it names a file; `<predicate> never reads $row, so it is the same for
every row.` (`Binding`) for an argument that used an operator and never reads `$row`;
`'<path>' does not hold a predicate : <text>` (`Invalid`) for a view record whose
content does not parse or has no operator, quoting the content trimmed. The content of
a view record is not held to the first of these (see
[Known deviations](conformance.md#known-deviations)).

### out

| Field | Value |
| --- | --- |
| Parameters | none |
| Returns | With a view set, `Text` of the current folder's path. Otherwise as `in ..`: `File` of the parent folder, or `Text` `/` at the root |
| Events | `TrailPushed` of the location left, then `LocationChanged`; none at the root with no view |
| Marks | none |

Its first keyword **must** be `up`, its name before decision 0037.

With a view set, `out` **must** clear `Location.View` and **must** leave
`Location.Folder` unchanged. Without one it moves to the parent, exactly as `in ..`.
Two `out`s from a view over a subfolder therefore leave the view, then the folder.

At the root, moving up **must** leave the location unchanged, emit nothing and **must
not** fail. Every move it does make **must** put the location it leaves on the trail
first, as `in` does.

`out` is not `ReadOnly`, because it emits `LocationChanged`; a refresh of it is refused
with `A live refresh only re-reads : out`.

### back

| Field | Value |
| --- | --- |
| Parameters | none |
| Returns | `File` of the folder gone back to, `Text` `/` for the root, or `Query` of the view gone back to; with nowhere to go, `Text` saying so |
| Events | `TrailPopped` of each place taken off the trail, most recent first, then `LocationChanged`; none when there is nowhere to go |
| Marks | none |

Goes back to where you were before the last move, like a browser's back button
([decision 0037](../decisions/0037-in-out-back-and-read.md)). The places are the
[trail](execution-model.md#the-trail) that `in` and `out` leave; `back` takes from it
and never adds to it, so each `back` goes one step further.

`back` **must** walk the trail from its most recent place and pass over a place that is
where you already are, or whose folder no longer exists, such as one deleted or renamed
since. It **must** go to the first place it does not pass over, folder and view
together, and **must** take off the trail every place it passed over and the one it went
to. Places are compared by folder and by the view's display text
(`Projection.samePlace`).

```
$ in documents
documents

$ in /examples
examples

$ back
documents

$ back
/

$ back
Nowhere further back: you are in /
```

When the walk reaches the end of the trail with nowhere to go, `back` **must not** fail
and **must** emit nothing, so the line commits nothing and the trail is left as it was.
It answers `Nowhere further back: you are in <where>`, where `<where>` is the view's
display text when a view is set and the folder otherwise.

Reversing a `back` restores the location it left and puts every place it took back on
the trail, in order, so `in documents`, `back`, `undo` is in `/documents` again with
`/` on the trail. `back` is not `ReadOnly`, as `in` and `out` are not.

### pwd

| Field | Value |
| --- | --- |
| Parameters | none |
| Returns | `Query` of the view when one is set, otherwise `Text` of the current folder's path |
| Events | None |
| Marks | `ReadOnly` |

### find

| Field | Value |
| --- | --- |
| Parameters | `predicate` (*predicate*) |
| Returns | `Table` |
| Events | None |
| Marks | `ReadOnly` |

Lists every record in the store the predicate is true of, in the same shape `ls`
produces and with the same ordering. It **must not** change the location.

The predicate is evaluated once per record, in a child scope with `$row` bound to that
record's row, exactly as `where` binds it
([execution model](execution-model.md#predicates)). A record is kept only where the
answer is `Boolean true`.

When no record is kept, the result **must** carry the explanation
[An empty filter explains itself](execution-model.md#an-empty-filter-explains-itself)
gives, read off every record in the store
([decision 0043](../decisions/0043-an-empty-filter-explains-itself.md)).

Errors: `'find' needs a predicate, such as $row.kind eq note.` (`Binding`) when the
argument used no operator; `<predicate> never reads $row, so it is the same for every
row.` (`Binding`) when it used one and never reads `$row`, with its suggestion
([Predicates](execution-model.md#predicates)); any fault the predicate raises for a
record. A predicate with an operator always answers true or false, so the run check of
decision 0033 never fails here.

### save-view

| Field | Value |
| --- | --- |
| Parameters | `name`, `predicate` (*predicate*) |
| Returns | `File` of the record created |
| Events | `FileCreated`, `ContentChanged` |
| Marks | none |

Creates a record in the current folder with `kind` `view` and content equal to the
predicate's display text, which is something a person could have typed. A view is an
ordinary record in every other respect: it appears in `ls`, and can be read with `read`,
renamed with `attr`, deleted with `rm` and entered with `in`. Reversing the line
deletes it.

Errors: `'save-view' needs a predicate, such as $row.kind eq note.` (`Binding`);
`<predicate> never reads $row, so it is the same for every row.` (`Binding`);
`A view needs a name.` (`Invalid`) for empty text; the other faults of the
[name rule](#rules-every-command-follows).

## Files

### read

| Field | Value |
| --- | --- |
| Parameters | `path` (*piped*) |
| Returns | `Text` of the file's content |
| Events | None |
| Marks | `ReadOnly` |

Its first keyword **must** be `cat`, its name before decision 0037.

A record that has never had content, such as one made by `save`, **must** read as empty
text rather than failing.

Errors: `That is a directory, not a file : <path>` (`Invalid`) for a folder or `/`;
`File does not exist : <path>` (`NotFound`).

### write

| Field | Value |
| --- | --- |
| Parameters | `path`, `text` (*piped*) |
| Returns | `File` |
| Events | New file: `FileCreated`, `ContentChanged`. Existing file: `ContentChanged`, then `AttributesChanged` updating `modified` |
| Marks | none |

The text written is the value's **display** string, so writing a table writes the table
as it reads on screen. A new file's kind is inferred from its name; an existing file
keeps the kind it has. Reversing the line restores the previous content and `modified`,
or deletes a file that did not exist.

Errors: `That is a directory, not a file : <path>` (`Invalid`);
`Directory does not exist : <parent>` (`NotFound`). Creating missing parents is **not**
permitted.

### rm

| Field | Value |
| --- | --- |
| Parameters | `path` (*piped*) |
| Returns | `Text` `Removed <name>` |
| Events | `FileDeleted`, carrying the whole record |
| Marks | none |

A folder **must** be empty to be deleted, and neither the current folder nor any folder
above it **must** be deletable. Recursive deletion is deliberately absent. Because the
event carries the record, reversing the line restores it with its attributes and
content.

Errors, checked in this order: `Cannot delete the current directory.` (`Invalid`) for
`/`; `Nothing exists at : <path>` (`NotFound`); `Directory is not empty : <path>`
(`Invalid`); `Cannot delete the current directory.` (`Invalid`) for the current folder
or one of its ancestors.

### cp

| Field | Value |
| --- | --- |
| Parameters | `sourcePathAndFile`, `targetPath` |
| Returns | `File` of the copy |
| Events | `FileCreated` |
| Marks | none |

Copies a file into a folder. The copy **must** keep the source's name and every
attribute, with `folder` set to the target and `created` and `modified` set to now, and
shares the source's content, so a copy costs no content. Overwriting is refused, and so
is a folder as the source: copying a folder would mean copying everything under it.
Reversing the line deletes the copy.

Errors: `File does not exist : <source>` (`NotFound`);
`'cp' copies files, and <source> is a directory.` (`Invalid`);
`Target directory does not exist : <target>` (`NotFound`), also when the target is a
file; `Target file already exists : <path>` (`Conflict`).

### mkdir

| Field | Value |
| --- | --- |
| Parameters | `FolderName` |
| Returns | `File` of the new folder |
| Events | `FileCreated` of a record with `kind` `folder` |
| Marks | none |

`FolderName` may be a path, but its parent **must** already exist. Reversing the line
deletes the folder.

Errors: `Directory does not exist : <parent>` (`NotFound`);
`Target directory already exists : <path>` (`Conflict`), including when the name is
taken by a file, since names are unique within a folder across files and folders
together ([decision 0016](../decisions/0016-folders-as-records.md)).

## Attributes

### attr

| Field | Value |
| --- | --- |
| Parameters | `path` (*piped*), `assignments` (*assignments*) |
| Returns | `Table` of `name` and `value` with no assignments, otherwise the updated `File` |
| Events | None with no assignments; otherwise `AttributesChanged`, and for a renamed folder an `AttributesChanged` per record under it and a `LocationChanged` when you are inside it |
| Marks | none |

With no assignments it **must** return a table with columns `name` and `value`, one row
per attribute the record carries, ordered by name with an ordinal comparison, `created`
included.

With assignments it applies them in the order written, a later one for the same name
winning, sets `modified`, emits one `AttributesChanged` carrying the whole attribute map
before and after, and returns the file. An assignment's value is evaluated like any
argument, so `n=5` stores a `Number`. Any attribute name may be written without quoting
([decision 0017](../decisions/0017-assignment-arguments.md)). Reversing the line
restores every attribute the record had.

`name` and `kind` may be written; `folder`, `created`, `modified` and `size` **must**
be refused. A new name **must** satisfy the
[name rule](#rules-every-command-follows).

Renaming a folder **must** carry what is under it: every record whose `folder` is the
folder's path or lies beneath it has that attribute rewritten to the new path, shallowest
first, without its `modified` changing, and when the current folder is at or under the
old path the location follows it. All of it is one line, so one `undo` puts everything
back ([decision 0016](../decisions/0016-folders-as-records.md)).

A change of `kind` **must** keep decision 0016 true: a folder stays a folder, empty or
not, and a file with content cannot become one. A record with no content, such as one
made by `save`, may become a folder.

`attr` is deliberately not `ReadOnly`, because it writes when given assignments.

Errors: `File does not exist : <path>` (`NotFound`);
`That is a directory, not a file : /` (`Invalid`) for the root;
`'<name>' is set by the terminal and cannot be written.` (`Invalid`) for `folder`,
`created` and `modified`; `'size' is worked out from the content and cannot be written.`
(`Invalid`); `A directory cannot change its kind : <path>` (`Invalid`);
`A file with content cannot become a directory : <path>` (`Invalid`); the faults of the
[name rule](#rules-every-command-follows).

### save

| Field | Value |
| --- | --- |
| Parameters | `tag` (*piped*) |
| Returns | `File` |
| Events | `FileCreated` |
| Marks | none |

Creates a record in the current folder from an object tag. The tag's type name becomes
`kind`, its `name` attribute's display text becomes `name`, and every other attribute is
copied across. The tag **must not** carry `folder`, `created`, `modified` or `size`,
which are refused with `attr`'s messages, so a tag cannot put a record anywhere `attr`
would refuse to. The record has no content, so `read` on it **must** return empty text
rather than failing. A tag of type `folder` therefore makes a folder. Reversing the line
deletes the record.

`save` takes its attributes from the tag and declares no *assignments* parameter
([decision 0027](../decisions/0027-save-takes-a-tag.md)), so `save note name=x` is
`'save' does not take 'name=' assignments.` (`Binding`).

Errors: `A saved tag needs a 'name' attribute.` (`Invalid`), also for an empty name;
`'<name>' is set by the terminal and cannot be written.` (`Invalid`);
`'size' is worked out from the content and cannot be written.` (`Invalid`); the other
faults of the [name rule](#rules-every-command-follows); `'save' needs a tag, not
<kind>.` (`Invalid`) for anything but an object tag, a component tag included.

## Values

### echo

| Field | Value |
| --- | --- |
| Parameters | `text` (*piped*) |
| Returns | the value it was given, unchanged |
| Events | None |
| Marks | `ReadOnly` |

`echo` **must not** convert its argument. A number stays a number and a list stays a
list; this is how a test or a user inspects what a pipe carries.

### set

| Field | Value |
| --- | --- |
| Parameters | `name`, `value` (*piped*) |
| Returns | the bound value |
| Events | `VariableChanged`, carrying the previous binding |
| Marks | none |

The name **must** be one or more letters, digits and underscores, written without the
`$`. Returning the value lets `set` sit mid-pipeline. Reversing the line restores the
previous binding, or unbinds a new name; two `set`s of one name undone in turn leave it
unbound.

Errors: `'<name>' is not a valid variable name.` (`Invalid`);
`'set' needs a value for '<name>'.` (`Binding`) when the value is `Empty` or `None`.

### vars

| Field | Value |
| --- | --- |
| Parameters | none |
| Returns | `Table` of `name` and `value` |
| Events | None |
| Marks | `ReadOnly` |

One row per variable in scope, ordered by name, its `value` cell the value itself, so a
table reads as `4 rows`, a list as `2 items`
([Displaying a table](execution-model.md#displaying-a-table)) and a fault as its message. With nothing bound it **must** still
return a table, so that `vars | count` is 0 rather than a fault, and writes one output
line, `No variables. Try: set greeting hello`. It does not use the one-line summaries
completion gives a variable ([Host interfaces](host-interfaces.md#a-value-in-one-line)).

### is-fault

| Field | Value |
| --- | --- |
| Parameters | `value` (*optional*, *piped*) |
| Returns | `Boolean`: whether the value is a `Fault` |
| Events | None |
| Marks | `ReadOnly` |

A script that branches on whether something worked needs a question it can ask
without knowing what success would have looked like. A `Fault` value exists only where
`try` made one ([Recovery](execution-model.md#recovery)). Given nothing at all, the
answer **must** be `false`.

## Long-running commands

Both write to the invocation's output while they run and update what they wrote in
place. Both **must** observe the invocation's cancellation token and, when cancelled,
fail with `Stopped.` (`Cancelled`), which neither `try` nor `else` recovers from
([decision 0024](../decisions/0024-stop-is-not-recoverable.md)). A cancelled line
commits nothing.

### progress

| Field | Value |
| --- | --- |
| Parameters | `steps` (*optional*, 100 when not written), `delay` (*optional*, 100 ms when not written) |
| Returns | `Number`, the percentage reached |
| Events | None |
| Marks | none |

Writes two output lines, a percentage and a bar of one `=` per four percent followed by
`>`, then takes `steps` steps `delay` milliseconds apart, updating both after each. It
checks the token before every step. On completion it writes `Progress test finished`;
on cancellation it writes `Cancelled at N%` and fails with `Stopped.`.

This command exists to exercise the asynchronous path. It **must not** touch the
store.

Errors: `'steps' must be at least 1.` (`Invalid`);
`'delay' must be zero or more, not '<value>'.` (`Invalid`);
`'<parameter>' must be a whole number, not '<value>'.` (`Invalid`) for a `steps` or a
`delay` that is not a whole number, including a bare `-steps` flag, which binds `true`.

### download

| Field | Value |
| --- | --- |
| Parameters | `url` (*optional*, a small file in this repository when not written), `into` (*optional*, the current folder when not written) |
| Returns | `File` |
| Events | Exactly what `write` emits: new file, `FileCreated`, `ContentChanged`; existing file, `ContentChanged`, `AttributesChanged` updating `modified` |
| Marks | none |

Fetches `url` over the session's `HttpClient` into a file in `into`, named by the last
segment of the URL's path, with its kind inferred from that name. An existing file of
that name has its content replaced, as `write` replaces it; reversing the line restores
it, or deletes a file that did not exist.

The response body is read whole and written in one step, so a cancelled or failed
download leaves no partial file ([decision 0015](../decisions/0015-atomic-lines.md)).
It writes three output lines, a percentage, a bar and the size in MB, and updates them
once the body has arrived, then writes `Downloaded to <path>`.

Errors: `Not a valid URL : <text>` (`Invalid`), also for a URL whose path has no last
segment; `Target directory does not exist : <path>` (`NotFound`);
`The server did not report a content length.` (`Invalid`);
`That is a directory, not a file : <path>` (`Invalid`) when a folder has the name;
`Download failed : <reason>` (`Invalid`) for any other failure of the transfer.

## Table functions

Thirteen commands over tables. Each one **must**:

- declare an optional *piped* parameter `table`, last, that carries the table, so the
  function's own arguments are filled positionally and the table arrives through the
  pipe;
- coerce whatever it is given per
  [Reading a tag as a table](execution-model.md#reading-a-tag-as-a-table)
  ([decision 0009](../decisions/0009-table-coercion.md)), raising
  `'<command>' needs a table, not <kind>.` (`Binding`) when the value is not a table, a
  tag or a list, and `<describe> is not a table: child <n> <reason>.` (`Invalid`) when
  a tag or list is not table-shaped;
- match a column name case-insensitively, and raise
  `'<command>' has no column named '<name>'.` (`NotFound`) for a column it was asked for
  and the table does not have;
- be `ReadOnly` and emit no events.

| Name | Parameters before `table` | Returns |
| --- | --- | --- |
| `where` | `predicate` (*predicate*) | the rows the predicate answered `Boolean true` for |
| `select` | `columns` (*rest*) | those columns, in the order named |
| `sort` | `column`, `desc` (*optional*, flag `desc`) | the rows ordered by the column |
| `take` | `count` | the first `count` rows |
| `skip` | `count` | every row after the first `count` |
| `first` | none | the first row as an `Object` of type `row`, or `None` |
| `last` | none | the last row as an `Object` of type `row`, or `None` |
| `count` | none | a `Number` |
| `distinct` | `column` (*optional*) | unique rows, or that column's unique values as a one-column table |
| `group` | `column` | a table of `key` and `rows`, one row per distinct value |
| `columns` | none | a table of `name` and `type` |
| `rows` | none | a `List` of `Object` rows of type `row` |
| `table` | none | the table itself |

`where` **must** evaluate its predicate in a child scope with `$row` bound to the row
as an `Object` of type `row` ([decision 0008](../decisions/0008-explicit-row-variable.md)),
and keep the row only where the answer is `Boolean true`, preserving order. A variable
named `row` outside the predicate is left alone. The predicate is held to the two
checks of [decision 0033](../decisions/0033-a-predicate-is-a-question-about-the-row.md)
([Predicates](execution-model.md#predicates)): one that used an operator and never reads
`$row` is `<predicate> never reads $row, so it is the same for every row.` (`Binding`),
and the first row it answers anything but true, false or a gap for is
`<predicate> is <kind> (<value>), not true or false.` (`Invalid`), each with its
suggestion. Unlike `find`, `where` does not refuse an operand with no operator, so
`where $row.done` keeps the rows whose `done` is true.

When the table has rows and `where` keeps none, its result **must** carry the
explanation [An empty filter explains itself](execution-model.md#an-empty-filter-explains-itself)
gives ([decisions 0043](../decisions/0043-an-empty-filter-explains-itself.md) and
[0045](../decisions/0045-a-near-value-offers-a-fix.md)), and is still the empty table:

```
$ ls | where $row.knd eq folder
name  kind  folder  size  modified
  explanation: No row has knd; did you mean kind?
  fix: ls | where $row.kind eq folder

$ ls | where $row.kind eq foldr
name  kind  folder  size  modified
  explanation: kind is folder or text
  fix: ls | where $row.kind eq folder
```

`select` with no columns **must** raise `'select' needs at least one column.`
(`Binding`) rather than answering an empty table. The *rest* parameter never takes the
pipe, so `ls | select` reaches this message. A column named twice appears twice.

`sort` **must** be stable in both directions: rows that compare equal keep the order
they arrived in, so an implementation **must not** reverse an ascending sort to
descend. Cells compare by the [comparison](execution-model.md#comparison) rules, so a
number column sorts numerically. `desc` is a switch with a word for each direction:
`desc` or the flag `-desc` sorts descending, `asc` ascending, and the words match
case-insensitively. Any other word is
`'sort' takes 'desc' or 'asc' for 'desc', not '<word>'.` (`Binding`).

`take` and `skip` beyond the end of the table **must** answer everything and nothing
respectively, not a fault. A count that is not a whole number at least zero raises
`'count' must be a whole number, not '<value>'.` (`Invalid`).

`distinct` compares rows, or the one column's cells, by their display text, and keeps
the first of each. The one-column table's column is named as written.

`group` groups by the display text of the column's cells, so a number and the text of
that number are one group. Each `key` is the first grouped row's cell; each `rows` cell
**must** be a whole table with the input's columns; and the groups **must** be in the
order their values first appeared.

`columns` names each column's type as one of `text`, `number`, `boolean`, `file`,
`object` and `mixed`.

A row built for `first`, `last` or `rows`, or bound to `$row` by `where`, **must** carry
the columns in the table's order and **must** omit a cell that is `None` or `Empty`.

## Documents

Four commands over XML and CSV files ([decision 0011](../decisions/0011-real-xml-files.md)),
and `pick`, which reads any tag as a document, whether it was typed or read from XML
([decision 0049](../decisions/0049-pick-selects-elements-with-css-selectors.md)).
The readers **must** resolve and read their file exactly as `read` does, raise the same
faults for a missing path or a folder, be `ReadOnly` and emit no events. The writers
**must** emit exactly the events `write` would for the text they serialise, so that
undo, redo and history treat a document like any other file; a file they create **must**
have kind `xml` or `csv` whatever its extension, and a file they overwrite keeps its
kind. A writer's value is serialised before anything is written, so a value it cannot
write leaves nothing behind.

| Name | Parameters | Returns | Marks |
| --- | --- | --- | --- |
| `from-xml` | `path` (*piped*) | the root element as an `Object` | `ReadOnly` |
| `to-xml` | `path`, `value` (*piped*), `root` (*optional*), `row` (*optional*), `declaration` (*optional*) | `File` | none |
| `from-csv` | `path` (*piped*), `delimiter` (*optional*, `,`) | `Table` | `ReadOnly` |
| `to-csv` | `path`, `value` (*piped*), `delimiter` (*optional*, `,`) | `File` | none |
| `pick` | `selector`, `document` (*optional*, *piped*) | `Table` | `ReadOnly` |

The writers' options are reached by flag in practice: `to-xml out.xml -root listing
-row entry -declaration`. `declaration` is a switch: the bare flag, or the word
`declaration`, turns it on, and any other word is
`'to-xml' takes '-declaration' on its own, not '<word>'.` (`Binding`).

### Reading XML

`from-xml` **must** parse the content as XML 1.0 and build a `Tag` per element:

- the element's name, as written with any prefix, is the `TypeName`;
- each attribute, as written with any prefix and including namespace declarations, is
  an attribute, in document order, its value read by the number-or-text rule a bare
  word is read by;
- the element's own text nodes, CDATA included, that are not only whitespace, trimmed
  and joined with one space, are an attribute `text`, typed by the same rule, replacing
  any XML attribute of that name
  ([decision 0025](../decisions/0025-xml-text-content.md));
- child elements are the children, in document order.

Comments and processing instructions **must** be dropped. A document with a DTD
**must** be refused. A document that does not parse, the empty file and a document with
a DTD included, **must** raise
`Not well-formed XML : <path> line <n>, position <m>` (`Invalid`), with the parser's
line and position, at least 1; the parser's own sentence **must not** be part of the
message, since it differs between hosts.

### Writing XML

`to-xml` **must** write:

- a `Tag` (`Object` or `Component`) as itself, renamed to `root` when given;
- a `Table` as a root named `root`, or `table`, with one child per row named `row`,
  or `row`, carrying the row's cells as attributes, a `None` cell omitted;
- a `List` as a root named `root`, or `list`, around its items, each of which **must**
  be a tag.

Anything else, or a list item that is not a tag, **must** raise `'to-xml' needs a tag, a
table or a list of tags, not <kind>.` (`Binding`). A name that is not an XML name
**must** raise `'<name>' is not a name XML allows.` (`Invalid`).

The output **must** be indented two spaces per level, one element per line, with each
line ending in `\n`. An attribute that is `None` or `Empty` is omitted. An attribute
named `text` **must** be written as the element's content, before its children; an
element with content and no children is written on one line. `&`, `<`, `>` and `\r`
are escaped in content, and additionally `"`, `\n` and `\t` in attribute values.
Values are written by their file text: a `Number` in its shortest exact form, anything
else by its display string. The declaration `<?xml version="1.0" encoding="UTF-8"?>` is
written only when `declaration` is.

### Reading CSV

`from-csv` **must** read RFC 4180: records end in `\n` or `\r\n`; a field in double
quotes may hold the delimiter, `""` for a quote, and line breaks; the line break after
the last record ends it. The first record is the header, and its fields name the
columns; an empty name raises `<path> column <n> has no name.`, and two names equal
ignoring case raise `<path> has two columns named '<name>'.`.

Every other record **must** have as many fields as the header, or raise `<path> line
<n> has <m> fields where the header has <k>.`, where `n` is the line the record began
on. A record that is one empty unquoted field is skipped when the header has more than
one column. An unclosed quote raises `<path> line <n>: a quoted field is never closed.`
and text after a closing quote `<path> line <n>: a closing quote is followed by more
text.`. All of these are `Invalid`.

An unquoted empty field **must** read as `None`. A column **must** be numeric, every cell
a `Number`, when every field in it that is not `None` reads as a number by the
number-or-text rule; otherwise every such field is `Text`. The empty file is the empty
table.

### Writing CSV

`to-csv` **must** coerce its value as a table function does, raising `'to-csv' needs a
table, not <kind>.` (`Binding`). It writes the header, then a record per row, each line
ending in `\n`, the last included. A cell that is `None` or `Empty` is an empty field. A
field **must** be quoted, with `"` doubled, when it contains the delimiter, `"`, `\n` or
`\r`, or is empty text, and **must not** be quoted otherwise. Values are written by
their file text, as for XML. A table with no columns is the empty file.

A `delimiter` is one character other than `"`, `\n` and `\r`, or the word `tab`;
anything else raises `'delimiter' must be one character, or 'tab', not '<text>'.`
(`Invalid`).

### Picking elements

`pick <selector>` reads what flows in as one or more documents and answers a table of
every element the selector matches
([decision 0049](../decisions/0049-pick-selects-elements-with-css-selectors.md)). It
emits no events and is `ReadOnly`, so a live listing may re-run it. Its keywords are
`select`, `query`, `css`, `find`, `xpath`, `elements`, `descendants` and `search`, so
`xpath` and `css` typed as commands are suggested `pick`; `select`, a command of its
own, does not have `pick` among its keywords. The selector is read before the document,
so a selector outside the subset is its own fault whatever was piped.

The examples below use `$d`, set as in the [execution model](execution-model.md#values)
to this document:

```
<library city=paris><book title=dune year=1965><author name=herbert/></book><book title=emma year=1815><author name=austen/></book><shelf/></library>
```

#### The selector

`selector` is an ordinary argument. The command line's grammar knows nothing of
selectors, so one with a space, `>`, `,`, `[` or a quote in it is written as a string,
`pick "book > author"`, while `pick book` and `pick *` are words. `pick` **must** read
the argument's display string by this grammar, and accept nothing outside it:

```
Selector   ::= Space* Chain ( "," Space* Chain )*
Chain      ::= Compound ( Combinator Compound )* Space*
Combinator ::= Space* ">" Space*                       -- child
             | Space+                                  -- descendant
Compound   ::= ( Name | "*" ) Test*
             | Test+
Test       ::= "[" Space* Name Space* ( "]" | Operator Space* Value Space* "]" )
Operator   ::= "=" | "^=" | "$=" | "*="
Value      ::= '"' ( any character but '"' )* '"'
             | "'" ( any character but "'" )* "'"
             | ( any character but white space, "[", "]", '"' and "'" )+
Name       ::= NameStart NameChar*
NameStart  ::= a letter | "_"
NameChar   ::= a letter | a digit | "_" | "-"
Space      ::= any white space character
```

A `Name` in a `Compound` is a type, and matches an element of that name; `*` matches any
element, and so does a compound of tests alone. In a `Test` the name is an attribute's:
`[a]` holds when the element has the attribute, `[a=v]` when its value is `v`, `[a^=v]`
when the value begins with `v`, `[a$=v]` when it ends with `v`, and `[a*=v]` when it
contains `v`. Every test of a compound must hold. `A B` matches a `B` anywhere inside an
`A`, and `A > B` a `B` directly inside one. Chains joined by commas are groups: the
selector matches an element any of them matches.

Letters and digits are as Unicode classes them. A name has no `.` or `:`, which CSS
gives other meanings, so an element whose name holds one, such as a prefixed XML name,
is matched only by `*` or by a test. A quoted value may be empty; a bare one may not, and
may hold any character but those listed, `=`, `>` and `,` included.

#### Matching

- Each document is walked from its root, the root included, in document order: an
  element, then each of its children that is a tag, in order, each walked the same way.
  Children that are not tags are passed over.
- An element is answered once, however many groups match it, at its place in document
  order: `$d | pick "shelf, book, shelf"` answers the two books and then the shelf.
- Names and attribute values **must** be compared exactly, ordinally and with case
  significant, as XML compares them: `$d | pick BOOK` matches nothing. An attribute's
  value is compared as its display string, so `[year=1965]` finds the number the tag
  notation or `from-xml` read.
- A prefix, suffix or substring test with an empty value, `[a^='']`, `[a$='']` or
  `[a*='']`, matches nothing, as in CSS. `[a='']` matches an attribute whose display
  string is empty.
- A chain is read from its last compound back towards the root, as a browser reads one:
  `>` needs the parent to match what is before it, and a space needs some ancestor to.
  Nothing above a document's root is visible to a selector, so
  `$d | pick "library > author"` matches nothing.

#### What it reads

The value piped in, or written for `document`, **must** be read as documents:

| Value | Documents |
| --- | --- |
| An object or a component | One, whose root is that tag. |
| A list | One per item, in order; every item **must** be a tag. The empty list is none. |
| A table with a column `@tag`, matched ignoring case | One per row, the row read back as an element: `@tag` is its name, as the cell's display string; `@children` its children, a list's items, none for a gap, and a single value of any other kind as its one child; every other column an attribute, in column order, gaps left out. |

So `pick` reads what `pick` answered, and `$d.@children | pick "*"` searches the
library's three children as three documents:

```
$ $d.@children | pick "*" | select @tag
@tag
book
author
book
author
shelf

$ $d | pick book | pick author | select name
name
herbert
austen
```

A document read from XML is a tag like any other:

```
$ $d | to-xml library.xml
library.xml

$ from-xml library.xml | pick "book > author" | select name
name
herbert
austen
```

#### Each element once

When the documents given overlap, each element **must** be answered once
([decision 0052](../decisions/0052-pick-answers-each-element-once.md)). A document that
sits inside another document given is not searched again, and neither is an element
given twice; the elements come in the order of the first document each was found in.
The rows of `pick "*"` overlap, since a book is a row of its own and also inside the
library's row, and picking from them finds each book once:

```
$ $d | pick "*" | pick "[year^=19]" | select title
title
dune
```

What is compared is which element a document is, not what it says. Two elements that
are equal but separate, such as the two authors of
`<pair><author name=x/><author name=x/></pair>`, are two, as documents in a list, as
children of one document, and as rows.

A row stands for the element it was made from. The reference implementation keeps that
link beside the table rather than in it, keyed by the row's `@children` cell
(`Selector.madeFrom` in `Selector.fs`), so the answer has no column it does not show.
The link travels with the row through `where`, `sort`, `select`, `take`, `skip` and a
variable. It is lost, and the row stands for itself, a new element with the row's name,
attributes and children, in two cases:

- **A table restored after a reload.** A table held in a variable is rebuilt from the
  stored log, and its rows can no longer say which element they came from. The rows of
  `pick "*"` are then separate documents, and an element inside more than one of them
  is answered once for each: after a reload, with `$all` set to `$d | pick "*"` before
  it, `$all | pick "[year^=19]"` answers `dune` twice. This falls short of decision 0052
  ([Known deviations](conformance.md#known-deviations)).
- **A row whose `@children` was dropped**, as by `select @tag title year`. Such a row
  has no children, so it is a document of one element that no combinator can reach
  past: `$d | pick "*" | select @tag title year | pick "library book"` matches nothing.

#### What it answers

A `Table` with:

- `@tag` first, the element's name as `Text`;
- then every attribute of the elements matched, each name once, in the order the names
  first appear, each element's attributes taken in its own order, with a gap, `None`,
  where an element lacks one;
- then `@children` last, the element's children as a `List`, in order, the empty list
  for none.

One row per element matched, in the order [Matching](#matching) gives. Columns are typed
from their cells as every table's are ([Tables](execution-model.md#tables)): `@tag` is
`text`, an attribute's column is typed by its values, and `@children` is `text`, since a
list is not a number, a boolean, a file or a tag. A table draws each `@children` cell
summarised ([decision 0051](../decisions/0051-a-list-in-a-table-cell-is-summarised.md)),
and `$row.@tag` and `$row.@children` read the columns
([Evaluating a written value](execution-model.md#evaluating-a-written-value)), so a
predicate filters by value what a selector picked by shape:

```
$ $d | pick book
@tag  title  year  @children
book  dune   1965  1 child
book  emma   1815  1 child

$ $d | pick book | columns
name       type
@tag       text
title      text
year       number
@children  text

$ $d | pick book | where $row.year gt 1900 | select title
title
dune
```

When nothing matches, and when there are no documents, the answer **must** be the empty
table with the columns `@tag` and `@children` only, both `text`:

```
$ $d | pick nothing
@tag  @children
```

#### Faults

A selector outside the grammar is a fault of kind `Syntax` that says where it stopped:

```
The selector '<selector>' stops <where>: <why>.
```

`<where>` is `at its end`, or `at character <n>, '<c>'`, counting characters from one
and naming the character. `<why>` is one of these:

| Where it stopped | `<why>` |
| --- | --- |
| Where a compound should begin, at the end or at a digit, `,` or `>` | `an element name, '*' or '[' is expected <after>`, where `<after>` is `at the start`, `after '>'`, `after a space` or `after ','` |
| Where a compound should begin, at any other character; or after a compound, at anything but white space, `>`, `,` or the end | `'<c>' is not part of the CSS that pick reads: names, *, [attribute] tests, spaces, > and commas` |
| After `[`, where there is no attribute name | `an attribute name is expected after '['` |
| After an attribute name, at `~=` or `\|=` | `'<c>=' is not part of the CSS that pick reads, whose tests are =, ^=, $= and *=` |
| After an attribute name, at the end | `']' is expected to close '['` |
| After an attribute name, at anything but `]` or an operator | `']' or one of =, ^=, $= and *= is expected after the attribute name` |
| After an operator, at the end or where no bare value begins | `a value is expected after '<operator>'` |
| At the opening quote of a value that never closes | `the quote is not closed` |
| After a value, at anything but `]` | `']' is expected after the value` |

A selector that is empty or only white space is
`The selector is empty: write an element name, such as 'book', or '*' for every element.`
(`Syntax`).

```
$ $d | pick "book >"
The selector 'book >' stops at its end: an element name, '*' or '[' is expected after '>'.
  [Syntax]

$ $d | pick "[title~=dune]"
The selector '[title~=dune]' stops at character 7, '~': '~=' is not part of the CSS that pick reads, whose tests are =, ^=, $= and *=.
  [Syntax]

$ $d | pick "#id"
The selector '#id' stops at character 1, '#': '#' is not part of the CSS that pick reads: names, *, [attribute] tests, spaces, > and commas.
  [Syntax]
```

A value `pick` cannot read as documents is a `Binding` fault naming what it was:

```
'pick' needs a tag, a list of tags or a table with a @tag column, not <what>.
```

`<what>` is the value's kind (`empty` when nothing flows in, `number`, `text` and so
on), `a list whose item <n> is <kind>`, counting items from one, or
`a table without a @tag column`. The message begins with the command's name, so the line
carries `pick`'s help
([decision 0038](../decisions/0038-a-wrong-call-shows-its-help.md)). A row of an element
table whose `@tag` is a gap is
`'pick' cannot read row <n> as an element: its @tag is empty.` (`Invalid`), counting
rows from one.

```
$ pick book
'pick' needs a tag, a list of tags or a table with a @tag column, not empty.
  [Binding]

$ ls | pick book
'pick' needs a tag, a list of tags or a table with a @tag column, not a table without a @tag column.
  [Binding]
```

## The log and the session

These commands are `Meta`: they run outside the line's transaction, and any events they
returned would not be committed. `undo`, `redo`, `reset` and `run` reach the store
through a `StoreAccess` capability that no other command is given; `help` is handed the
command list instead.

### undo

| Field | Value |
| --- | --- |
| Parameters | none |
| Returns | `Text` `Undone: <source>`, or `Nothing to undo.` |
| Events | None; appends a compensating transaction through `StoreAccess` |
| Marks | `Meta` |

Reverses the latest undoable, uncompensated line that is not itself a compensation
([Undo](execution-model.md#undo)). A line that moved the location, such as `in`, `out`
or `back`, is a line like any other. The seed is not undoable
([decision 0018](../decisions/0018-the-seed-is-not-a-line-anyone-typed.md)), so `undo`
in a fresh session answers `Nothing to undo.` Having nothing to undo **must not** be a
fault.

### redo

| Field | Value |
| --- | --- |
| Parameters | none |
| Returns | `Text` `Redone: <source>`, or `Nothing to redo.` |
| Events | None; appends a compensating transaction through `StoreAccess` |
| Marks | `Meta` |

Reverses the latest undo that has not itself been reversed, and **must** name the
original line rather than the undo. Having nothing to redo **must not** be a fault.

### history

| Field | Value |
| --- | --- |
| Parameters | none |
| Returns | `Table` of `seq`, `at`, `source`, `undone` and `compensates` |
| Events | None |
| Marks | `Meta`, `ReadOnly` |

One row per transaction in the log, oldest first, the seed included. `seq` is a
`Number`, `at` the time as `HH:mm:ss`, `source` the line as it was written, and `undone`
a `Boolean` that is true where the line's effect is not currently in force, and
`compensates` the `seq`, as a `Number`, of the transaction a compensating transaction
reverses, or `None` for an ordinary line. An undo or redo is a row in its own right,
whose `source` is the line it reversed and whose `compensates` names the row it
reversed: an undo names the original line, and a redo names the undo. A compensating
row is never itself marked undone. Lines that failed, lines that changed nothing and
meta commands leave no row.

### reset

| Field | Value |
| --- | --- |
| Parameters | none |
| Returns | `Text`: `Reset. <n> files restored.`, `Reset. 1 file restored.`, or `Reset. The filesystem is empty.` |
| Events | None; empties the log and seeds it again through `StoreAccess` |
| Marks | `Meta` |

`n` counts every record the seed creates, folders included. `reset` is the only
operation that removes anything from the log, and it **must not** be undoable: the
transactions that would have been reversed are the ones it threw away, and `undo`
straight after it answers `Nothing to undo.` Its description **must** say so, so `help`
warns before rather than after
([Starting and starting over](execution-model.md#starting-and-starting-over)).

### run

| Field | Value |
| --- | --- |
| Parameters | `path` (*piped*) |
| Returns | the value of the last line executed, or `Empty` when none ran |
| Events | None of its own; each line commits its own transaction |
| Marks | `Meta` |

Runs a script ([decision 0020](../decisions/0020-scripts-and-run.md)). Reads the file
as text, treats `\r\n` as `\n`, and splits it into lines. A line that is empty after
trimming, or whose first non-space character is `#`, **must** be skipped and **must**
still be counted.

Each remaining line **must** be parsed and executed exactly as if typed, committing its
own transaction, with `Transaction.Source` set to the line's trimmed text, so `undo`
after a script reverses its last line. `run` itself **must** emit no events.

Each line **must** be written to the output as `> <line>` before it runs, followed by
the display string of its result when that is not empty.

Execution **must** stop at the first fault, and the fault **must** be re-raised with
the message `<path> line <n>: <message>`, keeping its kind, where `path` is the script's
absolute path and `n` counts every line in the file. A fault from a nested script
carries each script's prefix, outermost first. Cancellation **must** be observed between
lines. Running a script while eight are already running is
`Scripts are only allowed to run scripts 8 deep.` (`Invalid`).

Errors: `File does not exist : <path>` (`NotFound`);
`That is a directory, not a file : <path>` (`Invalid`).

### help

| Field | Value |
| --- | --- |
| Parameters | `command` (*optional*) |
| Returns | Without `command`, `Table` of `name`, `parameters` and `description`. With it, `Table` of `name`, `required`, `piped`, `takes` and `description` |
| Events | None |
| Marks | `Meta`, `ReadOnly` |

Without `command`, one row per registered command except `UnknownCommand`, ordered by
name with an ordinal comparison. `parameters` **must** be the declared parameters in
order, separated by one space, each written `name...` when it is *rest*, `[name]` when
it is optional and `<name>` otherwise; a table function's `[table]` and `attr`'s
`[assignments]` are listed like any other.

With `command`, matched case-insensitively, one row per parameter of that command in
declaration order, and one output line before the table, the command's description.
The description is written to the output rather than put in the value, so the value
stays a table and `help where | count` counts parameters:

```
$ help sort
Order the rows by a column
name    required  piped  takes        description
column  true      false  a column     The column to order by
desc    false     false  desc or asc  Write 'desc' to order downwards
table   false     true   a value      The table to work on; taken from the pipe when it is not written
```

`required` is a `Boolean`, true when the parameter is not optional; `piped` is a
`Boolean`, true when it takes the pipe; `takes` is what it takes, in the words of
[What each parameter takes](#what-each-parameter-takes); `name` and `description` are
`Text`. A command with no parameters answers a table with these columns and no rows.

Errors: `Unknown command : <name>` (`UnknownCommand`) for a name that is not a command,
`UnknownCommand` included, with a suggestion naming the nearest commands as
[Resolving a command](execution-model.md#resolving-a-command) gives them, keywords
included, and a fix for each that writes it in place of the name:

```
$ help lss
Unknown command : lss
  [UnknownCommand]
  suggestion: Did you mean ls?
  fix: help ls

$ help cd
Unknown command : cd
  [UnknownCommand]
  suggestion: Did you mean in?
  fix: help in
```

What `help <command>` answers, its output line and its table, is also what a line that
called that command wrongly carries as its guide
([A wrong call carries its help](execution-model.md#a-wrong-call-carries-its-help)).

### exit

| Field | Value |
| --- | --- |
| Parameters | none |
| Returns | `Empty` |
| Events | None |
| Marks | `Meta` |

Calls the session's `Exit` function, which the host supplies. A command **must not**
reach a window or a process directly; what shutting down means is the host's decision,
and a host with nothing to close **may** do nothing, as the browser does.

### UnknownCommand

| Field | Value |
| --- | --- |
| Parameters | `name` (*optional*), `nearest` (*rest*) |
| Returns | never returns |
| Events | None |
| Marks | `Meta` |

Raises `Unknown command : <name>` (`UnknownCommand`), and nothing more in the message
([decision 0041](../decisions/0041-guidance-is-drawn-apart-from-output.md)). When there
are near names, at most three of them, the fault carries a `suggestion` note,
`Did you mean <names>?`, with a `Replace(<name>, <near>)` fix for each
([Suggestions](execution-model.md#suggestions)). The evaluator runs it with the name
that did not resolve bound to `name` and the command names a slip away bound to
`nearest`, nearest first. The names in the note **must** be the nearest
[Resolving a command](execution-model.md#resolving-a-command) defines, which read
keywords before slips, so an old name leads to the new one: `cd` is suggested `in`. The
reference session registers a variant that
works them out itself from the registered commands and their keywords, because the
evaluator knows only their names; it does not read `nearest`. It is not resolvable
by name, so typing `UnknownCommand` is itself an unknown command,
`Unknown command : UnknownCommand`, and it is not listed by `help` or offered by
completion.

## Known deviations

None. `ls` and `in` quoting a missing folder as written rather than resolved, noted
under [Paths](#rules-every-command-follows), is deliberate and pinned by
`FilesTests.AMissingFolderIsNamedAsItWasWritten`; the nearest folders it names are
looked for from the resolved path. Where an explanation's fix falls short of the
records, and where `pick` answers an element twice from a table restored after a
reload, [Conformance](conformance.md#known-deviations) lists it.
