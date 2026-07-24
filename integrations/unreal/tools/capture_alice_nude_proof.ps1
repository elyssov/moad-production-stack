$ErrorActionPreference = 'Stop'

$endpoint = 'http://127.0.0.1:8000/mcp'
$outputPath = 'C:\projects\moad-unreal-spike\integrations\unreal\MoadHybrid\Saved\Proofs\alice_unreal_viewport_nude_v01.png'

function Invoke-McpRequest {
    param(
        [Parameter(Mandatory)] [hashtable] $Body,
        [hashtable] $Headers = @{}
    )

    $requestHeaders = @{
        Accept = 'application/json, text/event-stream'
    }
    foreach ($key in $Headers.Keys) {
        $requestHeaders[$key] = $Headers[$key]
    }

    Invoke-WebRequest -Uri $endpoint -Method Post -Headers $requestHeaders `
        -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Depth 20 -Compress)
}

function Get-McpJson {
    param([Parameter(Mandatory)] [string] $Content)

    $dataLine = ($Content -split "`r?`n" | Where-Object { $_ -like 'data: *' } | Select-Object -Last 1)
    if ($dataLine) {
        return ($dataLine.Substring(6) | ConvertFrom-Json)
    }
    return ($Content | ConvertFrom-Json)
}

function Invoke-McpTool {
    param(
        [Parameter(Mandatory)] [string] $SessionId,
        [Parameter(Mandatory)] [string] $Toolset,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [hashtable] $Arguments,
        [Parameter(Mandatory)] [int] $Id
    )

    $response = Invoke-McpRequest -Headers @{ 'Mcp-Session-Id' = $SessionId } -Body @{
        jsonrpc = '2.0'
        id = $Id
        method = 'tools/call'
        params = @{
            name = 'call_tool'
            arguments = @{
                toolset_name = $Toolset
                tool_name = $Name
                arguments = $Arguments
            }
        }
    }
    Get-McpJson -Content $response.Content
}

$initialize = Invoke-McpRequest -Body @{
    jsonrpc = '2.0'
    id = 1
    method = 'initialize'
    params = @{
        protocolVersion = '2025-03-26'
        capabilities = @{}
        clientInfo = @{ name = 'codex-alice-proof'; version = '1.0' }
    }
}

$sessionId = $initialize.Headers['Mcp-Session-Id']
if (-not $sessionId) {
    throw 'MCP server did not return a session id.'
}

Invoke-McpRequest -Headers @{ 'Mcp-Session-Id' = $sessionId } -Body @{
    jsonrpc = '2.0'
    method = 'notifications/initialized'
} | Out-Null

$cameraResponse = Invoke-McpTool -SessionId $sessionId -Toolset 'EditorToolset.EditorAppToolset' `
    -Name 'GetCameraTransform' -Arguments @{} -Id 2
$cameraEnvelope = $cameraResponse.result.content[0].text | ConvertFrom-Json
$cameraTransform = $cameraEnvelope.returnValue

$captureResponse = Invoke-McpTool -SessionId $sessionId -Toolset 'EditorToolset.EditorAppToolset' `
    -Name 'CaptureViewport' -Id 3 -Arguments @{
        captureTransform = $cameraTransform
        annotations = @{
            gridSpacing = 0
            gridExtent = 0
            gridHeight = 0
            maxLabelDistance = 0
            classFilter = @{ refPath = '/Script/Engine.Actor' }
            maxLabels = 0
        }
        bShowUI = $false
    }

$captureEnvelope = $captureResponse.result.content[0].text | ConvertFrom-Json
if ($captureEnvelope.isError) {
    throw ($captureEnvelope | ConvertTo-Json -Depth 10)
}

$imageData = $captureEnvelope.returnValue.image.data
if (-not $imageData) {
    throw ('Capture returned no image data: ' + ($captureEnvelope | ConvertTo-Json -Depth 10))
}

[IO.File]::WriteAllBytes($outputPath, [Convert]::FromBase64String($imageData))
$file = Get-Item -LiteralPath $outputPath
[pscustomobject]@{
    Path = $file.FullName
    Bytes = $file.Length
    Camera = $cameraTransform
} | ConvertTo-Json -Depth 8
