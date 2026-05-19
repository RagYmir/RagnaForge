# Agente Setimmo Dev Reset
# Cleans build artifacts, cache and log JSON files for a fresh start.
# Does NOT delete configs, docs, source code, or .gitkeep files.

param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Write-Host "=== Agente Setimmo Dev Reset ===" -ForegroundColor Cyan

if (-not $Force) {
    $confirm = Read-Host "This will clean build artifacts, cache and log files. Continue? (y/N)"
    if ($confirm -ne "y") { Write-Host "Cancelled." -ForegroundColor Yellow; exit 0 }
}

# Clean build
Write-Host "Cleaning build artifacts..." -ForegroundColor Yellow
dotnet clean "$root\RagnaForge.Agent.slnx" --verbosity quiet 2>$null

# Clean cache (preserve .gitkeep)
$cacheDir = Join-Path $root "cache\agent"
if (Test-Path $cacheDir) {
    Get-ChildItem "$cacheDir" -Exclude ".gitkeep" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  Cache cleaned: $cacheDir (preserved .gitkeep)" -ForegroundColor Green
} else {
    Write-Host "  No cache to clean" -ForegroundColor Gray
}

# Clean log JSON files (preserve .gitkeep)
$logsDir = Join-Path $root "logs"
if (Test-Path $logsDir) {
    Get-ChildItem "$logsDir" -Include "*.json" -File -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
    Write-Host "  Logs cleaned: $logsDir (preserved .gitkeep)" -ForegroundColor Green
} else {
    Write-Host "  No logs to clean" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== Dev Reset Complete ===" -ForegroundColor Cyan
Write-Host "Run 'dotnet build RagnaForge.Agent.slnx' to rebuild." -ForegroundColor Gray
