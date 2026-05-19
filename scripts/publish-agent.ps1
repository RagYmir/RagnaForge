$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$agentRoot = Join-Path $root "Agente_Setimmo"
$project = Join-Path $agentRoot "src\RagnaForge.Agent.Cli\RagnaForge.Agent.Cli.csproj"
$out = Join-Path $root "dist\agent"
$publishedExe = Join-Path $out "RagnaForge.Agent.Cli.exe"
$publicExe = Join-Path $out "agente-setimmo.exe"
$compatExe = Join-Path $out "ragnaforge.exe"
$marker = Join-Path $out "ragnaforge.agentroot"

if (-not (Test-Path -LiteralPath $project)) {
    throw "Agent CLI project not found: $project"
}
if (-not (Test-Path -LiteralPath (Join-Path $agentRoot "config\paths.json"))) {
    Write-Warning "Agente Setimmo local config not found. Copy config\paths.example.json to config\paths.json before running live smokes."
}

if (Test-Path -LiteralPath $out) {
    Remove-Item -LiteralPath $out -Recurse -Force
}
New-Item -ItemType Directory -Path $out -Force | Out-Null

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $out

if ($LASTEXITCODE -ne 0) {
    throw "Agente Setimmo publish failed."
}
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Published executable not found: $publishedExe"
}

Copy-Item -LiteralPath $publishedExe -Destination $publicExe -Force
Copy-Item -LiteralPath $publishedExe -Destination $compatExe -Force
Set-Content -LiteralPath $marker -Value $agentRoot -NoNewline

Write-Host "Agente Setimmo published to $out"
Write-Host "Primary executable: $publicExe"
Write-Host "Compatibility executable: $compatExe"
