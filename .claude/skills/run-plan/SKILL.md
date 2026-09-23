---
name: run-plan
description: Run the implementation plan in docs/plan/ as its orchestrator, dividing each phase between yourself and sub-agents in git worktrees, as docs/plan/running.md prescribes. Use when asked to run, continue, resume or report on the plan, or a phase, checkpoint or stream of it.
argument-hint: "[phase | checkpoint | stream | status]"
---

# Run the plan

You are the orchestrator of the plan in `docs/plan/`. The owner has already told you
to delegate: this command, `CLAUDE.md` and `docs/plan/running.md` are that
instruction. Do not ask before launching sub-agents.

Arguments: `$ARGUMENTS`. None means carry on to the end of the current phase. A phase
(`8`), a checkpoint or a stream (`8.0`, `8.D`) means that work only. `status` means
report and change nothing.

1. Read `docs/plan/running.md` in full. It is the procedure; this file only starts it.
2. Read `docs/plan/README.md`, then the current phase's document: its design, its
   work breakdown ("Running it", "Who touches what") and its Progress table.
3. Say to the owner, in two or three lines, where the plan stands and what you are
   about to do, then do it without waiting for an answer.
4. Follow running.md: the serial checkpoints yourself; the parallel streams as
   sub-agents, all launched in one message, each with the filled-in brief; merge,
   verify and push as each stream reports; the integration checkpoint; the verifier;
   the republish.
5. Keep the Progress table and your task list current, and commit the table after
   every change of state.

Tools, as running.md's "In Claude Code" table maps them: `Agent` with
`isolation: "worktree"` and `run_in_background: true` for each stream; `Agent` with
`subagent_type: "Explore"` for scouts; `SendMessage` to answer or send back a stream;
`AskUserQuestion` for the owner, batched. Not `Workflow`, unless the owner asks for one.

Stop when the requested work is merged and verified, or when everything left is
blocked on the owner. End with the report that running.md's "Finish the phase"
describes.
