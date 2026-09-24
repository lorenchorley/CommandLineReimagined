# 0056. Pipelines grow into a live notebook

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

The owner asked for a way to see a pipeline (its inputs, outputs, types and the values
passing between its stages), and for pipelines to become more than a line: graphs, and
perhaps a node editor. The [vision](../vision.md#seeing-and-shaping-pipelines) set out
three routes to a graph.

## Options

1. **A node editor.** Stages as nodes and values as wires, edited by hand. Capable, and
   slow for what a line says quickly; big graphs tangle, and wiring with a finger on a
   phone is miserable.
2. **A live notebook.** An entry in the scrollback can be named, and later lines refer to
   it. When an entry changes, everything that refers to it recomputes, as a spreadsheet
   or a reactive notebook does, and as a live listing already does
   ([0047](0047-a-live-listing-stays-where-it-was-run.md)). The graph is drawn from which
   entry refers to which, not wired by hand.
3. **Both at once.**

## Decision

Option 2, chosen by the owner. Pipelines become graphs through the live notebook, and a
graph view is drawn from its references. A pipeline view (each stage's input, output,
type and values) comes first, since the notebook shows it for every entry. Text stays the
canonical form: undo, replay and scripts work on lines. A node editor, if it comes, is a
view that edits the text, and is a decision of its own.

## Consequences

The scrollback's entries gain names and dependencies, and the store and the page must
know which entries read which, so a change recomputes only what depends on it. The
notations (naming an entry, referring to one) are decisions of their own, keeping to the
fixed grammar and the word-first rules.
