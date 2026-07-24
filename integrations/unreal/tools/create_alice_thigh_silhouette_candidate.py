import bpy
import json
import math
import os
import sys


def smoothstep(edge0, edge1, value):
    if edge0 == edge1:
        return 0.0
    t = max(0.0, min(1.0, (value - edge0) / (edge1 - edge0)))
    return t * t * (3.0 - 2.0 * t)


def bell(value, low, peak_low, peak_high, high):
    rise = smoothstep(low, peak_low, value)
    fall = 1.0 - smoothstep(peak_high, high, value)
    return max(0.0, min(rise, fall))


args = sys.argv[sys.argv.index("--") + 1 :]
output_blend = args[0]
report_path = args[1]

body = bpy.data.objects.get("CC_Base_Body")
if body is None or body.type != "MESH":
    raise RuntimeError("CC_Base_Body mesh was not found")

bpy.context.view_layer.objects.active = body
body.select_set(True)

if body.data.shape_keys is None:
    body.shape_key_add(name="Basis", from_mix=False)

key_name = "Alice_ThighSilhouetteFix_v01"
existing = body.data.shape_keys.key_blocks.get(key_name)
if existing is not None:
    body.shape_key_remove(existing)

candidate = body.shape_key_add(name=key_name, from_mix=False)
candidate.value = 1.0

matrix = body.matrix_world
inverse = matrix.inverted()
moved = 0
max_shift_cm = 0.0

for index, vertex in enumerate(body.data.vertices):
    world = matrix @ vertex.co
    abs_x = abs(world.x)

    # The correction is limited to the outer upper thigh. It fades out before
    # the pelvis and knee, and never touches the inner thigh surface.
    height_weight = bell(world.z, 54.0, 68.0, 86.0, 106.0)
    outer_weight = smoothstep(10.0, 18.0, abs_x)
    weight = height_weight * outer_weight
    if weight <= 0.0:
        continue

    shift = 2.2 * weight
    world.x -= math.copysign(shift, world.x)
    candidate.data[index].co = inverse @ world
    moved += 1
    max_shift_cm = max(max_shift_cm, shift)

os.makedirs(os.path.dirname(output_blend), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=output_blend)

report = {
    "source": bpy.data.filepath,
    "candidate": output_blend,
    "mesh": body.name,
    "shape_key": key_name,
    "moved_vertices": moved,
    "total_vertices": len(body.data.vertices),
    "max_lateral_shift_cm": round(max_shift_cm, 4),
    "height_gate_cm": [54.0, 68.0, 86.0, 106.0],
    "outer_surface_gate_cm": [10.0, 18.0],
}
with open(report_path, "w", encoding="utf-8") as handle:
    json.dump(report, handle, indent=2)

print(json.dumps(report, indent=2))
