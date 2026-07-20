using UnrealBuildTool;
using System.Collections.Generic;

public class MoadHybridTarget : TargetRules
{
    public MoadHybridTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Game;
        DefaultBuildSettings = BuildSettingsVersion.Latest;
        ExtraModuleNames.Add("MoadHybrid");
    }
}
