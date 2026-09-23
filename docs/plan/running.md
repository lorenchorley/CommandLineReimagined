# Running the plan

How a session that is told to "run the plan" finds where the plan stands, divides the
work, hands it to sub-agents, and brings it back together. It applies to every phase.
A phase document says *what* its work is and how it divides; this document says *how*
that division is carried out.

**Launch.** In a Claude Code session on this repository, type `/run-plan`, or ask for
the plan in `docs/plan/` to be run. An argument narrows it:

| Argument | Does |
| --- | --- |
| none | Carry on from wherever the plan stands, to the end of the current phase |
| a phase, `8` | That phase, from wherever it stands |
| a checkpoint or stream, `8.0`, `8.D` | That piece of work only, then stop |
| `status` | Report where the plan stands, and change nothing |

The owner's instruction to use sub-agents as described here is standing: it is given
by this document, by the repository's `CLAUDE.md` and by the `/run-plan` command. The
launched session does not ask for permission to delegate. It asks the owner only for
the things listed under [Owner decisions](#owner-decisions).

## Roles

| Role | Who | Does | Never |
| --- | --- | --- | --- |
| **Orchestrator** | The launched session | Reads the plan, does the serial checkpoints, briefs and launches sub-agents, merges, verifies, pushes, keeps the progress table, talks to the owner | Writes a stream's code while that stream is running |
| **Stream agent** | One sub-agent per parallel stream, in its own git worktree | Builds its stream: code and tests in the files it owns | Pushes, merges, edits a file it does not own or touch, edits `docs/` or a project file |
| **Scout** | A read-only sub-agent | Answers a question that needs wide reading (an audit, every use of a function), so the orchestrator's context stays for integration | Edits anything |
| **Verifier** | A fresh sub-agent, after integration | Runs the phase's acceptance table against the merged head, line by line, and reports each mismatch | Fixes what it finds |

Only the orchestrator pushes, and it pushes only the working branch
(`claude/project-overview-38505w`). Stream work reaches the remote by being merged.

## 1. Find where the plan stands

Read, in this order:

1. The status line of [README.md](README.md), and of every phase document.
2. The **Progress** table of the current phase: the first phase whose status is not
   complete.
3. `git log --oneline -30` and `git worktree list`.
4. The [decision index](../decisions/README.md), for any record still Proposed.

The next piece of work is the first row of the Progress table that is not `merged`.
A row that says `in progress` with no worktree behind it was lost when a container
was reclaimed: that stream starts again from its brief, and the final report says so.

With `status`, stop here and report the table.

## 2. Read the phase's work breakdown

Every phase document from Phase 8 on has a work breakdown: its serial checkpoints,
its parallel streams, a **Who touches what** table giving each file one owner, a
merge order, and a **Progress** table. Follow it as written.

A phase written without one (Phases 1 to 7, all built) was run by one agent. If new
work arrives without a breakdown, write one first, using
[Dividing work yourself](#dividing-work-yourself), and commit it before launching
anyone, so that the division survives a context reset.

## 3. Serial checkpoints: the orchestrator does them

A serial checkpoint is the critical path: a foundation that the streams code against,
or an integration that has to see everything merged. The orchestrator does it in the
main checkout, on the working branch, and commits and pushes at each step, following
the [working conventions](README.md#working-conventions).

Two reasons it is not delegated. Every stream is briefed against it, and the brief is
only as good as the orchestrator's knowledge of what it built. And a sub-agent's
report would compress away the detail the integration needs.

Scouts may be used freely during a serial checkpoint. A serial step that is large,
self-contained and produces a known set of files may go to a single sub-agent, which
the orchestrator waits for before going on.

Streams are launched only once the foundation is committed and the full
[verification](README.md#verification-every-phase) is green on it, because every
worktree is created from the orchestrator's current commit.

## 4. Parallel streams: one sub-agent each

- Launch every stream that is ready **in one message**: one sub-agent per stream, each
  in its own worktree, all in the background.
- At most seven at once. Further streams wait for a slot.
- Each receives [the brief](#the-brief), filled in for its stream.
- While they run, the orchestrator does not edit any file a running stream owns. It
  may send scouts, draft the integration checkpoint's notes, and answer questions.
- It does not poll. A sub-agent's completion arrives as a notification.
- To answer a stream, or send it back with a failure, continue *that* agent, which
  keeps its context. Do not start a new one.
- A stream that stops with a question is not waited on by the others.

### The brief

Fill in every `{…}`. The brief is self-contained: a stream agent has the repository
and this text, and nothing from the orchestrator's conversation.

```text
You are stream {X} ({title}) of Phase {N} of the implementation plan in docs/plan/.
You are working in your own git worktree, branched from {commit}, which is the
foundation checkpoint. Other agents are building the other streams at the same time.

Read first:
- docs/plan/README.md: "Principles the implementation must hold to" and
  "Working conventions".
- docs/plan/{phase file}: "The design", your section "{X}", your column of
  "Who touches what", and findings {numbers} in the findings table.
- docs/decisions/{records your section cites}.

Your job: close findings {numbers}, as your section describes.
You are done when: {the section's tests} pass, every existing test suite still passes,
and {any stream-specific condition}.

Rules:
1. You own {files}. You may also edit {touched files}, additively and only where your
   section says. For any other file: do not edit it. Say what you needed in your report.
2. Do not edit project files (*.fsproj, *.csproj), anything in docs/,
   tools/browser-check.mjs, or the decision records. The orchestrator owns them, and
   they already list every file you need.
3. Code against the foundation's interfaces as they are. Where another stream's work
   would give a better answer, keep the fallback the foundation provides. Do not
   write that stream's part yourself.
4. Build the projects you changed and run their test projects, and Core.Tests. All
   must pass before you report.
5. Commit in your worktree, one commit per coherent step. The message says why, and
   ends with these lines:
   {attribution lines}
   Do not push, merge, rebase or open a pull request.
6. If something would change the language, or contradicts a decision record, do not
   decide it. Leave that item as it is today, finish everything else, and put the
   question in your report.
7. Before you report, delete the bin/ and obj/ folders in your worktree. The disk is
   shared with six other builds.

Your final message is your report, in this form:
- Branch and head commit (git rev-parse --abbrev-ref HEAD; git rev-parse --short HEAD)
- Findings closed, and any not closed, with the reason
- Tests added, per file, and the commands you ran, with their results
- Files edited outside the ones you own, and why
- Where you departed from the plan, and why
- Questions for the owner
```

## 5. Integrate

When a stream reports:

1. Read the report. If it closed nothing, or it broke rule 1 or 2, send it back.
2. Merge its branch into the working branch in the main checkout with
   `git merge --no-ff <branch>`. When several have reported, merge them in the
   phase's merge order. The order is a preference: a stream that is not ready does
   not hold back the ones after it.
3. Resolve conflicts in the shared files. They are additive by design, so both sides
   are usually kept.
4. Run the full [verification](README.md#verification-every-phase), including the
   browser check.
5. **Green:** update the stream's Progress row to `merged` with the merge commit,
   commit, push, then `git worktree remove` the worktree and delete its branch.
6. **Red:** undo the merge while it is still local (`git reset --hard ORIG_HEAD`),
   and send the failing output back to the same agent. It merges the working branch
   into its own branch, never rebases, fixes, and reports again. A one-line conflict
   fix in a shared file is the orchestrator's to make. Anything more is the stream's.

A stream that fails twice on the same cause is `blocked`, with the cause in its row,
and the final report names it. The other streams carry on.

## 6. Finish the phase

1. The integration checkpoint is serial and belongs to the orchestrator. Parts of it
   that write disjoint files (user documentation, the specification, the browser
   check) may run as parallel sub-agents, each briefed like a stream: the files it
   owns, what it must cover, and that it is to paste output from real runs, never
   type it. For these, the brief's rule 2 gives way to the files the part owns.
2. When everything is merged, one verifier runs the acceptance table against the
   merged head and reports every line that differs. What it finds goes back to the
   stream that owns the file.
3. The orchestrator republishes the browser client to GitHub Pages, by pushing, and
   to the Artifact, with
   [the recipe in building.md](../building.md#republishing-the-artifact). It then writes the phase's "As built" section, sets its status line and the README's, and
   pushes.
4. The final report to the owner covers what merged, what is blocked and why, which
   decisions are waiting on them, and the published link.

## Owner decisions

- A Proposed record gates exactly what the phase document says it gates, and nothing
  else. At the start, ask the owner about every Proposed record that gates work in
  this run, in one question, then carry on with everything it does not gate.
- Questions from stream reports are collected and asked together, once, not one at a
  time as they arrive.
- When the owner answers, the orchestrator writes or updates the record and its row
  in the index, on the working branch, before it merges the work that depends on it.

## Dividing work yourself

For work that arrives without a breakdown. Split it into parallel streams only when
**all** of these hold:

- the pieces edit disjoint files, or share a file only additively, in different
  functions;
- each piece can be built and tested on its own, against an interface that is already
  committed;
- each piece is at least a checkpoint's worth: more than one file of code, plus its
  tests. Below that, writing the brief costs more than doing the work.

Keep it serial, in the orchestrator, when the work:

- defines an interface that other work codes against;
- adds files to a project. F# compiles in the order `*.fsproj` lists files, so every
  new file is registered by the foundation, never by a stream;
- changes the grammar's shared rules in `Parser.FParsec/Grammar.fs` in more than one
  place;
- needs the owner's answer first;
- is documentation or specification, which describes what merged, not what was
  planned.

Then write the breakdown into the phase document, with its streams, the *Who touches
what* table, the merge order and the Progress table, and commit it before anyone is
launched.

## Resuming

Everything the orchestrator knows has to be on disk, because a context can be
summarised and a container reclaimed. The Progress table is committed and pushed
after every merge. Worktrees are local and are lost with the container. So unmerged
stream work is at risk, and the remedy is to merge each stream as soon as it is
green, rather than batching merges at the end.

## In Claude Code

| Plan term | Tool |
| --- | --- |
| Stream agent | `Agent`, `subagent_type: "general-purpose"`, `isolation: "worktree"`, `run_in_background: true`, `prompt:` the filled brief |
| Scout | `Agent`, `subagent_type: "Explore"` |
| Verifier | `Agent`, `subagent_type: "general-purpose"`, no worktree, told to change nothing |
| Continue an agent | `SendMessage` to the agent's name or id |
| Ask the owner | `AskUserQuestion` |
| Track the streams | One task per stream, updated as the Progress table changes |

Use the `Agent` tool, not the `Workflow` tool, unless the owner asks for a workflow.
