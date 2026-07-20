# MoAD Hybrid Architecture

## Authority

| Concern | Authority | Unreal role |
| --- | --- | --- |
| Level topology | `.moadmap` | Read-only consumer |
| Supports and transitions | `.moadmap` | Spline visualization and traversal playback |
| Depth and physical height | `.moadmap` | Scene placement and animation parameters |
| RURK combat | `Moad.Engine` | Presentation of commands and results |
| Narrative state and choices | `Moad.Engine` | Sequencer and overlay presentation |
| Character animation | Unreal | Authoritative pose playback and optional sprite bake |
| Visual acceptance | Kira gate | Screenshots, captures, and measured invariants |

Unreal must not invent supports, transitions, combat outcomes, or narrative
state. A visually plausible Unreal scene is a failure when it disagrees with
the map or engine contract.

## Runtime Options Under Test

### A. Unreal Presentation Runtime

The existing engine emits typed state and events. Unreal renders the world,
plays animation, runs Sequencer, and reports player intent back through a thin
adapter. This gives the best animation quality but keeps Unreal in the shipped
product.

### B. Unreal Animation Baker

Unreal retargets, rigs, corrects, and renders Alice and enemies into high
resolution frame sequences. The existing custom runtime remains the shipped
game. This gives simpler deployment and preserves deterministic image-space
movement, but loses live skeletal blending and IK.

The spike must implement enough of both paths to compare them using the same
background, map, character rig, and animations.

## MCP Tool Surface

After Unreal 5.8 and its official MCP plugin are installed, expose a bounded
MoAD toolset instead of unrestricted editor improvisation:

- `moad_import_map(path)`
- `moad_rebuild_generated_scene()`
- `moad_validate_track_graph()`
- `moad_validate_lane_scale()`
- `moad_measure_feet_to_support()`
- `moad_bind_transition_clip(transition_id, animation_asset)`
- `moad_capture_proof(name)`
- `moad_run_spike_gate()`

All generated actors carry the `MoadGenerated` tag. Reimport may replace only
those actors. Hand-authored character rigs, animation assets, cameras, and
lighting are outside the replaceable set.

## Visual Coordinate Law

The editor's painted line remains the visible support. The adapter maps it to
orthographic image space without guessing geometry:

```text
X = screen_x * (100 / pixels_per_meter)
Y = lane_index * depth_spacing_cm
Z = (image_height - screen_y) * (100 / pixels_per_meter)
```

Physical height is preserved as separate metadata and remains available to
combat, falling, and transition rules. Actor scale is read from the depth lane;
changing vertical support inside one lane cannot change scale.

## Proof Gate

The Unreal runtime path passes only when all of these are demonstrated on the
same imported map:

1. The background and every support originate from one `.moadmap` import.
2. Alice's feet remain within 2 source pixels of the authored support during
   idle, walk, run, and slope traversal.
3. Vertical movement inside a lane changes no character scale.
4. Every transition starts and ends on its bound supports.
5. Stairs, rung ladder, cliff, depth walk, and unstable bridge use distinct
   animation states.
6. Stop, turn, armed walk, shot, reload, crouch, and hit reactions do not snap
   scale or remove the weapon unexpectedly.
7. The scene can be rebuilt and validated through Codex and Unreal MCP without
   manually touching spline points.
8. A proof capture is visually stronger than the current sprite runtime.

Until all eight pass, the result is a `SPIKE`, not a migration decision.
