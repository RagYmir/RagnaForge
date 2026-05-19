# Agente Setimmo portable installer.
# Publishes the CLI and adds dist/agente-setimmo to the user PATH.

param(
    [string]$AgentRoot
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($AgentRoot)) {
    $AgentRoot = $root
}

$AgentRoot = [System.IO.Path]::GetFullPath($AgentRoot)
$project = Join-Path $root "src\RagnaForge.Agent.Cli\RagnaForge.Agent.Cli.csproj"
$dist = Join-Path $root "dist\agente-setimmo"
$exe = Join-Path $dist "agente-setimmo.exe"
$compatExe = Join-Path $dist "ragnaforge.exe"
$publishedExe = Join-Path $dist "RagnaForge.Agent.Cli.exe"
$marker = Join-Path $dist "ragnaforge.agentroot"

Write-Host "=== Agente Setimmo Install ===" -ForegroundColor Cyan
Write-Host "Agent root: $AgentRoot"
Write-Host "Install dir: $dist"

if (-not (Test-Path (Join-Path $AgentRoot "config\paths.json"))) {
    throw "Missing config\paths.json under agentRoot: $AgentRoot"
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host "Publishing CLI (Release, win-x64, self-contained single-file)..." -ForegroundColor Yellow
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $dist

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

if (-not (Test-Path $publishedExe)) {
    throw "Published executable not found: $publishedExe"
}

Copy-Item -Force -LiteralPath $publishedExe -Destination $exe
Copy-Item -Force -LiteralPath $publishedExe -Destination $compatExe
Set-Content -LiteralPath $marker -Value $AgentRoot -NoNewline

$currentUserPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ([string]::IsNullOrWhiteSpace($currentUserPath)) {
    $pathEntries = @()
} else {
    $pathEntries = $currentUserPath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
}

$alreadyInPath = $pathEntries | Where-Object { $_.TrimEnd('\') -ieq $dist.TrimEnd('\') }
if (-not $alreadyInPath) {
    $newPath = (($pathEntries + $dist) -join ';')
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    $env:Path = "$env:Path;$dist"
    Write-Host "Added to user PATH: $dist" -ForegroundColor Green
} else {
    Write-Host "User PATH already contains: $dist" -ForegroundColor Green
}

Write-Host ""
Write-Host "Install complete." -ForegroundColor Cyan
Write-Host "Executable: $exe"
Write-Host "Compatibility executable: $compatExe"
Write-Host "Agent root marker: $marker"
Write-Host "Config/cache/logs/inputs were preserved."
Write-Host "Open a new terminal to use 'ragnaforge' from PATH, or run: $exe"
