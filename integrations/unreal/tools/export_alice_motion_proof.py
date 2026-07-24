import math
import sys
from pathlib import Path

import bpy
import mathutils


def output_argument() -> Path:
    if "--" not in sys.argv or len(sys.argv) <= sys.argv.index("--") + 1:
        raise SystemExit("Expected output FBX path after --")
    return Path(sys.argv[sys.argv.index("--") + 1]).resolve()


def bake_visible_shape(body):
    if body.data.shape_keys is None:
        return
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


def set_pose(rig, frame, rotations=None, locations=None):
    rotations = rotations or {}
    locations = locations or {}
    bpy.context.scene.frame_set(frame)
    for pose_bone in rig.pose.bones:
        pose_bone.rotation_mode = "XYZ"
        pose_bone.rotation_euler = (0.0, 0.0, 0.0)
        pose_bone.location = (0.0, 0.0, 0.0)

    # Alice's source bind is a T-pose. Lower both arms for every gameplay pose.
    rotations = {
        "CC_Base_L_UpperarmTwist01": (-78.0, 0.0, 0.0),
        "CC_Base_R_UpperarmTwist01": (-78.0, 0.0, 0.0),
        **rotations,
    }

    for name, degrees in rotations.items():
        rig.pose.bones[name].rotation_euler = tuple(math.radians(value) for value in degrees)
    for name, location in locations.items():
        rig.pose.bones[name].location = location

    for pose_bone in rig.pose.bones:
        pose_bone.keyframe_insert("rotation_euler", frame=frame, group=pose_bone.name)
        pose_bone.keyframe_insert("location", frame=frame, group=pose_bone.name)


output_path = output_argument()
output_path.parent.mkdir(parents=True, exist_ok=True)

body = bpy.data.objects["CC_Base_Body"]
rig = bpy.data.objects["Alice_Rig"]
bake_visible_shape(body)

for obj in bpy.data.objects:
    obj.select_set(False)
for obj in (body, rig):
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig

bpy.context.view_layer.update()
corners = [body.matrix_world @ mathutils.Vector(corner) for corner in body.bound_box]
source_height = max(point.z for point in corners) - min(point.z for point in corners)
canon_ratio = 175.0 / source_height
rig.scale = tuple(component * canon_ratio for component in rig.scale)

rig.animation_data_create()
action = bpy.data.actions.new("Alice_MotionProof")
rig.animation_data.action = action
bpy.context.scene.render.fps = 30
bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = 195

# Quiet standing pose.
set_pose(rig, 1)
set_pose(rig, 20)

# Walk: two readable in-place strides.
walk_left = {
    "CC_Base_Pelvis": (2.0, 0.0, -2.0),
    "CC_Base_Spine01": (-3.0, 0.0, 0.0),
    "CC_Base_L_ThighTwist01": (22.0, 0.0, 0.0),
    "CC_Base_R_ThighTwist01": (-18.0, 0.0, 0.0),
    "CC_Base_R_CalfTwist01": (28.0, 0.0, 0.0),
    "CC_Base_L_UpperarmTwist01": (-64.0, 0.0, 0.0),
    "CC_Base_R_UpperarmTwist01": (-92.0, 0.0, 0.0),
}
walk_right = {
    **walk_left,
    "CC_Base_Pelvis": (-2.0, 0.0, 2.0),
    "CC_Base_L_ThighTwist01": (-18.0, 0.0, 0.0),
    "CC_Base_R_ThighTwist01": (22.0, 0.0, 0.0),
    "CC_Base_L_CalfTwist01": (28.0, 0.0, 0.0),
    "CC_Base_R_CalfTwist01": (0.0, 0.0, 0.0),
    "CC_Base_L_UpperarmTwist01": (-92.0, 0.0, 0.0),
    "CC_Base_R_UpperarmTwist01": (-64.0, 0.0, 0.0),
}
set_pose(rig, 21, walk_left, {"CC_Base_Pelvis": (0.0, 0.0, 1.5)})
set_pose(rig, 36, walk_right, {"CC_Base_Pelvis": (0.0, 0.0, 0.0)})
set_pose(rig, 51, walk_left, {"CC_Base_Pelvis": (0.0, 0.0, 1.5)})
set_pose(rig, 66, walk_right, {"CC_Base_Pelvis": (0.0, 0.0, 0.0)})
set_pose(rig, 81, walk_left, {"CC_Base_Pelvis": (0.0, 0.0, 1.5)})

# Run: larger stride, forward torso, stronger airborne beat.
run_left = {
    "CC_Base_Pelvis": (3.0, 0.0, -3.0),
    "CC_Base_Spine01": (-10.0, 0.0, 0.0),
    "CC_Base_L_ThighTwist01": (38.0, 0.0, 0.0),
    "CC_Base_R_ThighTwist01": (-30.0, 0.0, 0.0),
    "CC_Base_R_CalfTwist01": (48.0, 0.0, 0.0),
    "CC_Base_L_UpperarmTwist01": (-52.0, 0.0, 0.0),
    "CC_Base_R_UpperarmTwist01": (-108.0, 0.0, 0.0),
}
run_right = {
    **run_left,
    "CC_Base_Pelvis": (-3.0, 0.0, 3.0),
    "CC_Base_L_ThighTwist01": (-30.0, 0.0, 0.0),
    "CC_Base_R_ThighTwist01": (38.0, 0.0, 0.0),
    "CC_Base_L_CalfTwist01": (48.0, 0.0, 0.0),
    "CC_Base_R_CalfTwist01": (0.0, 0.0, 0.0),
    "CC_Base_L_UpperarmTwist01": (-108.0, 0.0, 0.0),
    "CC_Base_R_UpperarmTwist01": (-52.0, 0.0, 0.0),
}
set_pose(rig, 82, run_left, {"CC_Base_Pelvis": (0.0, 0.0, 2.0)})
set_pose(rig, 94, run_right, {"CC_Base_Pelvis": (0.0, 0.0, 5.0)})
set_pose(rig, 106, run_left, {"CC_Base_Pelvis": (0.0, 0.0, 2.0)})
set_pose(rig, 118, run_right, {"CC_Base_Pelvis": (0.0, 0.0, 5.0)})
set_pose(rig, 130, run_left, {"CC_Base_Pelvis": (0.0, 0.0, 2.0)})

# Jump, fall, and landing beats.
set_pose(
    rig,
    136,
    {
        "CC_Base_Pelvis": (10.0, 0.0, 0.0),
        "CC_Base_Spine01": (-12.0, 0.0, 0.0),
        "CC_Base_L_ThighTwist01": (24.0, 0.0, 0.0),
        "CC_Base_R_ThighTwist01": (24.0, 0.0, 0.0),
        "CC_Base_L_CalfTwist01": (48.0, 0.0, 0.0),
        "CC_Base_R_CalfTwist01": (48.0, 0.0, 0.0),
    },
    {"CC_Base_Pelvis": (0.0, 0.0, -6.0)},
)
set_pose(
    rig,
    146,
    {
        "CC_Base_Pelvis": (-4.0, 0.0, 0.0),
        "CC_Base_Spine01": (5.0, 0.0, 0.0),
        "CC_Base_L_ThighTwist01": (-8.0, 0.0, 0.0),
        "CC_Base_R_ThighTwist01": (10.0, 0.0, 0.0),
        "CC_Base_L_CalfTwist01": (22.0, 0.0, 0.0),
        "CC_Base_R_CalfTwist01": (18.0, 0.0, 0.0),
        "CC_Base_L_UpperarmTwist01": (-40.0, 0.0, 0.0),
        "CC_Base_R_UpperarmTwist01": (-40.0, 0.0, 0.0),
    },
    {"CC_Base_Pelvis": (0.0, 0.0, 10.0)},
)
set_pose(
    rig,
    158,
    {
        "CC_Base_L_ThighTwist01": (12.0, 0.0, 0.0),
        "CC_Base_R_ThighTwist01": (-10.0, 0.0, 0.0),
        "CC_Base_L_CalfTwist01": (30.0, 0.0, 0.0),
        "CC_Base_R_CalfTwist01": (36.0, 0.0, 0.0),
        "CC_Base_L_UpperarmTwist01": (-50.0, 0.0, 0.0),
        "CC_Base_R_UpperarmTwist01": (-50.0, 0.0, 0.0),
    },
    {"CC_Base_Pelvis": (0.0, 0.0, 18.0)},
)
set_pose(
    rig,
    171,
    {
        "CC_Base_Pelvis": (6.0, 0.0, 0.0),
        "CC_Base_Spine01": (-8.0, 0.0, 0.0),
        "CC_Base_L_ThighTwist01": (12.0, 0.0, 0.0),
        "CC_Base_R_ThighTwist01": (12.0, 0.0, 0.0),
        "CC_Base_L_CalfTwist01": (25.0, 0.0, 0.0),
        "CC_Base_R_CalfTwist01": (25.0, 0.0, 0.0),
    },
    {"CC_Base_Pelvis": (0.0, 0.0, 4.0)},
)
set_pose(
    rig,
    180,
    {
        "CC_Base_Pelvis": (12.0, 0.0, 0.0),
        "CC_Base_Spine01": (-10.0, 0.0, 0.0),
        "CC_Base_L_ThighTwist01": (22.0, 0.0, 0.0),
        "CC_Base_R_ThighTwist01": (22.0, 0.0, 0.0),
        "CC_Base_L_CalfTwist01": (45.0, 0.0, 0.0),
        "CC_Base_R_CalfTwist01": (45.0, 0.0, 0.0),
    },
    {"CC_Base_Pelvis": (0.0, 0.0, -7.0)},
)
set_pose(rig, 195)

bpy.ops.export_scene.fbx(
    filepath=str(output_path),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    global_scale=0.01,
    apply_unit_scale=False,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Y",
    axis_up="Z",
    use_armature_deform_only=False,
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=0.0,
    path_mode="COPY",
    embed_textures=True,
)

print(f"ALICE_MOTION_PROOF_FBX={output_path}")
print(f"ALICE_MOTION_FRAMES={bpy.context.scene.frame_start}-{bpy.context.scene.frame_end}")
