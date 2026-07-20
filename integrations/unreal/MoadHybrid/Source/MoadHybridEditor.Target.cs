using UnrealBuildTool;
using System.Collections.Generic;

public class MoadHybridEditorTarget : TargetRules
{
    public MoadHybridEditorTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Editor;
        DefaultBuildSettings = BuildSettingsVersion.Latest;
        ExtraModuleNames.Add("MoadHybrid");
    }
}
