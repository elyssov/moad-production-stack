import bpy
import sys
from pathlib import Path


def argument_after_double_dash() -> Path:
    args = sys.argv
    if "--" not in args or len(args) <= args.index("--") + 1:
        raise SystemExit("usage: blender <alice.blend> -b -P export_alice_unreal.py -- <output.fbx>")
    return Path(args[args.index("--") + 1]).resolve()


output_path = argument_after_double_dash()
output_path.parent.mkdir(parents=True, exist_ok=True)

export_names = {
    "CC_Base_Body",
    "Alice_Belt",
    "Alice_Boot_R",
    "Alice_Boot_L",
    "Alice_Breeches",
    "Alice_Holster",
    "Alice_Luger",
    "Alice_Shirt",
    "Alice_Strap_Belt",
    "Alice_Strap_Thigh",
    "Alice_Buttons",
    "Alice_Hair_v01",
    "Alice_Rig",
}

bpy.ops.object.select_all(action="DESELECT")
missing = sorted(export_names - set(bpy.data.objects.keys()))
if missing:
    raise RuntimeError(f"Alice export is missing required objects: {missing}")

for name in export_names:
    bpy.data.objects[name].select_set(True)
bpy.context.view_layer.objects.active = bpy.data.objects["Alice_Rig"]

# Export the accepted body, field outfit, weapon, and rig as one skeletal asset.
# Animation clips are imported in a later gate after the mesh survives UE intact.
bpy.ops.export_scene.fbx(
    filepath=str(output_path),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Y",
    axis_up="Z",
    use_armature_deform_only=True,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="COPY",
    embed_textures=True,
)

print(f"ALICE_FBX={output_path}")
