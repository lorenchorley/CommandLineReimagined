# The web terminal

A guide to the screen itself: every control, what it does, and the behaviour that is
the page's rather than the language's.

## The layout

| Area | What it is |
| --- | --- |
| Title bar | The project name, and a status that reads `wasm` in green once the runtime has loaded. |
| Scrollback | Every command you have run, with its live output, errors and results. |
| Token inspector | One line above the input, naming the role of the word you last tapped. |
| Prompt | The full current directory. |
| Input | A transparent text field over a coloured mirror of what you type. |
| Run button | Runs the line. It becomes a red Stop button while a command is running. |
| Completion row | Appears while you type, offering commands, variables and paths. |
| Suggestion row | Fixed examples you can tap to fill the input. |

The page is laid out for a phone first: a 390 by 844 screen fits the scrollback, the
input and both button rows without horizontal scrolling.

## Typing

The input is a real text field, so the phone keyboard, autocorrect settings and text
selection behave normally. The colouring you see is a mirror rendered behind it, updated
on every keystroke by parsing the line. While a line is half-typed it usually does not
parse, and the mirror shows plain text rather than complaining.

| Key | Effect |
| --- | --- |
| Enter | Run the line. |
| Up and Down | Walk back and forward through lines you have run. |
| Tab | Apply the only completion, or extend to the common prefix of several. |
| Escape | Stop a running command. |

History keeps what you actually submitted, including `help`, `clear` and `undo`.

## Completions

As you type, the page asks the session what the last word could become and shows the
answers as chips above the suggestions. Tapping one replaces that word. Directory
completions end with a separator so you can keep descending.

| You type | You are offered |
| --- | --- |
| `c` | `cat`, `cd`, `cp`, `clear` |
| `cat re` | `readme.txt` |
| `cd doc` | `documents/` |
| `cat documents/no` | `documents/notes.txt` |
| `ls \| se` | `set` |
| `echo $` | every bound variable |

The first word of the line, and the first word after a pipe, complete to command names.
A word starting with `$` completes to variables. Everything else completes to files and
directories relative to the current one. An empty line offers nothing, because the
suggestion row already covers that case.

## Results on screen

A result is rendered by kind:

- **Paths, objects, components, numbers and booleans** become chips. Tapping a chip
  appends its text to the input, which is how you avoid typing a file name on a phone.
- **Text** becomes an indented block, so `cat` output keeps its line breaks.
- **Errors** are shown in red under the command. A command you stopped shows
  `Stopped.` in amber instead.

Output written while a command runs appears above the result, and updates in place.

## Running and stopping

Submitting a line creates its scrollback entry immediately, with a blinking cursor,
and the entry fills in as the command writes. The Run button becomes **Stop** for as
long as the command is running.

Only one command runs at a time. Submitting another while one is in flight answers
`A command is already running. Stop it first.`

Stopping is cooperative: the command is asked to cancel, writes whatever it wants to
say about being interrupted, and the terminal adds `Stopped.` So a stopped `progress`
shows both its own `Cancelled at 9%` and the terminal's `Stopped.`

## Words the page handles itself

Three words never reach the evaluator:

| Word | Effect |
| --- | --- |
| `help` | Lists every command with its parameters and description, generated from the command definitions. |
| `clear` | Empties the scrollback. It does not touch the filesystem, the variables or the undo history. |
| `undo` | Reverses the last command and reports which one it was. |

They also appear in completions, so typing `cl` offers `clear` alongside real commands.

## Tapping a word

Every word in the scrollback is a span tagged with the role the parser gave it. Tapping
one highlights it and the inspector line names its role and repeats its text, for
example `identifier` followed by `notes.txt`. This works on your input as it is echoed
back, not just on results.

## The filesystem in the tab

The root is `/home/terminal`, seeded on first use with `documents/notes.txt`,
`projects/` and `readme.txt`. It is Emscripten's in-memory filesystem: real enough that
`mkdir`, `cp` and `write` behave normally, and gone when the tab is closed or reloaded.

`/home/terminal` is where you start, not a fence. `cd ..` and `up` walk above it into
the rest of the page's in-memory filesystem, which holds whatever the .NET runtime put
there. `cd /home/terminal` brings you back.

Nothing is uploaded. The parser, the commands and the files are all inside the page, so
the terminal works offline once loaded. The exception is `download`, which really does
fetch over the network and is therefore subject to the remote host's CORS policy.

## Loading

The runtime is about 15 MB across 121 files on a first visit, and is cached by
the browser afterwards. Until it is ready the input stays disabled and the status reads
`starting…`. If it cannot load, the status turns red and reads `failed to load`.
