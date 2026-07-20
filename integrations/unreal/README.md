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

## Unreal 5.8 Spike

The editor target builds against the launcher distribution of Unreal 5.8. It
enables Epic's official Model Context Protocol server plus the editor,
animation, and physics toolsets. The project-local Codex endpoint is stored in
`MoadHybrid/.codex/config.toml`; the server starts automatically on port 8000.

Before opening the map on a new machine, copy Epic's installed Quinn/Manny rig,
Control Rig, and pistol animation fixtures into the project:

```powershell
./integrations/unreal/tools/sync_ue_template_content.ps1
```

The Epic-owned binary assets remain outside Git. The reproducible import script
creates `/Game/Maps/SemiAbandonedTomb`, imports all authored splines, places a
rigged Quinn proxy on `near_floor`, assigns the pistol idle pose, and saves the
level:

```powershell
& "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe" `
  ./integrations/unreal/MoadHybrid/MoadHybrid.uproject `
  -ExecutePythonScript=./integrations/unreal/scripts/import_scene_contract.py `
  -unattended -nop4 -nosplash
```

`UMoadSceneImportSubsystem` treats `ContractId` as stable identity and lets
Unreal assign transient object names, so repeated replace-imports are safe.

See `HYBRID_ARCHITECTURE.md` for ownership boundaries and the proof gate.
