import unreal


LEVEL_PATH = "/Game/Maps/SemiAbandonedTomb"
SCENE_CONTRACT = (
    r"C:\projects\moad-unreal-spike\integrations\unreal\generated"
    r"\semi_abandoned_tomb\moad.unreal-scene.json"
)


level_editor = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
if unreal.EditorAssetLibrary.does_asset_exist(LEVEL_PATH):
    level_editor.load_level(LEVEL_PATH)
else:
    level_editor.new_level(LEVEL_PATH)

importer = unreal.get_editor_subsystem(unreal.MoadSceneImportSubsystem)
result = importer.import_scene_contract(SCENE_CONTRACT, True)

if not result.success:
    raise RuntimeError(f"MoAD scene import failed: {result.message}")

actor_editor = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
for actor in actor_editor.get_all_level_actors():
    if actor.get_actor_label() == "MOAD Alice - Quinn Rig Proxy":
        actor_editor.destroy_actor(actor)

quinn_mesh = unreal.load_asset(
    "/Game/Characters/Mannequins/Meshes/SKM_Quinn_Simple"
)
pistol_idle = unreal.load_asset(
    "/Game/Characters/Mannequins/Anims/Pistol/MF_Pistol_Idle_ADS"
)
if quinn_mesh is None or pistol_idle is None:
    raise RuntimeError("Quinn rig proxy or pistol idle animation is unavailable")

alice_proxy = actor_editor.spawn_actor_from_object(
    quinn_mesh,
    unreal.Vector(600.0, 0.0, 106.25),
    unreal.Rotator(0.0, 0.0, 0.0),
)
alice_proxy.set_actor_label("MOAD Alice - Quinn Rig Proxy")
mesh_component = alice_proxy.get_editor_property("skeletal_mesh_component")
mesh_component.set_editor_property(
    "animation_mode", unreal.AnimationMode.ANIMATION_SINGLE_NODE
)
mesh_component.set_editor_property(
    "animation_data",
    unreal.SingleAnimationPlayData(
        anim_to_play=pistol_idle,
        saved_looping=True,
        saved_playing=True,
        saved_position=0.0,
        saved_play_rate=1.0,
    ),
)

if not level_editor.save_current_level():
    raise RuntimeError(f"MoAD scene imported but {LEVEL_PATH} could not be saved")

unreal.log(
    "MOAD_IMPORT_OK "
    f"supports={result.support_count} transitions={result.transition_count} "
    f"rig_proxy={alice_proxy.get_actor_label()} level={LEVEL_PATH}"
)
