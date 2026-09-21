# 0004. The browser terminal renders to the DOM

| Field | Value |
| --- | --- |
| Status | Accepted, recorded retrospectively |
| Date | 2026-09-19 |

## Context

The desktop shell draws on a canvas through an entity component system, with per-token
hit testing. The first browser prototype mirrored that on an HTML canvas.

## Options

1. **Canvas, mirroring the desktop.** One rendering model for both hosts, but
   scrolling, selection, the soft keyboard and accessibility all have to be rebuilt.
2. **DOM elements, one span per token.** Everything the browser already does comes
   free, and token spans keep the per-token interaction.

## Decision

Option 2.

## Consequences

The two front ends share the execution layer and the token stream, not the renderer.
Tables in [0009](0009-table-coercion.md) render as real HTML tables.
