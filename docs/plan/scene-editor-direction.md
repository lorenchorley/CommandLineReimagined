# Design direction: a scene the command line edits

**Status.** A proposed direction, not part of Phases 1 to 7. Nothing here is built, and
nothing here is to be built until [decision 0029](../decisions/0029-scene-editor-direction.md)
is accepted. The command syntax below is illustrative: none of these commands exist, and
their exact form is for the records that introduce them. Its desktop parts, a
desktop host in WebView2 and the retiring WPF renderer, are superseded by
[0054](../decisions/0054-the-windows-front-end-is-removed.md), which removed the Windows front end.

## The idea

The entity component system was always meant to grow into an editor in the style of a
game engine's: a world of entities rendered on a canvas, built and changed from the
command line. Commands could construct 2D or 3D environments, or use the scene to
give their own results and interactions more than text can.

The browser terminal was a port that did not take this into account. Since the F#
core, the ECS had been only the desktop shell's display layer, and the core knew
nothing about it; both were removed with the Windows front end ([0054](../decisions/0054-the-windows-front-end-is-removed.md)). This document records how the original ambition fits the system as it now
stands, and what should change on the way.

## Why it fits

The core has built most of what a command-driven editor needs, aimed at files rather
than entities:

| The core already has | An editor needs |
| --- | --- |
| Records with typed attributes ([0013](../decisions/0013-attribute-filesystem.md)) | Entities with components |
| `ls \| where $row.kind eq folder` | A query over the world: which entities have a sprite and sit above the floor |
| A query is a place you can be (`in <predicate>`) | A selection, or a filtered view of the scene |
| Live views that refresh when the store changes | An outliner and an inspector that stay in sync |
| An event-sourced store with undo and redo ([0010](../decisions/0010-undo-by-event-sourcing.md)) | Undoable edits, which every editor needs and most get wrong |
| Tags: `<object/>` and `{component/}` | A literal for an entity and what is attached to it |
| Scripts of command lines ([0020](../decisions/0020-scripts-and-run.md)) | Scene construction that can be saved and replayed |

The language anticipated this. [The command language](../language.md#tags-objects-and-components)
says the tag notation "mirrors an entity component system", and the
[execution model](../spec/execution-model.md) already has `Object` and `Component`
values. What is missing is anything that attaches a component to an entity at run
time. Building the scene completes a design the language already carries.

A typed, undoable, queryable command line over a live scene has few precedents.
Blender's Python console and Unity's editor scripting come closest, and in both the
command line is an accessory to the editor rather than its main interface.

## Five design rules

### 1. The canvas is for the scene; the terminal stays in the DOM

The reasons in [0004](../decisions/0004-dom-not-canvas.md) still hold for text:
selection, copy, input methods, the soft keyboard, scrolling and accessibility come
from the browser, and a canvas would have to rebuild each. Most editors are built this
way too, with ordinary widgets around a GPU viewport.

A scene view is therefore a **value**. A command returns one, and it appears in the
scrollback as a live, interactive canvas, or docked beside the terminal as a panel.
The desktop shell used its ECS to draw its own prompt and output lines, which spent
the ECS on what the platform already does. That use went with the desktop.

### 2. One history, split into authored and simulated state

The desktop's prototype kept a second event log, the ECS's active and shadow history,
beside the core's store. For `undo` to take back a scene edit, the scene being edited must be events in
the core's store, and the runtime scene a projection folded from them, as the
filesystem is.

| | Authored state | Simulated state |
| --- | --- | --- |
| What | Entities, components and their values as edited | Physics, animation, anything recomputed per frame |
| Logged | Yes, one transaction per line ([0015](../decisions/0015-atomic-lines.md)) | No |
| Undone by `undo` | Yes | No; stopping play discards it |
| Persisted in IndexedDB | Yes | No |

This is the editor's edit mode and play mode. Getting the split right before anything
moves on its own is the most important architectural decision in this direction.

### 3. The old ECS was a prototype, not the foundation

The ideas carry over:

- **A stable snapshot for the renderer.** The active and shadow trees exist so drawing
  never races with editing. The same need returns for a scene being edited while it
  renders.
- **Generated change tracking.** Its source generator turned `[State]` properties into
  creation, differential and suppression events. The same shape serves scene events.
- **Components declared by their state.** A component is the data it carries.

Its implementation did not scale to a scene or survive WebAssembly, which is why none
of it was kept:

- Entities and components were found by linear `FirstOrDefault` searches, with no
  indexed queries.
- It depended on reflection throughout (`Activator.CreateInstance`, property setters
  found by name, creation types located by string), which trimming breaks and which is
  slow in WebAssembly.
- Components carried behaviour and injected services rather than plain data.
- `TriggerMerge` serialised the whole history to six files on every frame.
- `Entity.RemoveComponent(Component)` called itself.

Two paths are reasonable:

1. **The scene model as F# data in the core.** Entities are identifiers, components are
   typed records, systems are functions over them. It fits the functional core, and it
   is tested headlessly like everything else.
2. **An existing .NET ECS library for the runtime side**, with the core's store as the
   source of truth. Arch, DefaultEcs and Friflo.Engine.ECS are candidates; which of
   them trims and runs well in WebAssembly is to be measured, not assumed.

### 4. Commands reach the scene through the store

A command never holds a reference to the scene. It returns scene events
(`EntitySpawned`, `ComponentSet`, `EntityRemoved`), exactly as it now returns
filesystem events, and the host renders the projection. This is the same indirection
that `IOutput` gives progress output today. It keeps the core headless and testable,
lets any host share it, and means a script can build a scene as easily as a person
typing.

### 5. One renderer, the browser's

Building a 2D and 3D renderer twice would drain the project. The renderer is the
browser's: Canvas2D for 2D first, WebGL through a
library such as PixiJS or three.js later, fed one batch of changes per frame across
the JavaScript boundary rather than one call per entity. The WPF and GDI renderer was
removed with the desktop ([0054](../decisions/0054-the-windows-front-end-is-removed.md)) rather than grown.

## First milestone

The risk is scope: a game editor has no natural end. The first milestone proves the
idea end to end and nothing more. 2D only, three or four component types, no
simulation.

```
$ spawn <box x=10 y=20>{sprite colour=red/}</box>
$ ls entities | where has(sprite)
$ set $row.x 50          # the box moves in the viewport
$ undo                   # and moves back
$ view                   # a live canvas in the scrollback; a click selects, and the selection is a query
```

Done when:

- Spawning, listing, filtering, changing and removing entities work from the command
  line, and each is one undoable transaction.
- The scene persists across a reload, as the filesystem does.
- A scene view renders in the scrollback and follows edits live.
- Clicking in the view selects, and the selection is usable as a query in the next
  command.
- The example-program discipline of the [plan](README.md#principles-the-implementation-must-hold-to)
  applies: a small scene program with golden results is written first.

## Later steps

Each is its own decision record when its turn comes.

1. **Play mode.** Systems that run per frame over simulated state, started and stopped
   from the command line, with authored state untouched.
2. **Scripting.** Behaviour attached to entities, written in the command language or
   supplied by the host.
3. **3D.** A second viewport kind over the same scene model.

## Richer results in ordinary commands

The same machinery gives back what the old desktop shell lost when the core moved to
F#: output you can act on. On `main`, `ls` attached a path, a context menu and a double-click
action to each entry; that lived in the ECS, and the core cannot see it.

Two forms, which can coexist:

- **Affordances on values.** A value or a table cell carries what it is and what can be
  done with it (a record at this path; open, delete). The browser presents them with
  `data-*` attributes and one delegated listener that sends the chosen action back as a
  command.
- **Results as small scenes.** A dependency graph, a file tree or a chart returned as a
  scene view, interactive in the scrollback rather than printed.

## How this relates to the existing decisions

| Record | Effect if 0029 is accepted |
| --- | --- |
| [0004](../decisions/0004-dom-not-canvas.md) DOM, not canvas | Refined, not reversed. It governs the terminal; the scene viewport is the one canvas. |
| [0010](../decisions/0010-undo-by-event-sourcing.md) Event-sourced store | Extended with scene events and a scene projection, and with the authored/simulated split. |
| [0012](../decisions/0012-browser-first.md) Desktop out of scope | Superseded by [0054](../decisions/0054-the-windows-front-end-is-removed.md): the Windows front end is removed, so there is no desktop host. |
| [0013](../decisions/0013-attribute-filesystem.md) Attribute filesystem | Unchanged. Whether entities are a kind of record in the same store or a sibling domain is an open question below. |

## Open questions

- **Are entities records?** An entity could be an attribute record of a new `kind`, so
  that `ls`, `where` and views work on it unchanged; or the scene could be a sibling
  projection with its own listing. The first reuses the most; the second keeps
  per-frame reads away from the filesystem's shape.
- **Where does a scene live?** One scene per session, or scenes as files that `in`
  enters, the way views are.
- **How are component types declared?** Built into the core, declared by tags at run
  time, or supplied by the host.
- **How many entities must the milestone carry?** The answer decides between the F#
  model and an ECS library, and it should be measured in WebAssembly before choosing.
