$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "backend\src\RagnaForge.Api\RagnaForge.Api.csproj"
$out = Join-Path $root "dist\api"

if (-not (Test-Path -LiteralPath $project)) {
    throw "API project not found: $project"
}

if (Test-Path -LiteralPath $out) {
    Remove-Item -LiteralPath $out -Recurse -Force
}
New-Item -ItemType Directory -Path $out -Force | Out-Null

dotnet publish $project -c Release -o $out
if ($LASTEXITCODE -ne 0) {
    throw "API publish failed."
}

Write-Host "API published to $out"
