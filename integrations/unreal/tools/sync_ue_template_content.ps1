param(
    [string]$EngineRoot = "C:\Program Files\Epic Games\UE_5.8"
)

$ErrorActionPreference = "Stop"

$source = Join-Path $EngineRoot "Templates\TemplateResources\High\Characters\Content\Mannequins"
$destination = Join-Path $PSScriptRoot "..\MoadHybrid\Content\Characters\Mannequins"

if (-not (Test-Path -LiteralPath $source)) {
    throw "UE 5.8 mannequin content was not found at $source"
}

New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -Path (Join-Path $source "*") -Destination $destination -Recurse -Force

$files = Get-ChildItem -LiteralPath $destination -Recurse -File
[pscustomobject]@{
    Files = $files.Count
    SizeMB = [math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1)
    Quinn = Test-Path (Join-Path $destination "Meshes\SKM_Quinn_Simple.uasset")
    ControlRig = Test-Path (Join-Path $destination "Rigs\CR_Mannequin_Body.uasset")
    PistolReload = Test-Path (Join-Path $destination "Anims\Pistol\MM_Pistol_Reload.uasset")
}
