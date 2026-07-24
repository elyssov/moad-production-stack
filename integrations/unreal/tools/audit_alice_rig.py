import bpy


for name in ("CC_Base_Body", "Alice_Rig"):
    obj = bpy.data.objects[name]
    print(
        "OBJECT",
        name,
        "type=",
        obj.type,
        "parent=",
        obj.parent.name if obj.parent else None,
        "parent_type=",
        obj.parent_type,
        "hide_get=",
        obj.hide_get(),
        "hide_viewport=",
        obj.hide_viewport,
        "hide_render=",
        obj.hide_render,
        "visible=",
        obj.visible_get(),
    )
    for modifier in obj.modifiers:
        print(
            "MODIFIER",
            name,
            modifier.name,
            modifier.type,
            "object=",
            getattr(getattr(modifier, "object", None), "name", None),
        )

body = bpy.data.objects["CC_Base_Body"]
print("VERTEX_GROUP_COUNT", len(body.vertex_groups))
print("VERTEX_GROUPS", [group.name for group in body.vertex_groups])

rig = bpy.data.objects["Alice_Rig"]
print("BONE_COUNT", len(rig.data.bones))
print("DEFORM_BONE_COUNT", sum(1 for bone in rig.data.bones if bone.use_deform))
for bone in rig.data.bones:
    print(
        "BONE",
        bone.name,
        "parent=",
        bone.parent.name if bone.parent else None,
        "deform=",
        bone.use_deform,
    )

for name in (
    "CC_Base_Pelvis",
    "CC_Base_Spine01",
    "CC_Base_L_UpperarmTwist01",
    "CC_Base_L_ForearmTwist01",
    "CC_Base_L_ThighTwist01",
    "CC_Base_L_ThighTwist02",
    "CC_Base_L_CalfTwist01",
):
    bone = rig.data.bones[name]
    matrix = bone.matrix_local.to_3x3()
    print(
        "BONE_AXES",
        name,
        "head=",
        tuple(round(v, 4) for v in bone.head_local),
        "tail=",
        tuple(round(v, 4) for v in bone.tail_local),
        "x=",
        tuple(round(v, 4) for v in matrix.col[0]),
        "y=",
        tuple(round(v, 4) for v in matrix.col[1]),
        "z=",
        tuple(round(v, 4) for v in matrix.col[2]),
    )
