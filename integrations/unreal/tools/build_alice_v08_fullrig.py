import sys
from pathlib import Path

import bpy
import mathutils


if "--" not in sys.argv or len(sys.argv) < sys.argv.index("--") + 4:
    raise SystemExit("Expected rig donor .blend, output .blend, and output .fbx after --")
args = sys.argv[sys.argv.index("--") + 1 :]
donor_path = Path(args[0]).resolve()
blend_output = Path(args[1]).resolve()
fbx_output = Path(args[2]).resolve()
blend_output.parent.mkdir(parents=True, exist_ok=True)
fbx_output.parent.mkdir(parents=True, exist_ok=True)

body = bpy.data.objects["CC_Base_Body"]
old_rig = bpy.data.objects["Alice_Rig"]

# Bake the approved visible pelvis correction while preserving the accepted
# body vertex order and all 78 skin-weight groups.
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

with bpy.data.libraries.load(str(donor_path), link=False) as (data_from, data_to):
    if "Armature" not in data_from.objects:
        raise RuntimeError(f"Donor has no Armature object: {donor_path}")
    data_to.objects = ["Armature"]

full_rig = data_to.objects[0]
bpy.context.scene.collection.objects.link(full_rig)
full_rig.name = "Alice_FullRig"
full_rig.data.name = "Alice_FullRig"
full_rig.hide_set(False)
full_rig.hide_viewport = False

if len(full_rig.data.bones) != 101:
    raise RuntimeError(f"Expected 101 donor bones, found {len(full_rig.data.bones)}")
if len(body.data.vertices) != 14164 or len(body.vertex_groups) != 78:
    raise RuntimeError(
        f"Unexpected approved body topology: {len(body.data.vertices)} vertices, "
        f"{len(body.vertex_groups)} groups"
    )

body_world = body.matrix_world.copy()
body.parent = full_rig
body.matrix_parent_inverse = full_rig.matrix_world.inverted()
body.matrix_world = body_world
for modifier in list(body.modifiers):
    if modifier.type == "ARMATURE":
        body.modifiers.remove(modifier)
modifier = body.modifiers.new("Alice_FullRig", "ARMATURE")
modifier.object = full_rig
modifier.use_deform_preserve_volume = True

# Normalize the root once so mesh and skeleton remain one coherent 175 cm unit.
bpy.context.view_layer.update()
corners = [body.matrix_world @ mathutils.Vector(corner) for corner in body.bound_box]
source_height = max(point.z for point in corners) - min(point.z for point in corners)
canon_ratio = 175.0 / source_height
full_rig.scale = tuple(component * canon_ratio for component in full_rig.scale)
bpy.context.view_layer.update()

for obj in bpy.data.objects:
    obj.select_set(False)
for obj in (body, full_rig):
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.select_set(True)
bpy.context.view_layer.objects.active = full_rig

bpy.ops.wm.save_as_mainfile(filepath=str(blend_output))
bpy.ops.export_scene.fbx(
    filepath=str(fbx_output),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    global_scale=0.01,
    apply_unit_scale=False,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Y",
    axis_up="Z",
    use_armature_deform_only=False,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="COPY",
    embed_textures=True,
)

print(f"ALICE_FULLRIG_BLEND={blend_output}")
print(f"ALICE_FULLRIG_FBX={fbx_output}")
print(f"ALICE_FULLRIG_BONES={len(full_rig.data.bones)}")
print(f"ALICE_FULLRIG_HEIGHT_RATIO={canon_ratio:.9f}")
