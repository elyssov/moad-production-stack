import bpy
import mathutils
import sys
from pathlib import Path


def output_argument() -> Path:
    if "--" not in sys.argv or len(sys.argv) <= sys.argv.index("--") + 1:
        raise SystemExit("Expected output FBX path after --")
    return Path(sys.argv[sys.argv.index("--") + 1]).resolve()


output_path = output_argument()
output_path.parent.mkdir(parents=True, exist_ok=True)

# First proof intentionally exports only the corrected nude body and its rig.
# Eyes, hair, and clothing remain separate integration gates.
export_names = {"CC_Base_Body", "Alice_Rig"}
missing = export_names - set(bpy.data.objects.keys())
if missing:
    raise RuntimeError(f"Missing required Alice objects: {sorted(missing)}")

for scene_object in bpy.data.objects:
    scene_object.select_set(False)
for name in export_names:
    bpy.data.objects[name].hide_set(False)
    bpy.data.objects[name].hide_viewport = False
    bpy.data.objects[name].select_set(True)
bpy.context.view_layer.objects.active = bpy.data.objects["Alice_Rig"]

# Bake the currently approved visible shape into the export copy. The v08 file
# keeps its final pelvis correction as an active shape key; Unreal otherwise
# imports the old Basis and initializes that morph at zero.
body = bpy.data.objects["CC_Base_Body"]
if body.data.shape_keys is not None:
    keys = body.data.shape_keys.key_blocks
    basis = keys[0]
    mixed = [point.co.copy() for point in basis.data]
    for key in keys[1:]:
        if key.value == 0.0:
            continue
        relative = key.relative_key
        for index, point in enumerate(key.data):
            mixed[index] += (point.co - relative.data[index].co) * key.value
    for index, coordinate in enumerate(mixed):
        basis.data[index].co = coordinate
        body.data.vertices[index].co = coordinate
    body.shape_key_clear()

# The legacy Alice scene stores centimetre-valued coordinates while declaring
# Blender metres. Normalize the baked body to Alice's approved 175 cm canon
# instead of preserving the source file's accidental 180.7-unit stature.
bpy.context.view_layer.update()
world_corners = [body.matrix_world @ mathutils.Vector(corner) for corner in body.bound_box]
source_height = max(point.z for point in world_corners) - min(point.z for point in world_corners)
target_height_source_units = 175.0
canon_ratio = target_height_source_units / source_height

# Unreal normalizes the FBX file-unit metadata, so changing only FBX
# `global_scale` does not change the imported skeletal bounds. Scale the root
# armature instead; the skinned body inherits this transform as one unit.
rig = bpy.data.objects["Alice_Rig"]
rig.scale = tuple(component * canon_ratio for component in rig.scale)
bpy.context.view_layer.update()

bpy.ops.export_scene.fbx(
    filepath=str(output_path),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    global_scale=0.01,
    apply_unit_scale=False,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Y",
    axis_up="Z",
    # Export the complete source armature. The reduced Alice rig is already
    # deformation-only; Blender's extra filter dropped the armature object
    # entirely in the validation FBX.
    use_armature_deform_only=False,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="COPY",
    embed_textures=True,
)

print(f"ALICE_SOURCE_HEIGHT={source_height:.6f}")
print(f"ALICE_CANON_RATIO={canon_ratio:.9f}")
print(f"ALICE_V08_NUDE_FBX={output_path}")
