import bpy
import sys


path = sys.argv[sys.argv.index("--") + 1]
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=path)

for obj in bpy.context.scene.objects:
    print(
        "IMPORTED_OBJECT",
        obj.name,
        "type=",
        obj.type,
        "parent=",
        obj.parent.name if obj.parent else None,
    )
    if obj.type == "ARMATURE":
        print("IMPORTED_BONE_COUNT", len(obj.data.bones))
        print("IMPORTED_BONES", [bone.name for bone in obj.data.bones])
    elif obj.type == "MESH":
        print("IMPORTED_MESH_DIMENSIONS", tuple(obj.dimensions))
