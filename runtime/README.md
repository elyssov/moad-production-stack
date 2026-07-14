# MoAD Custom Runtime

The runtime is the consumer of the visual editor's level contract.

## Modules

- `Moad.Engine`: authoritative world, support resolution, 2.5D combat geometry,
  RURK-derived combat, and narrative decisions.
- `Moad.Runtime`: the current playable C# runtime and renderer.
- `Moad.Engine.Tests`: executable regression suite covering the engine contract.

Depth and height are independent. A visible floor on a different depth plane
cannot silently catch an actor; a support surface on the actor's current plane
can. Transitions explicitly move the actor between supports and planes.
