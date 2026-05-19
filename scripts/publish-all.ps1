$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

& (Join-Path $root "scripts\publish-api.ps1")
& (Join-Path $root "scripts\publish-agent.ps1")

Write-Host "All publish outputs are ready under dist\."
