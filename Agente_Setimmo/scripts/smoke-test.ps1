# Agente Setimmo Smoke Test
# Validates that the agent builds, tests pass, and CLI commands return ok=true.

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = $false

Write-Host "=== Agente Setimmo Smoke Test ===" -ForegroundColor Cyan
Write-Host ""

# 1. Build
Write-Host "[1/4] Building solution..." -ForegroundColor Yellow
dotnet build "$root\RagnaForge.Agent.slnx" --verbosity quiet
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED" -ForegroundColor Red; exit 1 }
Write-Host "  Build OK" -ForegroundColor Green

# 2. Test
Write-Host "[2/4] Running tests..." -ForegroundColor Yellow
dotnet test "$root\RagnaForge.Agent.slnx" --verbosity quiet
if ($LASTEXITCODE -ne 0) { Write-Host "TESTS FAILED" -ForegroundColor Red; exit 1 }
Write-Host "  Tests OK" -ForegroundColor Green

# 3. Status — must return "ok": true
Write-Host "[3/4] Running ragnaforge status..." -ForegroundColor Yellow
$statusOutput = dotnet run --project "$root\src\RagnaForge.Agent.Cli" -- status --json 2>&1 | Out-String
if ($Verbose) { Write-Host $statusOutput }
if ($statusOutput -match '"ok":\s*true') {
    Write-Host "  Status OK (ok=true)" -ForegroundColor Green
} else {
    Write-Host "  Status FAILED (ok != true)" -ForegroundColor Red
    $failed = $true
}

# 4. Doctor — must return "ok": true
Write-Host "[4/4] Running ragnaforge doctor..." -ForegroundColor Yellow
$doctorOutput = dotnet run --project "$root\src\RagnaForge.Agent.Cli" -- doctor --json 2>&1 | Out-String
if ($Verbose) { Write-Host $doctorOutput }
if ($doctorOutput -match '"ok":\s*true') {
    Write-Host "  Doctor OK (ok=true)" -ForegroundColor Green
} else {
    Write-Host "  Doctor FAILED (ok != true)" -ForegroundColor Red
    $failed = $true
}

Write-Host ""
if ($failed) {
    Write-Host "=== Smoke Test FAILED ===" -ForegroundColor Red
    exit 1
} else {
    Write-Host "=== Smoke Test Complete ===" -ForegroundColor Cyan
}
