# CommandLineReimagined

Work here is driven by the implementation plan in [docs/plan/](docs/plan/README.md).
To run it, follow [docs/plan/running.md](docs/plan/running.md); the `/run-plan`
command does exactly that.

Running the plan uses sub-agents: the launched session is the orchestrator, and it
hands each parallel stream to its own sub-agent in its own git worktree, then merges
and verifies what comes back. This file and the plan are the owner's standing
instruction to work that way. Do not ask before delegating as running.md describes.

- The working branch is `claude/project-overview-38505w`. Only the orchestrator
  pushes, and only to it. No pull request unless the owner asks for one.
- Decisions are in [docs/decisions/](docs/decisions/README.md). The plan executes them
  and does not reopen them. A new question that would change the language is asked,
  not decided.
- Build, test and browser-check with the commands in
  [Verification, every phase](docs/plan/README.md#verification-every-phase).
