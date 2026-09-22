# CommandLineReimagined documentation

A command line where the text you type is a tree, not a string. Every word knows what
it is, every command returns a value instead of printing one, and every command can be
undone.

The terminal runs in a browser tab. The parser, the commands and the filesystem are
.NET compiled to WebAssembly, so nothing you type leaves the page.

## Start here

| Document | Read it when you want to |
| --- | --- |
| [Getting started](getting-started.md) | Open a terminal and run your first commands. |
| [Worked examples](examples.md) | Follow complete sessions, keystroke by keystroke. |
| [The command language](language.md) | Understand every syntax the parser accepts. |
| [Tables and predicates](tables.md) | Question a listing: filter, sort, count, group. |
| [The filesystem](filesystem.md) | Files as attribute records, and queries as places. |
| [Command reference](commands.md) | Look up one command's arguments and behaviour. |
| [The web terminal](web-terminal.md) | Learn the screen: chips, completion, Stop, undo. |
| [How it works](concepts.md) | See what happens between pressing Enter and the answer. |
| [Error reference](errors.md) | Find out what a message means and how to clear it. |
| [Troubleshooting](troubleshooting.md) | Fix a symptom that is not an error message. |
| [Building and testing](building.md) | Build, test, publish and deploy the project. |

## For implementers

The [decision log](decisions/README.md) records every architecture decision, the
options that were on the table and the one chosen.

The [implementation plan](plan/README.md) is the work in progress: an F# core with
failure as a value, an event-sourced store, an attribute filesystem, tables and
queries. It is written so that an agent with only this repository can execute it.

The [specification](spec/README.md) defines the language and the runtime normatively:
grammar, semantic tree, execution model, command contracts and the host interfaces.
The user documentation describes what the system does; the specification says what any
implementation of it must do.

## The idea in one minute

A traditional shell passes text between programs, and every program parses that text
again. Here the shell parses once, into a tree, and passes *values* along the pipe:

```
ls | where $row.kind eq folder | count
```

`ls` returns a table of records. `where` keeps the rows its predicate is true for,
still as a table, and `count` answers a number. Nothing was ever flattened to text and
re-split, so nothing can be mangled by a space in a file name — and nothing had to be
parsed a second time to be filtered.

Because the parse is a tree, the screen can show what the parser decided. Tap any word
in the scrollback and the terminal tells you whether it was a command, a flag, a string,
a variable, a type or punctuation. Because every command returns a value and knows how
to reverse itself, the last command can always be taken back.
