# Agente Setimmo portable uninstaller.
# Removes dist/agente-setimmo from the user PATH. It does not delete config/cache/logs by default.

param(
    [switch]$CleanDist
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "dist\agente-setimmo"

Write-Host "=== Agente Setimmo Uninstall ===" -ForegroundColor Cyan
Write-Host "Install dir: $dist"

$currentUserPath = [Environment]::GetEnvironmentVariable("Path", "User")
if (-not [string]::IsNullOrWhiteSpace($currentUserPath)) {
    $pathEntries = $currentUserPath -split ';' | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_) -and $_.TrimEnd('\') -ine $dist.TrimEnd('\')
    }
    [Environment]::SetEnvironmentVariable("Path", ($pathEntries -join ';'), "User")
    Write-Host "Removed from user PATH if present: $dist" -ForegroundColor Green
} else {
    Write-Host "User PATH is empty; nothing to remove." -ForegroundColor Yellow
}

if ($CleanDist) {
    if (Test-Path $dist) {
        Remove-Item -LiteralPath $dist -Recurse -Force
        Write-Host "Removed install directory: $dist" -ForegroundColor Green
    }
} else {
    Write-Host "Install directory preserved. Use -CleanDist to remove portable binaries." -ForegroundColor Yellow
}

Write-Host "Config/cache/logs/inputs were not deleted." -ForegroundColor Cyan
