# Example programs

Four small programs, one per pillar of the design. Each is a script the terminal can
run with `run examples/<name>.clr`, and each is also an acceptance test: the
[implementation plan](../docs/plan/examples.md) states the exact output every line must
produce, `Core.Tests` asserts it, and the browser check runs it in Chromium.

| Program | Proves | Complete after |
| --- | --- | --- |
| [`tables.clr`](tables.clr) | Listings are tables; word-operator predicates with `$row`; table functions | Phase 3 |
| [`journal.clr`](journal.clr) | Attribute records, views as places, undo and history from the event log, atomic lines | Phase 4 |
| [`resilient.clr`](resilient.clr) | Failure as a value: `else`, `try`, `??`, nested pipelines | Phase 5 |
| [`inventory.clr`](inventory.clr) | Tag to table coercion, XML and CSV as real files | Phase 6 |

A script is one command line per line. Blank lines and lines starting with `#` are
skipped. `run` executes each line as its own transaction and stops at the first fault,
naming the line.

These files are embedded in the core and seeded into a fresh terminal under
`/examples`, so the hosted build can run them as they are.

`tables.clr` and `journal.clr` run today. `resilient.clr` and `inventory.clr` need
error recovery and XML, which are not built yet.
