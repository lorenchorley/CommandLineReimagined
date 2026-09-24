# 0054. The Windows front end is removed

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |
| Supersedes | [0012](0012-browser-first.md), and the desktop parts of [0029](0029-scene-editor-direction.md) |

## Context

The Windows desktop shell was the project's first front end. It drew the terminal on a
canvas through an entity component system, with its own renderer, ray casting for hit
testing, interaction logic and a thesaurus-based command search. Since the move to the
browser ([0003](0003-execute-in-the-browser.md), [0004](0004-dom-not-canvas.md)) and the
F# core ([0006](0006-functional-core-in-fsharp.md)), it has had no new presentation
([0012](0012-browser-first.md)): it rendered new value kinds as text, its right-click
menu had probably never run, and it could only be built and checked on Windows, by a CI
job of its own. It was ten of the solution's projects, about 9,500 lines, and the
browser terminal used none of them.

[0029](0029-scene-editor-direction.md), still Proposed, suggested keeping a desktop host
by embedding the browser client in WebView2.

## Options

1. **Keep it compiling**, as 0012 decided. A second front end nobody develops, and a
   Windows-only CI job to protect it.
2. **Host the browser client in WebView2**, as 0029 proposed. A desktop app without a
   second renderer, and still a Windows build to keep.
3. **Remove it.** The browser is the one front end.

## Decision

Option 3, chosen by the owner to simplify the project. Removed: the desktop shell
(`CommandLine`, `CommandLineReimagined`), its entity component system, renderer, ray
casting, UI components, interaction logic and controller (`EntityComponentSystem`,
`Rendering`, `RayCasting`, `VisualInterface`, `InteractionLogic`, `Controller`), the
source generator and utilities only they used (`SourceGenerators`, `Utils`), their tests,
and the Windows CI job.

## Consequences

The solution is the parser, the core and the web projects, and everything builds and
tests on Linux. The retained GOLD parser lived in the desktop shell, so it goes too
([0055](0055-the-gold-parser-is-removed.md)).

[0029](0029-scene-editor-direction.md)'s idea stands, a scene the command line edits,
rendered in the browser. Its fifth rule narrows to "one browser renderer", and its desktop
step is gone. Its third rule already treated the ECS as a prototype whose ideas carry
over: those ideas are written in the [design direction](../plan/scene-editor-direction.md),
and the code stays in the repository's history.

The thesaurus command search goes with the shell; the browser's command keywords
([0037](0037-in-out-back-and-read.md)) remain.
