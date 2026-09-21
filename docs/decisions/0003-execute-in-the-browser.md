# 0003. The browser executes commands itself

| Field | Value |
| --- | --- |
| Status | Accepted, recorded retrospectively |
| Date | 2026-09-19 |

## Context

A browser front end was wanted so the terminal could be tested from a phone. The
question was where commands run.

## Options

1. **Execute on a server and stream results.** A session per user, latency on every
   keystroke for colouring, and a data-protection question about what users type.
2. **Execute in the page, with .NET compiled to WebAssembly.** A larger first download,
   but no server, no latency, and nothing leaves the tab.

## Decision

Option 2. The ASP.NET host remains for tooling and parses only.

## Consequences

The execution layer had to be free of anything that needs a window or a render loop,
which is what produced the output-sink interface. The filesystem is the page's
in-memory one, which [0013](0013-attribute-filesystem.md) revisits.
