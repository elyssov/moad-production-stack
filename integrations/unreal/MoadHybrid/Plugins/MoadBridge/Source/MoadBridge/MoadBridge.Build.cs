using UnrealBuildTool;

public class MoadBridge : ModuleRules
{
    public MoadBridge(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        PublicDependencyModuleNames.AddRange(new[] { "Core", "CoreUObject", "Engine" });
        PrivateDependencyModuleNames.AddRange(new[] { "Json", "JsonUtilities", "UnrealEd" });
    }
}
