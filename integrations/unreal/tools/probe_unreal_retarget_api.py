import json
import inspect
from pathlib import Path

import unreal


OUTPUT = Path(
    r"C:\projects\moad-unreal-spike\integrations\unreal\MoadHybrid\Saved\Proofs"
) / "unreal_retarget_api_probe.json"


def public_members(value):
    return sorted(name for name in dir(value) if not name.startswith("_"))


names = [
    name
    for name in dir(unreal)
    if any(token in name.lower() for token in ("ikrig", "retarget", "animsequence"))
]

payload = {"unreal_symbols": sorted(names)}
for name in names:
    value = getattr(unreal, name)
    if isinstance(value, type):
        payload[name] = public_members(value)

interesting_calls = {
    "IKRigController": [
        "get_controller",
        "set_skeletal_mesh",
        "set_retarget_root",
        "add_retarget_chain",
    ],
    "IKRetargeterController": [
        "get_controller",
        "set_ik_rig",
        "add_default_ops",
        "auto_map_chains",
    ],
    "IKRetargetBatchOperation": ["run_batch_retarget", "duplicate_and_retarget"],
    "IKRigDefinitionFactory": ["create_new_ik_rig_asset"],
}

payload["call_docs"] = {}
for class_name, method_names in interesting_calls.items():
    cls = getattr(unreal, class_name)
    payload["call_docs"][class_name] = {}
    for method_name in method_names:
        method = getattr(cls, method_name)
        try:
            signature = str(inspect.signature(method))
        except (TypeError, ValueError):
            signature = None
        payload["call_docs"][class_name][method_name] = {
            "signature": signature,
            "doc": inspect.getdoc(method),
        }

for struct_name in ("IKRetargetBatchOperationInputs", "IKRetargetFactory"):
    struct = getattr(unreal, struct_name)
    payload[f"{struct_name}_doc"] = inspect.getdoc(struct)

alice = unreal.load_asset(
    "/Game/MoAD/Characters/AliceV08/SK_Alice_Nude_v08_Canon175_AnimRig"
)
quinn = unreal.load_asset("/Game/Characters/Mannequins/Meshes/SKM_Quinn_Simple")
from editor_toolset.toolsets.skeletal_mesh import SkeletalMeshTools

payload["alice_bones"] = [str(name) for name in SkeletalMeshTools.get_bone_names(alice)]
payload["quinn_bones"] = [str(name) for name in SkeletalMeshTools.get_bone_names(quinn)]

OUTPUT.parent.mkdir(parents=True, exist_ok=True)
OUTPUT.write_text(json.dumps(payload, indent=2), encoding="utf-8")
unreal.log(f"Wrote retarget API probe to {OUTPUT}")
