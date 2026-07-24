import sys
from pathlib import Path

import bpy
from mathutils import Vector


if "--" not in sys.argv:
    raise SystemExit("Expected FBX path and output directory after --")
values = sys.argv[sys.argv.index("--") + 1 :]
fbx_path = Path(values[0]).resolve()
output_dir = Path(values[1]).resolve()
output_dir.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(fbx_path), use_anim=True)

for action in bpy.data.actions:
    print("IMPORTED_ACTION", action.name, "frame_range=", tuple(action.frame_range))
for obj in bpy.context.scene.objects:
    if obj.type == "ARMATURE":
        print(
            "ARMATURE_ACTION",
            obj.name,
            obj.animation_data.action.name if obj.animation_data and obj.animation_data.action else None,
        )

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x = 512
scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.world.color = (0.025, 0.025, 0.025)

camera_data = bpy.data.cameras.new("ReviewCamera")
camera = bpy.data.objects.new("ReviewCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera_data.type = "ORTHO"
camera_data.ortho_scale = 2.15
target = Vector((0.0, 0.0, 0.92))

for view_name, camera_location in (
    ("front", (0.0, -4.0, 0.92)),
    ("side", (-4.0, 0.0, 0.92)),
):
    camera.location = camera_location
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    for frame in (1, 21, 36, 82, 94, 136, 146, 158, 171, 180, 195):
        scene.frame_set(frame)
        scene.render.filepath = str(
            output_dir / f"alice_motion_{view_name}_{frame:03d}.png"
        )
        bpy.ops.render.render(write_still=True)
        print(f"RENDERED_VIEW={view_name} FRAME={frame}")
