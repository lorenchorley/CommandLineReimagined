# How it works

What happens between pressing Enter and seeing an answer, and why the pieces are
arranged this way. You do not need any of this to use the terminal, but it explains
behaviour that would otherwise look arbitrary.

## The short version

```
text ──▶ parser ──▶ semantic tree ──┬──▶ tokens ──▶ coloured on screen
                                    │
                                    └──▶ evaluator ──▶ runtime value ──▶ rendered result
                                              │
                                              ├──▶ output sink (live lines)
                                              └──▶ history (undo)
```

The text is parsed once. Everything afterwards works on the tree or on values, never on
the text again.

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

A command returns a `RuntimeValue`. The kinds are:

| Value | Produced by | Displays as |
| --- | --- | --- |
| Empty | a command with no result | nothing |
| Text | `cat`, `echo "x"` | the text |
| Number | `echo 42`, `progress` | the number |
| Boolean | a flag with no value | `true` or `false` |
| Path | `ls`, `cd`, `mkdir`, `write` | the entry's name |
| List | `ls`, `vars` | its items, space separated |
| Object | `<thing a=1/>` | the tag as written |
| Component | `{renderer/}` | the tag as written |

Every value answers two questions: how it should read to a human, and what it means as
an argument to another command. A path displays as `documents` and argues as
`/home/terminal/documents`. That single distinction is what makes `ls | cd` work
without any quoting rules.

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
the last stage's value is the result of the line. A stage that throws stops the
pipeline, and the error reaches you instead of a result.

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

## Undo

Every executed command is pushed onto a history stack together with its invocation: the
arguments it bound, the scope it ran in and the output it wrote. Undo pops one entry and
asks that command to reverse itself.

Three consequences are worth knowing:

- **Undo is one command at a time, not one change at a time.** Read-only commands are on
  the stack too, so undoing after `ls` undoes the `ls`, which correctly changes nothing.
  The terminal names what it undid so this is visible.
- **Each execution has its own undo state.** A command instance is created per
  execution, so undoing `set v 1` then `set v 2` unwinds both bindings in turn.
- **A long-running command undoes in two steps.** The first undo cancels it and leaves
  its output on screen; a second undo clears that output.

## Scope

Variables live in a scope, which can nest and shadow. Today the terminal uses one global
scope for a session, and `vars` lists everything visible from it. The mechanism exists
for nested scopes; the shell does not yet create them.

## Where the filesystem comes from

The commands use ordinary .NET file APIs. Under WebAssembly those calls land on
Emscripten's in-memory filesystem, which lives in the tab, so `mkdir` really does create
a directory in a real tree that a later `ls` reads back. It disappears when the tab does.

The desktop shell and the tests use the same commands against the real disk.

## The pieces, by project

| Project | Role |
| --- | --- |
| `Parser.FParsec` | The grammar and the parser (F#). |
| `Parser.Tree` | The semantic tree, the visitors and parser errors. |
| `CommandLine` (assembly `Terminal`) | Execution: values, binder, evaluator, history, scope, output interfaces. |
| `Commands` | The command implementations. |
| `Web.Core` | A terminal session, parse service and tokeniser shared by both web front ends. |
| `WebClient` | The WebAssembly client and its JavaScript bridge. |
| `Web` | An ASP.NET host serving the client, `/api/parse` and a WebSocket. |
| `CommandLineReimagined` | The Windows desktop shell, built on the entity component system. |

The layering rule is that execution depends on interfaces, not on a user interface. That
is what lets the same commands run in a window, in a browser tab and in a test.
