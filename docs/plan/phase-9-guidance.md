# Phase 9: names that read as moves, and a terminal that guides

**Goal.** The owner's requests of 2026-09-24, from using the terminal on a phone. Going
into and out of places reads like zooming: `in`, `out`, and `back` to where you were.
Reading a file is `read`. A command called wrongly shows how to call it. Completion
offers what can come next, the pipe first. On the page, any listing can be made live
again, text selected in the scrollback is copied, the history can be walked with
buttons, and the title bar goes.

**Status: complete.** Built on 2026-09-24; where the result differs from the plan is in
[As built](#as-built). It was run as [running.md](running.md) describes, with the
layout in [Running it](#running-it) and the state in [Progress](#progress).

## Requests

| # | The owner asked | Where |
| --- | --- | --- |
| R1 | Rename `cd` to `in` ("into view") and `up` to `out` ("out of view"), like zooming | 9.1 |
| R2 | A new `back`: to the previous folder or view | A |
| R3 | Rename `cat` to `read` | 9.1 |
| R4 | Update all the documentation, and the guide in the filesystem | 9.1, 9.9 |
| R5 | A listing that stopped being live can be made live again, so several are live at once | C |
| R6 | Selecting text in the scrollback copies it to the clipboard | C |
| R7 | `vars` takes no arguments, but folder names are offered after it | B |
| R8 | After a command that outputs data, the first suggestion is the pipe | B |
| R9 | A command called wrongly shows its help | B, C |
| R10 | Up and down arrow buttons beside undo, redo and out, to walk the history | C |
| R11 | Tapping a palette example leaves the caret at the end of what it inserted | C |
| R12 | Remove the title and the green `wasm` at the top of the screen | C |
| R13 | What the terminal adds, that a command did not answer, looks clearly different from output | C |

## Decision records

Written in 9.0, all Accepted: each is the owner's request of 2026-09-24.

- **0037. Places are entered with `in`, left with `out`, and retraced with `back`; a
  file is read with `read`.** `cd`, `up` and `cat` are removed, not kept as aliases:
  two names for one thing is one more thing to learn. The old names are each new
  command's keywords, so completion still finds them (`cd` offers `in`, detail
  `in · matches "cd"`) and running one says which to use
  (`Unknown command : cd. Did you mean in?`). `back` goes to where you were before the
  last move made by `in`, `out` or `back`'s opposite: each `back` goes one step further,
  like a browser's back button, and `undo` takes a `back` back like any move. The
  location line's `up` button becomes `out`. Earlier records that say `cd`, `up` or
  `cat` are not edited; they read as they were decided.
- **0038. A command called wrongly shows its help.** When binding fails for a command
  that exists (too many or too few arguments, a missing parameter, an unknown flag, a
  value of the wrong kind), the line still fails with the same fault, and the response
  carries that command's help as well: the table `help <command>` answers, with its
  description. The page draws it under the error. A failure inside a running command
  (a file that is not there) is not a call made wrongly, and shows no help.
- **0039. Completion offers the pipe first after a complete stage.** When the word
  under the cursor is empty and the stage before it has all its required arguments,
  the first chip is `|`, detail `send the result on`. A command with no parameter left
  to take, as `vars` has none at all, offers only `|` there, never files.

## Wire

In 9.0, so that B and C code against it from the start:

- `Session.Response` gains `Guide: Value option`, always `None` in 9.0.
- `ExecutionResponse` gains `IReadOnlyList<ResultItem>? Guide`, mapped the way `Result`
  is, and serialised as `guide`. The page ignores it until C draws it.

## Checkpoint 9.0: foundation

The orchestrator, before anything else. Records 0037 to 0039 and their index rows; the
wire above with a test that `guide` is absent on a line that succeeds; this document.
**Done when** every suite passes and the browser check passes.

## Checkpoint 9.1: the rename

One sub-agent, alone, because it touches almost every file and the streams code
against its names. The orchestrator waits for it.

- Core: `cd` becomes `in`, `up` becomes `out`, `cat` becomes `read`, with the old name
  first among each one's keywords. Descriptions: `in` "Go into a folder, a saved view, or
  a question written out"; `out` "Come out of the current view, or up out of the
  folder"; `read` "Show what a file says".
- Every test, the four example programs in `examples/` and their golden results
  (the tests and [examples.md](examples.md)), the seven files in `guide/`, the page (the
  location line's `up` button reads `out`; the palette keys), `tools/browser-check.mjs`,
  and every user document and spec page, transcripts re-run with
  `tools/transcript.fsx`, never edited by hand. Decision records before 0037 are not
  edited.
- **Done when** a search for the old names finds them only in decision records before
  0037, as keywords, in the phase documents of Phases 1 to 8, and where a document says
  they were renamed; every suite passes; the browser check passes.

## Parallel workstreams

After 9.1, A, B and C start together from its commit.

### A. `back`

**Closes** R2. **Size** M.

- The projection keeps the trail of places left by `in` and `out`. `back` goes to the
  most recent and takes it off the trail; a second `back` goes one further. At the
  start of the trail it is not a fault: it answers where you are and says there is
  nowhere further back.
- The move is an event like the others, so it is replayed, inverted by `undo` (which
  puts the place back on the trail) and kept across a reload. If a new event case is
  needed, the store's serialisation and its round-trip tests carry it.
- `back` is not read-only, as `in` and `out` are not. Take `back` out of `out`'s
  keywords, where it came from `up`, so `back` offers only `back`.
- Decision [0040](../decisions/0040-seeded-files-follow-the-seed.md): in
  `Session.BringUpToDate`, a seeded file that no undoable transaction has touched is
  brought to the seed's current content, in one system transaction with the source
  `seed update`. Returning visitors then read the renamed guide. Tests in
  `PersistenceTests.fs`: an untouched guide file and readme are updated; an edited one,
  a renamed or tagged one and a deleted one are left alone; a second load changes
  nothing.
- **Tests** in `ViewTests.fs` or a new class: in, in, back, back; back from a view to
  the folder; back after out; back at the start; undo of a back; back across a reload.
- **Owns** `back` in `Core/Commands/Files.fs`, the trail in `Core/Projection.fs`,
  any new case in `Core/Events.fs` and its serialisation, `Core/Seed.fs` and
  `BringUpToDate` in `Core/Session.fs`. **Touches** the command list in
  `Core/Session.fs`, one line.

### B. Guidance in the core

**Closes** R7, R8, R9 (core side). **Size** M.

- 0038: where binding fails for a known command, the session sets `Response.Guide` to
  the value `help <command>` answers; `ExecutionResponse.Guide` maps it. Not for
  faults raised while a command runs, and not for unknown commands.
- `Unknown command : cd. Did you mean in?`: the nearest names consider keywords, so an
  old name leads to the new one. 9.1 made a keyword that is the whole word match at any
  length, so `cd` offers `in`; keep that, and stop a short common word matching a
  keyword it merely equals by accident (`by` offering `sort`) if a rule can tell them
  apart, or report that it cannot.
- 0039 in completion: `|` first after a complete stage, detail `send the result on`;
  a command with nothing left to take offers only `|`. `vars ` offers `|` and nothing
  else; `ls ` offers `|` first and then what it offered before; `sort ` offers columns
  (its column is required) and no `|`.
- **Tests** in `CommandCompletionTests.fs`, `ArgumentCompletionTests.fs`,
  `ExecutionTests.fs` or a new class for the guide, and `Web.Core.Tests` for the wire.
- **Owns** `Core/Completion/Arguments.fs`, `Core/Completion/Commands.fs`, `Nearest`,
  the guide in `Core/Session.fs`. **Touches** `Core/Faults.fs` (unknownCommand),
  `Web.Core/TerminalSession.cs` (mapping `Guide`).

### C. The page

**Closes** R5, R6, R9 (page side), R10, R11, R12, R13. **Size** L.

- **Live listings (R5):** every listing keeps its badge. A frozen one reads `paused`,
  and tapping it makes it live again; any number can be live, and each is refreshed
  when the store changes. The newest listing still starts live.
- **Copy on select (R6):** a selection made inside the scrollback is copied to the
  clipboard when the selection ends, with a short `copied` note that fades. Nothing is
  copied from the input. It must not break tapping a cell or a token, and on a phone a
  long press must still select text.
- **The guide (R9):** a failed line whose response has `guide` draws it under the
  error, as the `help` table is drawn.
- **The terminal's own words look like its own (R13):** everything the page shows that
  is not what a command answered (the guide under an error, the `copied` note, the
  banner, `Did you mean` suggestions) is drawn so it cannot be mistaken for output: a
  panel of its own, set apart by an accent border and background, a small label saying
  what it is (`help`, `note`), and a different face (the proportional UI font rather
  than the terminal's monospace). Output keeps the look it has. The browser check reads
  the style of the guide panel against a result's.
- **History buttons (R10):** ↑ and ↓ beside ↶, ↷ and `out`, doing what the Up and Down
  keys do. Like the other buttons, they neither open nor close the keyboard.
- **Palette (R11):** a key puts its text in the line with the caret at its end.
- **Header (R12):** the title and the `wasm` status go. What the status said still
  reaches the user: `restoring…`, `failed to load` and `failed to restore` in the
  banner's place in the scrollback, `not persisted` in the banner (it already is). The
  page marks itself ready (a `data-ready` attribute on `body`) for the browser check to
  wait on, instead of the status text.
- **Tests:** `tools/browser-check.mjs` for each of the above: two listings live at once,
  a paused one made live again, a selection copied (read back through the clipboard
  permission in the test browser), the guide drawn under a wrong call, ↑ and ↓ walking
  the history, the caret after a palette key, and no header.
- **Owns** `WebClient/wwwroot/index.html`, `tools/browser-check.mjs`.

## Who touches what

| File | 9.0 | 9.1 | A | B | C | 9.9 |
| --- | --- | --- | --- | --- | --- | --- |
| `docs/decisions/` | own | | | | | index |
| `Core/Session.fs` | wire | rename | one line | guide | | |
| `Web.Core/TerminalSession.cs` | wire | | | mapping | | |
| `Core/Commands/Files.fs` | | rename | `back` | | | |
| `Core/Projection.fs`, `Core/Events.fs` | | | own | | | |
| `Core/Completion/*.fs` | | rename | | own | | |
| `Core/Faults.fs` | | | | unknownCommand | | |
| `Core.Tests/`, `Web.Core.Tests/` | wire test | rename | own tests | own tests | | |
| `examples/`, `guide/`, `docs/plan/examples.md` | | own | | | | guide |
| `WebClient/wwwroot/index.html` | | rename | | | own | |
| `tools/browser-check.mjs` | | rename | | | own | |
| `docs/` user documents and `docs/spec/` | | rename | | | | own |

## Running it

1. **9.0: the orchestrator.**
2. **9.1: one sub-agent,** in its own worktree from the 9.0 commit, the only one
   running. The orchestrator merges it and verifies before anything else starts.
3. **A, B and C: three sub-agents, launched together,** each in its own worktree from
   the 9.1 merge. Merge order when several are waiting: A, then B, then C, since C draws
   what B sends.
4. **9.9: integration.** Two sub-agents in parallel for the documentation of what A, B
   and C built, each owning a set of files:
   - *docs:* the user documents in `docs/` and the guide in `guide/` (`back`, the guide
     under an error, the pipe first, live listings, copying, the history buttons, the
     page without a title);
   - *spec:* `docs/spec/` (the command catalogue, the execution model for `back`, the
     host interfaces for `guide` and completion, conformance counts).
   Then the orchestrator: a verifier over [Acceptance](#acceptance), the republish, the
   "As built" section and the status lines.

## Progress

| Work | Done by | State | Commit |
| --- | --- | --- | --- |
| 9.0 Foundation | orchestrator | merged | aef5fea |
| 9.1 The rename | sub-agent | merged | 75d105f |
| A. `back` | stream agent | merged | e62c9df |
| B. Guidance in the core | stream agent | merged | 2dc822f |
| C. The page | stream agent | merged | cebe5a2 |
| 9.9 docs, spec | two sub-agents | merged | ef242a3, 7a69f41 |
| 9.9 gaps the documents found | orchestrator | merged | d6c6194 |
| 9.9 verifier, republish, As built | orchestrator | done | this commit |

## Acceptance

From a fresh tab, at 390 by 844:

| Line or action | Does |
| --- | --- |
| `in documents`, then `out` | into `/documents`, then back to `/` |
| `in documents`, `in /examples`, `back`, `back` | `/documents`, then `/` |
| `in $row.kind eq folder`, `back` | into the view, then back to `/` |
| `back` in a fresh tab | says there is nowhere further back, not a fault |
| `in documents`, `back`, `undo` | back in `/documents` |
| `cd documents` | `Unknown command : cd. Did you mean in?` |
| `cd` typed | the chip `in`, detail `in · matches "cd"` |
| `read readme.txt` | the readme |
| `help where extra` | the fault, and under it `help`'s own help, since `help` is the command called wrongly (0038) |
| `read` with no argument | the fault, and under it `read`'s help |
| `read missing.txt` | the fault, and no help |
| `vars ` | only `|` |
| `ls ` | `|` first |
| `sort ` after `ls |` | columns, no `|` |
| two `ls`, a `mkdir` between | both listings refreshed |
| tap `paused` on an old listing | it reads `live`, and refreshes |
| select text in a result | it is on the clipboard, and `copied` shows |
| ↑, ↑, ↓ | the line before last, then the last |
| tap the `readme` key | `read readme.txt` in the line, caret at its end |
| the top of the screen | no title, no `wasm` |

Plus: every suite green, the four example programs at their golden results with the
new names, the guide's examples all running, and the payload under 20 MB.

## As built

Every checkpoint and stream, with these differences and findings.

- **How it ran.** One orchestrator did 9.0 and handed 9.1, the rename, to one
  sub-agent, since every later stream reads the new names. Streams A, B and C then ran
  together in their own worktrees and merged without conflict. 9.9's documents and
  specification went to two sub-agents, and each wrote down where the code did not do
  what it was describing, which is how the gaps below were found. Nothing waited on the
  owner.
- **Found by the documents and fixed**, which also closed the two deviations the
  specification had first listed for them:
  - A value of the wrong kind for a parameter, like `ls | take x`, is an `Invalid`
    fault, and it carried no help. A fault whose message names the parameter
    (`'count' must be a whole number`) now does, as 0038 asks of every wrong call.
  - The pipe was not offered after `in documents ` or `$files `: the first is a
    predicate place that answered a constant, and the second a value stage completion
    saw as an unknown place. Both now offer `|` first, as 0039 asks.
  - The page never showed the pipe chip's detail, because the signature took the line
    first. With `|` selected, the line reads `| · send the result on`.
  - `undo` still had the keyword `back`, which is now a command of its own.
  - `'read' takes 1 argument, but 2 were given` read `1 were given` when one was given.
    It says `was`.
- **Found by the verifier and fixed:**
  - A command found by a keyword named itself twice in the detail line,
    `in · in · matches "cd"`, since Phase 8. The page no longer repeats a name the
    detail already starts with.
  - A palette key tapped with the keyboard down left the caret at the start of the
    line, so typing after focusing it went in front. The caret now goes to the end when
    the line next gets the focus. The browser check tests both.
- **Kept as they are, and why:**
  - `Did you mean in?` is still part of the fault's own text, drawn as an error. Phase
    10 moves suggestions into notes drawn as guidance
    ([decision 0041](phase-10-help-towards-understanding.md#decision-records)), and
    changing the sentence before then would change it twice.
- **Not verifiable here.** A real long press and its selection handles, and a real
  on-screen keyboard, cannot be run headless. The browser check makes a selection in
  code, as the handles do, and emulates a phone at 390 by 844 with touch. The owner's
  phone is the check that remains.
- **Verification.** A verifier ran the acceptance table against d6c6194: 20 of 20 rows
  matched, on the core and on the published page. On the final head every project
  builds without warnings, the 1372 tests of the four suites `conformance.md` counts
  pass (823 in Core.Tests), and so do the smaller suites; the four example programs
  give their golden results and every example in the guide runs; the browser check
  passes. The payload is 10.7 MB the way `build.yml` measures it, and 18.5 MB as the
  whole `wwwroot`, both under 20 MB. The Artifact was republished from the final head as version 19.
