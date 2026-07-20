# MoAD Unreal Hybrid Spike

This directory is an isolated adapter for evaluating Unreal Engine as the
animation and scene-presentation layer of Mystery of Ancient Darkness.

The existing `.moadmap` contract remains authoritative. Unreal must consume
owner-authored supports, transitions, hazards, triggers, and occlusion data. It
must never infer or redraw invisible floors from the background image.

## Coordinate Contract

The generated scene uses Unreal's conventional axes:

- `X`: horizontal position in the painted level;
- `Y`: discrete depth plane (`near`, `middle`, `far`);
- `Z`: vertical screen position, inverted from image coordinates;
- `physical_height_cm`: independent gameplay height metadata.

One source pixel is converted to `100 / pixels_per_meter` Unreal centimetres.
This preserves the editor's scale: a 140 px Alice at 80 px/m becomes 175 cm.
An orthographic camera can therefore reproduce the painted path exactly while
lane scale remains controlled exclusively by the authored lane definition.

## Generate A Scene Contract

```powershell
python integrations/unreal/tools/moadmap_to_unreal.py `
  apps/level-editor/public/semi-abandoned-tomb.moadmap `
  integrations/unreal/generated/semi_abandoned_tomb
```

The output contains:

- `moad.unreal-scene.json`: normalized scene data for an Unreal importer;
- the exact background embedded in the `.moadmap` archive;
- `import-report.json`: validation evidence and object counts.

Run the standalone gate with:

```powershell
python -m unittest discover integrations/unreal/tests -v
```

## Spike Boundary

Unreal may own skeletal animation, Control Rig, IK, Sequencer, presentation,
and rendered proof capture. It does not own RURK combat rules, narrative state,
or level topology. Those remain in the existing runtime and `.moadmap` data.

The next stage is an Unreal 5.8 editor plugin that consumes the generated scene
contract and creates spline actors, depth-plane roots, the background plane,
and invariant checks through the official Unreal MCP server.

The initial C++ bridge is already scaffolded under `MoadHybrid/`. Once Unreal
5.8 is available, generate project files, compile the editor target, open an
empty level, and call `UMoadSceneImportSubsystem.ImportSceneContract` with the
absolute path to `moad.unreal-scene.json`.

See `HYBRID_ARCHITECTURE.md` for ownership boundaries and the proof gate.
