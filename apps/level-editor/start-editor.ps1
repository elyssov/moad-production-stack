$ErrorActionPreference = 'Stop'
$editorRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $editorRoot

if (-not (Test-Path 'node_modules')) {
    npm install
}

npm run dev -- --host 127.0.0.1 --port 4173
