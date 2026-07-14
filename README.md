# Mystery of Ancient Darkness Production Stack

> A painted 2.5D adventure, a custom runtime, an art-aware visual level editor,
> a cinematic prologue, and an illustrated novella built as one production
> system.

**OpenAI Build Week 2026 · Developer Tools**

[Open the project site](https://elyssov.github.io/moad-production-stack/) ·
[Launch the visual editor](https://elyssov.github.io/moad-production-stack/editor/) ·
[Watch the prologue](https://elyssov.github.io/moad-production-stack/prologue/) ·
[Read the novella](https://elyssov.github.io/moad-production-stack/novella/Mystery_of_Ancient_Darkness_INITIUM_Illustrated_Novella.pdf)

## Why this exists

Painterly 2.5D games commonly maintain two worlds: the image the player sees
and invisible collision authored somewhere else. When those worlds disagree,
characters float above floors, fall through visible bridges, change scale on
stairs, or disappear behind the wrong scenery.

The MoAD Visual Level Editor makes the painted background authoritative.
Designers draw support tracks directly beneath the actor's feet, assign
discrete depth by colour, record continuous physical height numerically, bind
transitions to real tracks, and paint hazards, events, obstacles, patrols, and
occlusion over the scene. The resulting `.moadmap` archive carries both the art
and the typed runtime contract.

## Repository map

| Path | Purpose |
| --- | --- |
| [`apps/level-editor`](apps/level-editor) | React, TypeScript, and Konva visual authoring tool |
| [`runtime`](runtime) | C# custom 2.5D world, support, combat, and narrative runtime |
| [`experiences/prologue`](experiences/prologue) | Browser-playable illustrated and voiced cinematic prologue |
| [`novella`](novella) | Canon covers and complete illustrated *Initium* PDF |
| [`site`](site) | Unified project and competition presentation |

The active stack intentionally excludes our rejected Godot and Unity
prototypes. They are research history, not production architecture.

## Editor model

- **Colour is depth:** yellow near, green middle, blue far.
- **Numbers are height:** every support point has a physical height in metres.
- **A support never changes depth:** it may change height to form slopes.
- **Walkable slopes stop at 55°:** steeper movement becomes a typed transition.
- **Transitions bind supports:** stairs, rung ladders, ledges, cliffs, and depth
  changes know their real source and destination.
- **Occlusion is authored:** painted masks tell the runtime which scenery must
  cover an actor.

## Run locally

### Visual editor

```powershell
cd apps\level-editor
npm ci
npm run dev
```

Then open `http://127.0.0.1:4173/`.

### Runtime and tests

```powershell
dotnet run --project runtime\Moad.Engine.Tests\Moad.Engine.Tests.csproj
dotnet run --project runtime\Moad.Runtime\Moad.Runtime.csproj
```

### Editor verification

```powershell
cd apps\level-editor
npm test
npm run lint
npm run build
```

## Built by Eugene + Kira

**Eugene Lyssovsky** is the creator, game and narrative designer, canon owner,
level author, producer, and final playtest gate.

**Kira, powered by Codex GPT-5.6**, is the persistent engineering collaborator:
architecture, implementation, integration, visual tooling, technical direction,
testing, documentation, and release preparation.

This is a deliberately visible human–AI studio practice. Codex is not hidden
behind a generic “AI assisted” footnote: the repository records a continuous
collaboration in which Eugene supplies authorship, design intent, taste, and
hard acceptance tests while Kira turns that intent into systems and artifacts,
audits failures, and carries fixes through verification.

## Current proof

The repository includes a semi-abandoned tomb `.moadmap` created in the editor.
It demonstrates multiple depth planes, variable physical heights, sloped
supports, stairs and ladders, occlusion, hazards, events, and enemy patrol
ranges. The editor can export both the complete archive and engine-facing JSON.

The downloadable Windows alpha remains an evolving investor build. The visual
editor and its serialization tests are the Build Week developer-tool proof.

## Rights

Source code is published for Build Week evaluation. Original characters,
story, prose, images, audio, and the *Mystery of Ancient Darkness* identity are
copyright © 2026 The Misfits Software and Eugene Lyssovsky. Third-party notices
remain with the relevant components.
