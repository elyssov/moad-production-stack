from pathlib import Path
import os
import sys

import unreal


def argument(index: int, label: str) -> str:
    args = sys.argv
    if "--" not in args:
        raise RuntimeError(f"missing -- arguments; expected {label}")
    values = args[args.index("--") + 1 :]
    if index >= len(values):
        raise RuntimeError(f"missing argument: {label}")
    return values[index]


fbx_value = os.environ.get("MOAD_ALICE_FBX")
fbx_path = Path(fbx_value or argument(0, "Alice FBX path")).resolve()
destination = "/Game/MoAD/Characters/Alice"

if not fbx_path.is_file():
    raise RuntimeError(f"Alice FBX does not exist: {fbx_path}")

unreal.EditorAssetLibrary.make_directory(destination)

options = unreal.FbxImportUI()
options.import_as_skeletal = True
options.import_mesh = True
options.import_animations = False
options.import_materials = True
options.import_textures = True
options.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
if hasattr(options, "import_morph_targets"):
    options.import_morph_targets = True
elif hasattr(options.skeletal_mesh_import_data, "import_morph_targets"):
    options.skeletal_mesh_import_data.import_morph_targets = True
for property_name, value in (
    ("update_skeleton_reference_pose", False),
    ("use_t0_as_ref_pose", False),
    ("import_meshes_in_bone_hierarchy", True),
):
    if hasattr(options.skeletal_mesh_import_data, property_name):
        setattr(options.skeletal_mesh_import_data, property_name, value)

task = unreal.AssetImportTask()
task.filename = str(fbx_path)
task.destination_path = destination
task.destination_name = "SK_Alice_Compton_Field"
task.automated = True
task.replace_existing = True
task.replace_existing_settings = True
task.save = True
task.options = options

unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
if not task.imported_object_paths:
    raise RuntimeError("Unreal imported no Alice assets")

for asset_path in task.imported_object_paths:
    unreal.EditorAssetLibrary.save_asset(asset_path, only_if_is_dirty=False)
    print(f"ALICE_ASSET={asset_path}")
