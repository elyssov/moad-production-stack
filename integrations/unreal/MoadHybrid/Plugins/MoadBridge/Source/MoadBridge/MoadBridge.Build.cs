using UnrealBuildTool;

public class MoadBridge : ModuleRules
{
    public MoadBridge(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        PublicDependencyModuleNames.AddRange(new[] { "Core", "CoreUObject", "Engine", "EditorSubsystem", "UnrealEd" });
        PrivateDependencyModuleNames.AddRange(new[] { "Json", "JsonUtilities" });
    }
}
