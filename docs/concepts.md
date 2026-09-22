# How it works

What happens between pressing Enter and seeing an answer, and why the pieces are
arranged this way. You do not need any of this to use the terminal, but it explains
behaviour that would otherwise look arbitrary. The normative version, with every rule
spelled out, is the [execution model](spec/execution-model.md).

## The short version

A line goes through five stages:

```
 text you typed
      │
      ▼
   parse ─────▶ a tree: every word knows what it is ──▶ coloured on screen
      │
      ▼
   bind ──────▶ each command's arguments matched to its declared parameters
      │
      ▼
   evaluate ──▶ the pipeline folded to one value, plus a list of events
      │                                       │
      │                                       ▼ (only if every stage succeeded)
      │                                   commit ──▶ one transaction appended to the log
      │                                                            │
      ▼                                                            ▼
   the answer, rendered                                        project ──▶ files,
                                                                           variables,
                                                                           where you are
```

1. **Parse.** The text becomes a tree, once. Nothing after this looks at the text
   again.
2. **Bind.** For each command in the line, what you wrote is matched against what the
   command declared it takes.
3. **Evaluate.** The stages run left to right, each handing its value to the next. A
   command does not change anything: it returns a value and a description of what it
   would change, as events.
4. **Commit.** If the whole line succeeded, its events are appended to the log as one
   transaction. If any part failed, nothing is.
5. **Project.** The files, the variables and your location are not stored anywhere as
   such. They are what the log adds up to, folded from it event by event.

The examples on this page are one session, from a fresh tab.

## 1. Parse

The grammar is written as parser combinators in F#, using FParsec, in
`Parser.FParsec/Grammar.fs`. It replaced a table-driven LALR parser generated from
`CommandLineGrammar.grm`, which is still in the repository because the two are compared
against each other in tests.

Parsing produces a **semantic tree**: nodes such as `PipedCommandList`,
`CommandExpressionCli`, `ObjectInstance`, `StringConstant` and `VariableReference`. A
failed parse produces a syntax error with a column and the list of symbols the parser
could have accepted there, which is what the message quotes:

```
$ <thing
Syntax error at column 6: expected identifier, />, >.
```

A line that does not parse never reaches the other four stages, so it runs nothing and
records nothing.

Two visitors walk the tree:

- **The tokeniser** produces a flat list of `(text, kind)` pairs, where kind is a role
  such as command, flag, string, variable, identifier, type, attribute, operator,
  keyword or punctuation. That list is what the page colours, and what the token
  inspector reads when you tap a word.
- **The serialiser** turns the tree back into text. Both web front ends run it on every
  parse alongside the tokeniser, so a parse that lost information would show up as text
  that differs from what you typed.

This is the project's central claim in practice: the command line is not a string, so
every run of characters on screen knows what it is.

## 2. Bind

The binder, in `Core/Binder.fs`, matches what you wrote against what the command
declared. Named arguments and flags are matched first, wherever they appear on the
line; the remaining values fill parameters in declaration order; anything still empty
falls back to the piped value, then to a default, and otherwise the line fails.
[The command language](language.md#how-arguments-reach-parameters) gives the exact
order.

Binding fails before the command runs, with a fault of kind `Binding`:

```
$ write note.txt
'write' needs an argument for 'text'.

$ write note.txt hello world
'write' takes 2 arguments, but 3 were given.

$ echo hello | write note.txt
note.txt
```

The third line binds `path` from what you wrote and `text` from the pipe. The piped
value is offered only to parameters that declared they accept it, so a command with
several parameters is not confused about which one the pipe fills.

Nothing is passed as a string on the way in. An unquoted word arrives as a number if it
reads as one and as text otherwise, `$name` arrives as whatever the variable holds, a
tag arrives as an object or a component, and a pipeline in parentheses arrives as the
value it answered. An expression such as `$row.kind eq folder` is bound only to a
parameter declared as a predicate, like the one `where` takes, and it arrives
unevaluated, so the command can ask it once per row.

## 3. Evaluate

The evaluator, in `Core/Evaluator.fs`, runs the stages of a line left to right. Each
stage receives the previous stage's value as its input, and the last stage's value is
the answer. The commands themselves are in `Core/Commands`.

### Values, not text

A command returns a value. The kinds are:

| Value | Produced by | Displays as |
| --- | --- | --- |
| Empty | a command with no result | nothing |
| None | an answer that is "there is nothing", such as `first` of an empty table | nothing |
| Text | `cat`, `echo "x"` | the text |
| Number | `echo 42`, `count`, `progress` | the number |
| Boolean | a flag with no value | `true` or `false` |
| File | `mkdir`, `write`, `attr`, `save`, a listing's `name` column | the record's name |
| List | `rows`, an argument that collected several | its items, space separated |
| Table | `ls`, `vars`, `history`, `help`, every table function | a header and one line per row |
| Object | `<thing a=1/>`, a row taken out of a table | the tag as written |
| Component | `{renderer/}` | the tag as written |
| Query | `pwd` inside a view | the predicate as written |
| Fault | `try` in front of a stage that failed | its message |

Every value answers two questions: how it should read to a person, and what it means
as an argument to another command. A file displays as `scratch` and argues as
`/scratch`. That distinction is why `mkdir scratch | cd` needs no quoting and lands in
the right place.

### Commands describe, they do not change

A command reads the current state, returns a value, and returns *events*: what it did,
described. `mkdir alpha` returns "a record was created"; `write note.txt hello`
returns "a record was created" and "its content changed from nothing to this hash".
When the command returns, nothing has happened yet.

The evaluator folds each stage's events into a working copy of the state, so a later
stage sees an earlier one's effect:

```
$ mkdir scratch | cd
scratch

$ pwd
/scratch

$ up
/
```

`cd` found `scratch` in the working copy, even though no folder of that name had been
committed when it ran.

### Recovery

A stage that fails stops the pipeline, and the fault reaches you instead of a result,
unless the line said otherwise. `try` in front of a stage turns its fault into its
result, and `else` between two pipelines runs the second with the first one's fault as
its input. A fault is a value like any other, which is why a variable can hold one and
`$problem.kind` can read it. See [Errors as values](language.md#errors-as-values).

A branch of an `else` that failed, and a `try` stage that failed, are put back exactly
as a failed line is: the working copy returns to where it was before them. What goes
on to be committed is the work of the branch that produced the value, and nothing else.

### Output versus results

A command has two ways to say something:

- **Its result**, a value, returned at the end. It flows down the pipe and is rendered
  as a table, chips or a text block.
- **Its output**, written while it runs, through an output sink. This is how `progress`
  updates a counter and a bar, `download` reports its progress, and `run` shows each line
  of a script as it goes.

The sink is an interface. The desktop shell implements it over the entity component
system; the browser implements it by collecting lines and raising an event; tests
implement it by recording. Because commands only know the interface, none of them
needs a window, and all of them can be tested headlessly. It is also why long-running
commands work in WebAssembly at all. In the browser the updates are coalesced to about
one every 40 milliseconds and pushed into the page, which is what you see moving while
a command runs.

## 4. Commit

When every stage has succeeded, the store, in `Core/Store.fs`, appends the line's
events to the log as **one transaction**, named by the line as you typed it. If any
stage fails, nothing is appended at all. That is decision
[0015](decisions/0015-atomic-lines.md), and it is the rule worth remembering:
**a line is all or nothing**.

```
$ mkdir alpha | cd nowhere
Directory does not exist : nowhere

$ ls | select name
name
documents
examples
projects
scratch
note.txt
readme.txt
```

`mkdir alpha` succeeded and `cd nowhere` did not, so there is no `alpha`. You do not
have to know how many stages ran before the failure in order to clean up, because as
far as the store is concerned, none of them did.

Two more things follow:

- **A line that produces no events commits nothing.** `ls`, `cat`, `pwd` and any
  question about a table leave no transaction behind.
- **`undo`, `redo` and `history` are about the log, not the files**, so they act on the
  store directly and are not part of any line's transaction. `run` is the same: each
  line of the script commits its own transaction.

Before appending, the store checks the events against the state they will land on, so
two records with the same name in one folder cannot get into the log.

`history` is the log, as a table:

```
$ history
seq  at        source                       undone  compensates
1    09:30:00  seed                         false
2    09:30:00  echo hello | write note.txt  false
3    09:30:00  mkdir scratch | cd           false
4    09:30:00  up                           false
```

The lines that failed, the listing and `pwd` are not there. `up` is, because
moving changes your location, and your location is part of what the log records.

## 5. Project

Nothing in the session holds the files, the variables or the location as state that
commands change. `Core/Projection.fs` computes them by folding the log's events in
order, starting from nothing, and the result is called the **projection**. Every
command reads the projection; only the store appends to the log it comes from. That is
decision [0010](decisions/0010-undo-by-event-sourcing.md).

Three things follow from that arrangement.

### Undo is a compensating transaction

The log only grows. Undoing does not remove a transaction: it appends a new one whose
events are the originals inverted and in reverse order, marked as compensating the one
it reverses. Every event carries both sides of its change, so inverting one needs
nothing but the event itself. Redo compensates the compensation.

```
$ undo
Undone: up

$ pwd
/scratch

$ history
seq  at        source                       undone  compensates
1    09:30:00  seed                         false
2    09:30:00  echo hello | write note.txt  false
3    09:30:00  mkdir scratch | cd           false
4    09:30:00  up                           true
5    09:30:00  up                           false   4

$ redo
Redone: up

$ pwd
/
```

Row 5 is the undo. It is a transaction in its own right, carrying the name of the line
it reversed, and its `compensates` column holds 4, the sequence number of the
transaction it reverses. It is never itself marked undone; row 4, the `up` it reversed,
is. A redo is a row of the same kind, whose `compensates` names the undo it reverses.

- **Undo is one line at a time, not one command at a time.** `undo` reverses the last
  line that changed something, and names it.
- **A line that changed nothing is not in the way.** The `ls` and `pwd` above left no
  transaction to step over.
- **Redo is not a second mechanism.** It is undo applied to an undo, which is why
  `undo`, `redo`, `undo` leaves you where the first `undo` did.
- **The seed cannot be undone.** What the session started with is the first
  transaction, and `history` shows it, but it is not a line you typed, so `undo` stops
  before it.

A variable binding is an event like any other, so undoing `set` restores whatever was
bound before, including nothing:

```
$ set v 1
1

$ set v 2
2

$ undo
Undone: set v 2

$ undo
Undone: set v 1

$ echo $v
Unknown variable : $v
```

Variables live in a scope, which can nest and shadow. The terminal uses one global
scope for a session, and `vars` lists everything visible from it.

### A reload is a replay

The log is what is kept. In the browser it is stored in IndexedDB, for this site, and
opening the page replays it into a fresh projection before the input is enabled, so
your files, variables and location come back exactly as they were. In the desktop shell
and in tests the log is in memory and lasts as long as the process.

Content is stored separately from the log, by the hash of its text, so keeping the
previous version of a file costs nothing and undoing a write is pointing at the old
hash again.

The stored shape is versioned and hand-written rather than a serialiser pointed at the
types. What is written is read back by a later build, so renaming a case or reordering
a field must not be able to make somebody's filesystem unreadable.

**The log grows with use, and nothing compacts it.** Every line that changed something
is kept, which is what makes undo reach as far back as it does. A session used heavily
for a long time will replay more slowly on load. Snapshotting the projection and
keeping only the log after it is the obvious answer and is deliberately not done yet.
`reset` empties the log and starts again from the seed; it is the only command that
cannot be undone.

### Files are records, and a folder is a question

There is no disk. A file is a record: a set of typed attributes, and optionally some
content. `name`, `kind` and `folder` are three attributes among them, and a folder is
simply a record whose `kind` is `folder`. `attr` shows them all and writes new ones.

Because a folder is only an attribute, being in one is a question: *which records say
their folder is this one?* Any other question is a place in the same sense.
`cd $row.mood eq great` sets a view, `ls` answers it across directories, and
`save-view` keeps one as a record of kind `view`. [The filesystem](filesystem.md) is
the guide to the model.

## The pieces, by project

| Project | Role |
| --- | --- |
| `Parser.FParsec` | The grammar and the parser (F#). Stage 1. |
| `Parser.Tree` | The semantic tree, the visitors and parser errors. |
| `Core` (assembly `CommandLineReimagined.Core`) | Everything the language means, in F#: values, faults, events, the binder (`Binder.fs`), the evaluator (`Evaluator.fs`), every command (`Commands/`), the store (`Store.fs`), the projection (`Projection.fs`) and the session that ties them together (`Session.fs`). Stages 2 to 5. |
| `Web.Core` | The adapter from the core to JSON, the stored log format, and the parse service and tokeniser shared by both web front ends. |
| `WebClient` | The WebAssembly client, its JavaScript bridge and the IndexedDB log. |
| `Web` | An ASP.NET host serving the client, `/api/parse` and a WebSocket. |
| `CommandLine` (assembly `Terminal`) | The desktop shell over the entity component system. |
| `CommandLineReimagined` | The Windows desktop application, built on the entity component system. |

The layering rule is that the core depends on the parser and on nothing else. It knows
nothing about the entity component system, rendering, WPF, Blazor or JavaScript, and it
is what lets the same semantics run in a window, in a browser tab and in a test.
