import json
from pathlib import Path

import unreal


PROJECT_ROOT = Path(r"C:\projects\moad-unreal-spike")
FBX_PATH = (
    PROJECT_ROOT
    / "integrations/unreal/MoadHybrid/Saved/Import/Alice/"
    "Alice_Compton_Nude_v08_FullRig_Canon175.fbx"
)
PROOF_ROOT = (
    PROJECT_ROOT
    / "integrations/unreal/MoadHybrid/Saved/Proofs/alice_feminine_locomotion"
)

ALICE_FOLDER = "/Game/MoAD/Characters/AliceV08/FullRig"
ALICE_MESH_PATH = f"{ALICE_FOLDER}/SK_Alice_Nude_v08_FullRig_Canon175"
RETARGET_FOLDER = "/Game/MoAD/Characters/AliceV08/Retarget"
SOURCE_IK_PATH = f"{RETARGET_FOLDER}/IK_Quinn_MoAD_Source"
TARGET_IK_PATH = f"{RETARGET_FOLDER}/IK_Alice_v08_Target"
RETARGETER_PATH = f"{RETARGET_FOLDER}/RTG_Quinn_To_Alice_v08"
OUTPUT_FOLDER = "/Game/MoAD/Characters/AliceV08/Animations/Proof"

QUINN_MESH_PATH = "/Game/Characters/Mannequins/Meshes/SKM_Quinn_Simple"
SOURCE_ANIM_PATHS = [
    "/Game/Characters/Mannequins/Anims/Unarmed/Walk/MF_Unarmed_Walk_Fwd",
    "/Game/Characters/Mannequins/Anims/Unarmed/Jog/MF_Unarmed_Jog_Fwd",
    "/Game/Characters/Mannequins/Anims/Pistol/MF_Pistol_Idle_ADS",
    "/Game/Characters/Mannequins/Anims/Pistol/Walk/MF_Pistol_Walk_Fwd",
    "/Game/Characters/Mannequins/Anims/Pistol/Jog/MF_Pistol_Jog_Fwd",
]


def require_asset(path, expected_type=None):
    asset = unreal.load_asset(path)
    if asset is None:
        raise RuntimeError(f"Required Unreal asset is missing: {path}")
    if expected_type is not None and not isinstance(asset, expected_type):
        raise RuntimeError(f"Asset has wrong type: {path} ({type(asset).__name__})")
    return asset


def import_alice_mesh():
    if not FBX_PATH.is_file():
        raise RuntimeError(f"Alice FBX is missing: {FBX_PATH}")

    unreal.EditorAssetLibrary.make_directory(ALICE_FOLDER)
    if unreal.EditorAssetLibrary.does_asset_exist(ALICE_MESH_PATH):
        return require_asset(ALICE_MESH_PATH, unreal.SkeletalMesh)

    options = unreal.FbxImportUI()
    options.automated_import_should_detect_type = False
    options.import_as_skeletal = True
    options.import_mesh = True
    options.import_animations = False
    options.import_materials = False
    options.import_textures = False
    options.create_physics_asset = False
    options.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    options.original_import_type = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    mesh_import_data = options.skeletal_mesh_import_data
    for property_name, value in (
        ("import_meshes_in_bone_hierarchy", True),
        ("update_skeleton_reference_pose", False),
        ("use_t0_as_ref_pose", False),
    ):
        if hasattr(mesh_import_data, property_name):
            setattr(mesh_import_data, property_name, value)

    task = unreal.AssetImportTask()
    task.filename = str(FBX_PATH)
    task.destination_path = ALICE_FOLDER
    task.destination_name = "SK_Alice_Nude_v08_FullRig_Canon175"
    task.automated = True
    task.replace_existing = False
    task.save = True
    task.options = options

    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
    mesh = unreal.load_asset(ALICE_MESH_PATH)
    if mesh is None:
        raise RuntimeError(
            "Full-rig Alice import produced no skeletal mesh at the expected path; "
            f"imported={list(task.imported_object_paths)}"
        )
    return mesh


def recreate_ik_rig(asset_path, mesh, root_bone, chains):
    if unreal.EditorAssetLibrary.does_asset_exist(asset_path):
        return require_asset(asset_path, unreal.IKRigDefinition)

    package_path, asset_name = asset_path.rsplit("/", 1)
    rig = unreal.IKRigDefinitionFactory.create_new_ik_rig_asset(
        package_path, asset_name
    )
    if rig is None:
        raise RuntimeError(f"Could not create IK Rig: {asset_path}")

    controller = unreal.IKRigController.get_controller(rig)
    if not controller.set_skeletal_mesh(mesh):
        raise RuntimeError(f"Could not assign mesh to IK Rig: {asset_path}")
    if not controller.set_retarget_root(unreal.Name(root_bone)):
        raise RuntimeError(
            f"Could not set retarget root {root_bone} on {asset_path}"
        )

    for chain_name, start_bone, end_bone in chains:
        created_name = controller.add_retarget_chain(
            unreal.Name(chain_name),
            unreal.Name(start_bone),
            unreal.Name(end_bone),
            unreal.Name("None"),
        )
        # FName preserves the first-seen display casing globally. The Quinn
        # bone `head` therefore makes a requested chain `Head` print as `head`.
        if str(created_name).lower() != chain_name.lower():
            raise RuntimeError(
                f"Retarget chain {chain_name} was not created cleanly on {asset_path}: "
                f"created={created_name}"
            )

    unreal.EditorAssetLibrary.save_asset(asset_path, only_if_is_dirty=False)
    return rig


def recreate_retargeter(source_ik, target_ik, source_mesh, target_mesh):
    if unreal.EditorAssetLibrary.does_asset_exist(RETARGETER_PATH):
        return require_asset(RETARGETER_PATH, unreal.IKRetargeter)

    factory = unreal.IKRetargetFactory()
    retargeter = unreal.AssetToolsHelpers.get_asset_tools().create_asset(
        RETARGETER_PATH.rsplit("/", 1)[1],
        RETARGETER_PATH.rsplit("/", 1)[0],
        unreal.IKRetargeter,
        factory,
    )
    if retargeter is None:
        raise RuntimeError(f"Could not create IK Retargeter: {RETARGETER_PATH}")

    controller = unreal.IKRetargeterController.get_controller(retargeter)
    source = unreal.RetargetSourceOrTarget.SOURCE
    target = unreal.RetargetSourceOrTarget.TARGET
    controller.set_ik_rig(source, source_ik)
    controller.set_ik_rig(target, target_ik)
    controller.set_preview_mesh(source, source_mesh)
    controller.set_preview_mesh(target, target_mesh)
    controller.add_default_ops()
    controller.assign_ik_rig_to_all_ops(source, source_ik)
    controller.assign_ik_rig_to_all_ops(target, target_ik)
    controller.auto_map_chains(unreal.AutoMapChainType.EXACT, True)
    controller.auto_align_all_bones(
        target, unreal.RetargetAutoAlignMethod.CHAIN_TO_CHAIN
    )
    controller.snap_bone_to_ground(unreal.Name("CC_Base_L_Foot"), target)

    unreal.EditorAssetLibrary.save_asset(RETARGETER_PATH, only_if_is_dirty=False)
    return retargeter


def retarget_proof_set(source_mesh, target_mesh, retargeter):
    unreal.EditorAssetLibrary.make_directory(OUTPUT_FOLDER)
    source_assets = []
    for source_path in SOURCE_ANIM_PATHS:
        output_name = f"Alice_{source_path.rsplit('/', 1)[1]}"
        existing = f"{OUTPUT_FOLDER}/{output_name}"
        if unreal.EditorAssetLibrary.does_asset_exist(existing):
            unreal.EditorAssetLibrary.delete_asset(existing)

        source_data = unreal.EditorAssetLibrary.find_asset_data(source_path)
        if not source_data.is_valid():
            raise RuntimeError(f"Source animation is missing: {source_path}")
        source_assets.append(source_data)

    inputs = unreal.IKRetargetBatchOperationInputs()
    inputs.assets_to_retarget = source_assets
    inputs.source_mesh = source_mesh
    inputs.target_mesh = target_mesh
    inputs.ik_retarget_asset = retargeter
    inputs.prefix = "Alice_"
    inputs.target_path = OUTPUT_FOLDER
    inputs.use_source_path = False
    inputs.include_referenced_assets = False
    inputs.overwrite_existing_files = True
    inputs.retain_additive_flags = True

    generated = unreal.IKRetargetBatchOperation.run_batch_retarget(inputs)
    if not generated:
        raise RuntimeError("Unreal returned no retargeted animation assets")
    generated_paths = [str(asset.package_name) for asset in generated]
    for asset_path in generated_paths:
        unreal.EditorAssetLibrary.save_asset(asset_path, only_if_is_dirty=False)
    return generated_paths


def main():
    source_mesh = require_asset(QUINN_MESH_PATH, unreal.SkeletalMesh)
    target_mesh = import_alice_mesh()

    source_chains = [
        ("Spine", "spine_01", "spine_05"),
        ("Neck", "neck_01", "neck_02"),
        ("Head", "head", "head"),
        ("ClavicleL", "clavicle_l", "clavicle_l"),
        ("ArmL", "upperarm_l", "hand_l"),
        ("ClavicleR", "clavicle_r", "clavicle_r"),
        ("ArmR", "upperarm_r", "hand_r"),
        ("LegL", "thigh_l", "ball_l"),
        ("LegR", "thigh_r", "ball_r"),
    ]
    target_chains = [
        ("Spine", "CC_Base_Waist", "CC_Base_Spine02"),
        ("Neck", "CC_Base_NeckTwist01", "CC_Base_NeckTwist02"),
        ("Head", "CC_Base_Head", "CC_Base_Head"),
        ("ClavicleL", "CC_Base_L_Clavicle", "CC_Base_L_Clavicle"),
        ("ArmL", "CC_Base_L_Upperarm", "CC_Base_L_Hand"),
        ("ClavicleR", "CC_Base_R_Clavicle", "CC_Base_R_Clavicle"),
        ("ArmR", "CC_Base_R_Upperarm", "CC_Base_R_Hand"),
        ("LegL", "CC_Base_L_Thigh", "CC_Base_L_ToeBase"),
        ("LegR", "CC_Base_R_Thigh", "CC_Base_R_ToeBase"),
    ]

    source_ik = recreate_ik_rig(
        SOURCE_IK_PATH, source_mesh, "pelvis", source_chains
    )
    target_ik = recreate_ik_rig(
        TARGET_IK_PATH, target_mesh, "CC_Base_Hip", target_chains
    )
    retargeter = recreate_retargeter(
        source_ik, target_ik, source_mesh, target_mesh
    )
    generated_paths = retarget_proof_set(source_mesh, target_mesh, retargeter)

    report = {
        "status": "PASS",
        "source_mesh": QUINN_MESH_PATH,
        "target_mesh": ALICE_MESH_PATH,
        "source_animations": SOURCE_ANIM_PATHS,
        "source_ik": SOURCE_IK_PATH,
        "target_ik": TARGET_IK_PATH,
        "retargeter": RETARGETER_PATH,
        "generated_assets": generated_paths,
        "selection_note": (
            "Epic MF clips selected for the first restrained feminine locomotion "
            "and pistol proof set. Manual diagnostic keyframes are not reused."
        ),
    }
    PROOF_ROOT.mkdir(parents=True, exist_ok=True)
    report_path = PROOF_ROOT / "retarget_report.json"
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    unreal.log(f"Alice feminine locomotion proof generated: {report_path}")


main()
