# 0029. The entity component system becomes a scene the command line edits

| Field | Value |
| --- | --- |
| Status | Proposed |
| Date | 2026-09-22 |

## Context

The entity component system was built so that the command line could grow into an
editor in the manner of a game engine's: everything rendered to a canvas, and commands
able to build 2D or 3D scenes, or to use the scene to make their own output and
interactions richer. The browser terminal was a straight port that did not take this
into account, and since the move to the F# core
([0006](0006-functional-core-in-fsharp.md)) the ECS survives only as the desktop
shell's display layer ([0012](0012-browser-first.md)). The core knows nothing about
it, and the one feature that relied on it, clickable listings, has gone from both
hosts.

The core has meanwhile built most of what a command-driven editor needs, aimed at
files rather than entities: attribute records ([0013](0013-attribute-filesystem.md)),
predicates over tables ([0008](0008-explicit-row-variable.md)), queries as places, live
views, and an event-sourced store with undo ([0010](0010-undo-by-event-sourcing.md)).
The tag notation already distinguishes objects from components, a distinction that
"mirrors an entity component system"; but a component is only a value
([execution model](../spec/execution-model.md)), and nothing attaches it to an entity at
run time.

The full reasoning, the mapping from the core's concepts to an editor's, and a first
milestone are in the [design direction](../plan/scene-editor-direction.md).

## Options

1. **Leave the ECS as the desktop's display layer.** Nothing to build. The original
   ambition is abandoned, and the ECS stays a second copy of what the DOM already does.
2. **Port the ECS to the browser as it is, and draw everything on a canvas.** Closest
   to the original plan. It reverses [0004](0004-dom-not-canvas.md) for the terminal
   text as well as the scene, so selection, copy, input methods, the soft keyboard,
   scrolling and accessibility all have to be rebuilt. The current implementation's
   reflection, linear searches and per-frame serialisation also do not survive
   WebAssembly and trimming.
3. **A scene as a first-class domain of the core, rendered on a canvas, with the
   terminal kept in the DOM.** The ECS models the user's world, not the terminal's
   own lines. Scene edits are events in the core's store, so `undo` covers them, and
   the runtime scene is a projection of the log, as the filesystem is. A scene view is
   a value: it appears in the scrollback as a live canvas, or docked as a panel. One
   browser renderer serves both hosts, the desktop embedding it through WebView2.

## Decision

Pending the owner's answer. Option 3 is recommended, together with these five design
rules:

1. **The canvas is for the scene; the terminal stays in the DOM.** [0004](0004-dom-not-canvas.md)
   stands for text.
2. **One history.** Authored scene state is events in the core's store and is undone
   like anything else. Simulated state (physics, animation, per-frame values) is not
   logged. This is the editor's edit mode and play mode.
3. **The current ECS code is a prototype.** Its ideas carry over: a stable snapshot for
   the renderer (the active and shadow trees), generated change tracking, components
   declared by their state. Its implementation does not. The scene model is either F#
   data in the core or an existing .NET ECS library, checked first for trimming and
   WebAssembly, behind the store.
4. **Commands reach the scene through the store, never through a reference.** They
   return scene events as they now return filesystem events; the host renders. The
   core stays headless, and scripts ([0020](0020-scripts-and-run.md)) can build scenes.
5. **One renderer for both hosts.** Canvas2D first, WebGL later, fed a batch of changes
   per frame across the JavaScript boundary. The desktop hosts the same page in
   WebView2, and the WPF and GDI renderer retires rather than growing.

## Consequences

If accepted, [0012](0012-browser-first.md) is superseded: the desktop stops being a
second presentation to keep at parity and becomes a second host of the browser client.
[0004](0004-dom-not-canvas.md) is refined rather than reversed: it continues to govern
the terminal, and a scene viewport becomes the one place a canvas is used.

The core gains a scene projection, scene event types and a scene view value kind; the
store gains a distinction between logged and simulated state. The `EntityComponentSystem`,
`Rendering`, `RayCasting`, `Controller` and `InteractionLogic` projects stop being the
desktop's display layer, and are either reworked into the scene runtime or retired once
the WebView2 host replaces them.

Affordances on ordinary results, such as a listing entry that opens on double click,
come back through the same mechanism: output can be a small live scene, or a value
carrying actions each host knows how to present.

The risk is scope. A game editor has no natural end, so the first milestone in the
[design direction](../plan/scene-editor-direction.md#first-milestone) is deliberately
2D, small and without simulation, and each later step (play mode, scripting, 3D) is
its own record.
