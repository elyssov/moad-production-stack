param(
    [Parameter(Mandatory)] [string] $Toolset,
    [Parameter(Mandatory)] [string] $Tool,
    [string] $ArgumentsJson = '{}'
)

$ErrorActionPreference = 'Stop'
$endpoint = 'http://127.0.0.1:8000/mcp'

function Convert-McpResponse {
    param([Parameter(Mandatory)] [string] $Content)
    $dataLine = $Content -split "`r?`n" | Where-Object { $_ -like 'data: *' } | Select-Object -Last 1
    if ($dataLine) {
        return ($dataLine.Substring(6) | ConvertFrom-Json)
    }
    return ($Content | ConvertFrom-Json)
}

function Invoke-Request {
    param([hashtable] $Body, [hashtable] $Headers)
    Invoke-WebRequest -Uri $endpoint -Method Post -Headers $Headers -ContentType 'application/json' `
        -Body ($Body | ConvertTo-Json -Depth 30 -Compress)
}

$headers = @{ Accept = 'application/json, text/event-stream' }
$init = Invoke-Request -Headers $headers -Body @{
    jsonrpc = '2.0'
    id = 1
    method = 'initialize'
    params = @{
        protocolVersion = '2025-03-26'
        capabilities = @{}
        clientInfo = @{ name = 'codex-unreal-mcp'; version = '1.0' }
    }
}
$headers['Mcp-Session-Id'] = $init.Headers['Mcp-Session-Id']

Invoke-Request -Headers $headers -Body @{
    jsonrpc = '2.0'
    method = 'notifications/initialized'
} | Out-Null

$arguments = $ArgumentsJson | ConvertFrom-Json
$response = Invoke-Request -Headers $headers -Body @{
    jsonrpc = '2.0'
    id = 2
    method = 'tools/call'
    params = @{
        name = 'call_tool'
        arguments = @{
            toolset_name = $Toolset
            tool_name = $Tool
            arguments = $arguments
        }
    }
}

$json = Convert-McpResponse -Content $response.Content
$json.result.content[0].text
