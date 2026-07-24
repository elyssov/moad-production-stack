param(
    [Parameter(Mandatory)] [string] $AssetPath,
    [Parameter(Mandatory)] [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$endpoint = 'http://127.0.0.1:8000/mcp'
$headers = @{ Accept = 'application/json, text/event-stream' }

function Send-Mcp([hashtable] $body) {
    Invoke-WebRequest -Uri $endpoint -Method Post -Headers $headers -ContentType 'application/json' `
        -Body ($body | ConvertTo-Json -Depth 20 -Compress)
}

function Parse-Mcp([string] $content) {
    $line = $content -split "`r?`n" | Where-Object { $_ -like 'data: *' } | Select-Object -Last 1
    if ($line) { return ($line.Substring(6) | ConvertFrom-Json) }
    return ($content | ConvertFrom-Json)
}

$init = Send-Mcp @{
    jsonrpc = '2.0'; id = 1; method = 'initialize'
    params = @{
        protocolVersion = '2025-03-26'; capabilities = @{}
        clientInfo = @{ name = 'codex-asset-capture'; version = '1.0' }
    }
}
$headers['Mcp-Session-Id'] = $init.Headers['Mcp-Session-Id']
Send-Mcp @{ jsonrpc = '2.0'; method = 'notifications/initialized' } | Out-Null

$response = Send-Mcp @{
    jsonrpc = '2.0'; id = 2; method = 'tools/call'
    params = @{
        name = 'call_tool'
        arguments = @{
            toolset_name = 'EditorToolset.EditorAppToolset'
            tool_name = 'CaptureAssetImage'
            arguments = @{ assetPath = $AssetPath }
        }
    }
}

$message = Parse-Mcp $response.Content
$payload = $message.result.content[0].text | ConvertFrom-Json
$data = $payload.returnValue.data
if (-not $data) { throw 'Asset capture returned no image data.' }

[IO.File]::WriteAllBytes($OutputPath, [Convert]::FromBase64String($data))
$file = Get-Item -LiteralPath $OutputPath
[pscustomobject]@{ Path = $file.FullName; Bytes = $file.Length } | ConvertTo-Json
