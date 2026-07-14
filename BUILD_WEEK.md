# OpenAI Build Week 2026 Submission Notes

## Project

**Mystery of Ancient Darkness Production Stack**

Category: **Developer Tools**

Short pitch:

> Paint playable 2.5D worlds directly over concept art. The MoAD Visual Level
> Editor turns depth, height, stairs, ladders, hazards, occlusion, events, and
> patrols into a typed runtime contract without separating physics from the
> picture the player sees.

## The problem

Painterly 2.5D games often author beautiful backgrounds and invisible collision
in different tools. The resulting drift creates floating actors, false floors,
wrong scale changes, broken staircases, and expensive iteration between artists,
designers, and programmers.

## The solution

The MoAD Visual Level Editor treats the painted scene as the authoring surface:

- support lines are drawn directly under the character's feet;
- colour encodes discrete depth;
- numeric values encode continuous physical height;
- transitions bind real source and destination supports;
- hazards, events, obstacles, patrols, and occlusion are painted in place;
- `.moadmap` bundles background art, editable source, and engine-ready JSON;
- a custom C# runtime consumes the same contract.

## Working proof

The hosted editor includes a one-click **Demo tomb** map with:

- 10 support tracks across near, middle, and far depth planes;
- 6 bound transitions including steps, a rung ladder, a depth walk, an unstable
  crossing, and a lift;
- continuous physical height and a 55° walkable-slope gate;
- 2 hazards, 2 event volumes, 2 occlusion masks, an obstacle, 3 enemies, and
  patrol ranges;
- zero validation errors or declared technical debt.

## Built with Codex GPT-5.6

Kira, powered by Codex GPT-5.6, worked as Eugene's persistent engineering
collaborator rather than as a one-shot code generator. The collaboration covered:

1. auditing a failed engine-first architecture and identifying the painted-art /
   invisible-collision mismatch;
2. designing the independent depth + numeric height model;
3. implementing the React, TypeScript, and Konva editor;
4. defining support-bound transitions and `.moadmap` serialization;
5. integrating the contract with the custom C# runtime;
6. building regression tests, the proof map, the public site, and release gates;
7. iterating continuously against Eugene's direct playtest and visual feedback.

## Suggested three-minute demo

1. **0:00–0:20** — show the painted tomb and explain the invisible-collision
   problem.
2. **0:20–0:50** — click Demo tomb and identify depth colours and numeric
   heights.
3. **0:50–1:25** — draw a support, finish with Space, reopen it, and adjust a
   point.
4. **1:25–1:55** — bind a stone-step transition between two supports.
5. **1:55–2:20** — paint an event or occlusion region and place an enemy patrol.
6. **2:20–2:40** — export runtime JSON and show the `.moadmap` contents.
7. **2:40–3:00** — show the custom runtime, prologue, novella, and the Eugene +
   Kira collaboration statement.

## Submission checklist

- [x] Public source repository
- [x] Hosted working editor
- [x] Reproducible tests and builds
- [x] Project description and README
- [x] Public proof map
- [x] Windows alpha artifact
- [ ] Public demo video under three minutes
- [ ] Codex `/feedback` session ID
- [ ] Final Devpost signature and submission by Eugene
