import math
import sys
from pathlib import Path

import bpy


if "--" not in sys.argv or len(sys.argv) <= sys.argv.index("--") + 1:
    raise SystemExit("Expected output FBX path after --")
output_path = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
output_path.parent.mkdir(parents=True, exist_ok=True)

body = bpy.data.objects["CC_Base_Body"]
rig = bpy.data.objects["Alice_FullRig"]
if len(rig.data.bones) != 101:
    raise RuntimeError(f"Expected 101 bones, found {len(rig.data.bones)}")

for obj in bpy.data.objects:
    obj.select_set(False)
for obj in (body, rig):
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig

rig.animation_data_create()
action = bpy.data.actions.new("Alice_FullRig_MotionProof")
rig.animation_data.action = action
scene = bpy.context.scene
scene.render.fps = 30
scene.frame_start = 1
scene.frame_end = 195


def set_pose(frame, rotations=None, locations=None):
    rotations = rotations or {}
    locations = locations or {}
    scene.frame_set(frame)
    for pose_bone in rig.pose.bones:
        pose_bone.rotation_mode = "XYZ"
        pose_bone.rotation_euler = (0.0, 0.0, 0.0)
        pose_bone.location = (0.0, 0.0, 0.0)

    # Mirrored local Z rotations lower the T-pose arms without twisting them.
    base_rotations = {
        "CC_Base_L_Upperarm": (0.0, 0.0, -82.0),
        "CC_Base_R_Upperarm": (0.0, 0.0, 82.0),
        "CC_Base_L_Forearm": (0.0, 0.0, -8.0),
        "CC_Base_R_Forearm": (0.0, 0.0, 8.0),
    }
    base_rotations.update(rotations)
    for name, degrees in base_rotations.items():
        rig.pose.bones[name].rotation_euler = tuple(math.radians(value) for value in degrees)
    for name, location in locations.items():
        rig.pose.bones[name].location = location

    for pose_bone in rig.pose.bones:
        pose_bone.keyframe_insert("rotation_euler", frame=frame, group=pose_bone.name)
        pose_bone.keyframe_insert("location", frame=frame, group=pose_bone.name)


set_pose(1)
set_pose(20)

walk_left = {
    "CC_Base_Spine01": (4.0, 0.0, 0.0),
    "CC_Base_L_Thigh": (20.0, 0.0, 0.0),
    "CC_Base_R_Thigh": (16.0, 0.0, 0.0),
    "CC_Base_R_Calf": (28.0, 0.0, 0.0),
}
walk_right = {
    "CC_Base_Spine01": (4.0, 0.0, 0.0),
    "CC_Base_L_Thigh": (-16.0, 0.0, 0.0),
    "CC_Base_R_Thigh": (-20.0, 0.0, 0.0),
    "CC_Base_L_Calf": (-28.0, 0.0, 0.0),
}
set_pose(21, walk_left, {"CC_Base_Hip": (0.0, 0.0, 1.0)})
set_pose(36, walk_right, {"CC_Base_Hip": (0.0, 0.0, 0.0)})
set_pose(51, walk_left, {"CC_Base_Hip": (0.0, 0.0, 1.0)})
set_pose(66, walk_right, {"CC_Base_Hip": (0.0, 0.0, 0.0)})
set_pose(81, walk_left, {"CC_Base_Hip": (0.0, 0.0, 1.0)})

run_left = {
    "CC_Base_Spine01": (10.0, 0.0, 0.0),
    "CC_Base_L_Thigh": (34.0, 0.0, 0.0),
    "CC_Base_R_Thigh": (28.0, 0.0, 0.0),
    "CC_Base_R_Calf": (48.0, 0.0, 0.0),
}
run_right = {
    "CC_Base_Spine01": (10.0, 0.0, 0.0),
    "CC_Base_L_Thigh": (-28.0, 0.0, 0.0),
    "CC_Base_R_Thigh": (-34.0, 0.0, 0.0),
    "CC_Base_L_Calf": (-48.0, 0.0, 0.0),
}
set_pose(82, run_left, {"CC_Base_Hip": (0.0, 0.0, 2.0)})
set_pose(94, run_right, {"CC_Base_Hip": (0.0, 0.0, 4.0)})
set_pose(106, run_left, {"CC_Base_Hip": (0.0, 0.0, 2.0)})
set_pose(118, run_right, {"CC_Base_Hip": (0.0, 0.0, 4.0)})
set_pose(130, run_left, {"CC_Base_Hip": (0.0, 0.0, 2.0)})

set_pose(
    136,
    {
        "CC_Base_Spine01": (12.0, 0.0, 0.0),
        "CC_Base_L_Thigh": (24.0, 0.0, 0.0),
        "CC_Base_R_Thigh": (-24.0, 0.0, 0.0),
        "CC_Base_L_Calf": (-50.0, 0.0, 0.0),
        "CC_Base_R_Calf": (50.0, 0.0, 0.0),
    },
    {"CC_Base_Hip": (0.0, 0.0, -6.0)},
)
set_pose(
    146,
    {
        "CC_Base_Spine01": (-4.0, 0.0, 0.0),
        "CC_Base_L_Thigh": (-8.0, 0.0, 0.0),
        "CC_Base_R_Thigh": (8.0, 0.0, 0.0),
        "CC_Base_L_Calf": (-20.0, 0.0, 0.0),
        "CC_Base_R_Calf": (20.0, 0.0, 0.0),
    },
    {"CC_Base_Hip": (0.0, 0.0, 10.0)},
)
set_pose(
    158,
    {
        "CC_Base_L_Thigh": (8.0, 0.0, 0.0),
        "CC_Base_R_Thigh": (-8.0, 0.0, 0.0),
        "CC_Base_L_Calf": (-28.0, 0.0, 0.0),
        "CC_Base_R_Calf": (28.0, 0.0, 0.0),
    },
    {"CC_Base_Hip": (0.0, 0.0, 18.0)},
)
set_pose(
    171,
    {
        "CC_Base_Spine01": (8.0, 0.0, 0.0),
        "CC_Base_L_Thigh": (12.0, 0.0, 0.0),
        "CC_Base_R_Thigh": (-12.0, 0.0, 0.0),
        "CC_Base_L_Calf": (-30.0, 0.0, 0.0),
        "CC_Base_R_Calf": (30.0, 0.0, 0.0),
    },
    {"CC_Base_Hip": (0.0, 0.0, 4.0)},
)
set_pose(
    180,
    {
        "CC_Base_Spine01": (12.0, 0.0, 0.0),
        "CC_Base_L_Thigh": (22.0, 0.0, 0.0),
        "CC_Base_R_Thigh": (-22.0, 0.0, 0.0),
        "CC_Base_L_Calf": (-45.0, 0.0, 0.0),
        "CC_Base_R_Calf": (45.0, 0.0, 0.0),
    },
    {"CC_Base_Hip": (0.0, 0.0, -7.0)},
)
set_pose(195)

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

print(f"ALICE_FULLRIG_MOTION_FBX={output_path}")
