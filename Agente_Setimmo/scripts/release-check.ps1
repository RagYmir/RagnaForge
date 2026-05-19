# Agente Setimmo release check.
# Builds, tests, publishes, and verifies installed-safe CLI behavior.

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root "RagnaForge.Agent.slnx"
$install = Join-Path $root "scripts\install.ps1"
$exe = Join-Path $root "dist\agente-setimmo\agente-setimmo.exe"

function Invoke-Step([string]$Name, [scriptblock]$Action) {
    Write-Host ""
    Write-Host "=== $Name ===" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed: $Name"
    }
}

function Assert-JsonContains([string]$Name, [string]$Output, [string]$Pattern) {
    if ($Output -notmatch $Pattern) {
        Write-Host $Output
        throw "$Name did not match expected pattern: $Pattern"
    }
}

Invoke-Step "dotnet clean" { dotnet clean $solution }
Invoke-Step "dotnet build" { dotnet build $solution }
Invoke-Step "dotnet test" { dotnet test $solution }
Invoke-Step "dotnet publish/install" { & $install -AgentRoot $root }

if (-not (Test-Path $exe)) {
    throw "Installed executable not found: $exe"
}

$env:RAGNAFORGE_AGENT_ROOT = $root
$env:Path = "$(Split-Path -Parent $exe);$env:Path"

Invoke-Step "ragnaforge --version" {
    $out = & ragnaforge --version | Out-String
    Write-Host $out
    Assert-JsonContains "version" $out '"version"\s*:'
}

Invoke-Step "ragnaforge status --json" {
    $out = & ragnaforge status --json | Out-String
    Write-Host $out
    Assert-JsonContains "status" $out '"ok"\s*:\s*true'
}

Invoke-Step "ragnaforge doctor --json" {
    $out = & ragnaforge doctor --json | Out-String
    Write-Host $out
    Assert-JsonContains "doctor" $out '"ok"\s*:\s*true'
}

Write-Host ""
Write-Host "=== ragnaforge apply --json ===" -ForegroundColor Cyan
$applyOut = & ragnaforge apply --json | Out-String
Write-Host $applyOut
Assert-JsonContains "apply" $applyOut '"nextRequiredAction"\s*:\s*"blocked_by_safety_policy"'

Write-Host ""
Write-Host "Release check complete." -ForegroundColor Green
