# How it works

What happens between pressing Enter and seeing an answer, and why the pieces are
arranged this way. You do not need any of this to use the terminal, but it explains
behaviour that would otherwise look arbitrary.

## The short version

```
text ──▶ parser ──▶ semantic tree ──┬──▶ tokens ──▶ coloured on screen
                                    │
                                    └──▶ evaluator ──▶ value ──▶ rendered result
                                              │          events
                                              │            │
                                              │            ▼
                                              │          store ──▶ log ──▶ projection
                                              │                              │
                                              └──▶ output sink (live lines)  └──▶ files,
                                                                                 variables,
                                                                                 where you are
```

The text is parsed once. Everything afterwards works on the tree or on values, never on
the text again.

A command does not change anything. It reads the projection, returns a value and a list
of events, and the evaluator commits them. The filesystem is what those events add up
to.

## Parsing

The grammar is written as parser combinators in F#, using FParsec, in
`Parser.FParsec/Grammar.fs`. It replaced a table-driven LALR parser generated from
`CommandLineGrammar.grm`, which is still in the repository because the two are compared
against each other in tests.

Parsing produces a **semantic tree**: nodes such as `PipedCommandList`,
`CommandExpressionCli`, `ObjectInstance`, `StringConstant` and `VariableReference`. A
failed parse produces a syntax error with a line, a column and the list of symbols the
parser could have accepted there, which is what the error message quotes.

Two visitors walk that tree:

- **The serialiser** turns it back into text. The web host runs it on every parse and
  compares the result with what you typed. If they differ, the parse lost information.
- **The tokeniser** produces a flat list of `(text, kind)` pairs, where kind is one of
  command, flag, string, variable, identifier, type, attribute, punctuation, whitespace
  or newline. That list is what the page colours, and what the token inspector reads
  when you tap a word.

This is the project's central claim in practice: the command line is not a string, so
every run of characters on screen knows what it is.

## Values, not text

A command returns a `Value`. The kinds are:

| Value | Produced by | Displays as |
| --- | --- | --- |
| Empty | a command with no result | nothing |
| None | an answer that is "there is nothing" | nothing |
| Text | `cat`, `echo "x"` | the text |
| Number | `echo 42`, `progress` | the number |
| Boolean | a flag with no value | `true` or `false` |
| File | `mkdir`, `write`, `attr`, `save`, a listing's `name` column | the record's name |
| List | `rows`, an argument that collected several | its items, space separated |
| Table | `ls`, `vars`, `attr`, `history`, `help`, every table function | a header and one line per row |
| Object | `<thing a=1/>` | the tag as written |
| Component | `{renderer/}` | the tag as written |

Every value answers two questions: how it should read to a human, and what it means as
an argument to another command. A file displays as `documents` and argues as
`/documents`. That single distinction is what makes `ls | cd` work without any quoting
rules.

## Binding arguments

The binder matches what you wrote against what the command declared. Named arguments
and flags are matched first, wherever they appear on the line; the rest fill parameters
in declaration order; anything still empty falls back to the piped value, then to a
default, then reports an error. [The command language](language.md#how-arguments-reach-parameters)
gives the exact order.

Nothing is passed as a string on the way in: an unquoted word arrives as text or a
number, `$name` arrives as whatever the variable holds, and a tag arrives as an object
or a component.

## Pipes

A pipeline is a fold. Each stage receives the previous stage's value as its input, and
the last stage's value is the result of the line. A stage that fails stops the
pipeline, and the fault reaches you instead of a result — unless the line said
otherwise. `try` in front of a stage turns its fault into its result, and `else`
between two pipelines runs the second with the first one's fault as its input. A fault
is a value like any other, which is why a variable can hold one and `$problem.kind`
can read it. See [Errors as values](language.md#errors-as-values).

The input is offered only to parameters that declared they accept it, so a command with
several parameters is not confused about which one the pipe fills.

## Output versus results

A command has two ways to say something:

- **Its result**, a value, returned at the end. It flows down the pipe and is rendered
  as chips or a text block.
- **Its output**, written while it runs, through an output sink. This is how `progress`
  updates a counter and `download` reports MB/s.

The sink is an interface. The desktop shell implements it over the entity component
system and asks the renderer for a frame on every write; the browser implements it by
collecting lines and raising an event; tests implement it by recording. Because
commands only know the interface, none of them needs a window, and all of them can be
tested headlessly. It is also why long-running commands work in WebAssembly at all.

In the browser the event is coalesced to about one update every 40 milliseconds and
pushed into the page, which is what you see moving while a command runs.

## Events, and what a line is

A command returns *events*: what it did, described. `mkdir alpha` returns "a record was
created"; `write note.txt hello` returns "a record was created" and "its content
changed from nothing to this hash". Nothing has happened yet when the command returns.

The evaluator runs a line's stages left to right, accumulating events into a working
copy of the projection so that a later stage sees an earlier one's effect — which is
what makes `mkdir scratch | cd` land in the new folder. If every stage succeeds, the
accumulated events are appended to the log as **one transaction**, named by the line
you typed. If any stage fails, nothing is appended at all.

That is the rule worth remembering: **a line is all or nothing**. `mkdir a | cd nowhere`
leaves no folder `a` behind. You do not have to know how many stages ran before the
failure in order to clean up, because nothing ran, as far as the store is concerned.

Recovery keeps the rule rather than bending it. A branch of an `else` that failed, and
a `try` stage that failed, are put back exactly as a failed line is, so
`mkdir a | cd nowhere else echo "no"` answers `no` and still leaves no `a`. What commits
is the work of the branch that produced the value, and nothing else.

A line that produces no events commits nothing. `ls` and `pwd` leave no transaction.

## Undo

The log only grows. Undoing does not remove a transaction: it appends a new one whose
events are the originals inverted and reversed, marked as compensating the one it
reverses. Redo compensates the compensation.

Every event carries both sides of its change — a content change carries the old hash
and the new one — so inverting is exact and needs nothing but the event itself.

What follows from this:

- **Undo is one line at a time, not one command at a time.** `undo` reverses the last
  line that changed something, and names it: `Undone: write note.txt second`.
- **A line that changed nothing is not in the way.** After `mkdir alpha` then `ls`,
  `undo` reverses the `mkdir`. The `ls` left no transaction to step over.
- **Redo is not a second mechanism.** It is undo applied to an undo, which is why
  `undo`, `redo`, `undo` leaves you where the first `undo` did.
- **`history` shows the lines that changed something**, oldest first, with `(undone)`
  against any whose effect is not currently in force.
- **The seed cannot be undone.** What the session started with is recorded and shown,
  but it is not a line you typed, so `undo` stops before it.

## Scope

Variables live in a scope, which can nest and shadow. Today the terminal uses one global
scope for a session, and `vars` lists everything visible from it. The mechanism exists
for nested scopes; the shell does not yet create them.

A binding is an event like any other, which is why undoing `set` restores what was
bound before, and why `set v 1`, `set v 2`, undo, undo leaves `$v` unbound rather than
back at 1.

## Where the filesystem comes from

There is no disk. A file is a record: a set of typed attributes, and optionally some
content. `name`, `kind` and `folder` are three attributes among them, and a folder is
simply a record whose `kind` is `folder`. `attr` shows them all and writes new ones.

Because a folder is only an attribute, being in one is a question — *which records say
their folder is this one?* — and any other question is a place in the same sense.
`cd $row.mood eq great` sets a view, `ls` answers it across directories, and
`save-view` keeps one as a record of kind `view`. [The filesystem](filesystem.md) is
the guide to the model.

The whole filesystem is a projection folded from the log, so it lives wherever the log
does. In the browser that is IndexedDB, for this origin, so a reload replays it and
everything comes back; in the desktop shell and in tests it is memory, and lasts as
long as the process. Content is stored by the hash of its text, so keeping the previous
version of a file costs nothing and undoing a write is just pointing at the old hash
again.

The stored shape is versioned and hand-written rather than a serialiser pointed at the
types. What is written is read back by a later build, so renaming a case or reordering
a field must not be able to make somebody's filesystem unreadable.

**The log grows with use, and nothing compacts it.** Every line that changed something
is kept for ever, which is what makes undo reach as far back as it does. A session used
heavily for a long time will replay more slowly on load. Snapshotting the projection
and keeping only the log after it is the obvious answer and is deliberately not done
yet: it is worth doing when somebody has a log big enough to notice, and not before.
`reset` is the blunt instrument in the meantime.

## The pieces, by project

| Project | Role |
| --- | --- |
| `Parser.FParsec` | The grammar and the parser (F#). |
| `Parser.Tree` | The semantic tree, the visitors and parser errors. |
| `Core` (assembly `CommandLineReimagined.Core`) | Everything the language means, in F#: values, faults, events, the store, the binder, the evaluator, every command, the session. |
| `CommandLine` (assembly `Terminal`) | The desktop shell over the entity component system. |
| `Web.Core` | The adapter from the core to JSON, plus the parse service and tokeniser shared by both web front ends. |
| `WebClient` | The WebAssembly client and its JavaScript bridge. |
| `Web` | An ASP.NET host serving the client, `/api/parse` and a WebSocket. |
| `CommandLineReimagined` | The Windows desktop shell, built on the entity component system. |

The layering rule is that the core depends on the parser and on nothing else. It knows
nothing about the entity component system, rendering, WPF, Blazor or JavaScript, and it
is what lets the same semantics run in a window, in a browser tab and in a test.
