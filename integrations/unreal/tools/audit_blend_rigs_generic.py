import bpy


for obj in bpy.data.objects:
    if obj.type == "ARMATURE":
        print(
            "ARMATURE",
            obj.name,
            "bones=",
            len(obj.data.bones),
            "deform=",
            sum(1 for bone in obj.data.bones if bone.use_deform),
            "hidden=",
            obj.hide_get(),
        )
        print("BONES", obj.name, [bone.name for bone in obj.data.bones])
        for bone_name in (
            "CC_Base_L_Upperarm",
            "CC_Base_R_Upperarm",
            "CC_Base_L_Forearm",
            "CC_Base_L_Thigh",
            "CC_Base_L_Calf",
            "CC_Base_Spine01",
        ):
            bone = obj.data.bones.get(bone_name)
            if bone is None:
                continue
            axes = bone.matrix_local.to_3x3()
            print(
                "AXES",
                bone_name,
                "x=",
                tuple(round(value, 4) for value in axes.col[0]),
                "y=",
                tuple(round(value, 4) for value in axes.col[1]),
                "z=",
                tuple(round(value, 4) for value in axes.col[2]),
            )
    elif obj.type == "MESH" and (obj.vertex_groups or "body" in obj.name.lower()):
        print(
            "MESH",
            obj.name,
            "vertices=",
            len(obj.data.vertices),
            "groups=",
            len(obj.vertex_groups),
            "parent=",
            obj.parent.name if obj.parent else None,
        )
