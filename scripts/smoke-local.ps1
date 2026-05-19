$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$agentRoot = Join-Path $root "Agente_Setimmo"

Write-Host "Running Ragna_Forge backend tests..."
dotnet run --project (Join-Path $root "backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj")
if ($LASTEXITCODE -ne 0) { throw "Backend tests failed." }

Write-Host "Running frontend tests..."
Push-Location (Join-Path $root "frontend")
npm.cmd run test
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Frontend tests failed." }
Pop-Location

Write-Host "Running Agente Setimmo smokes..."
Push-Location $agentRoot
dotnet run --project src\RagnaForge.Agent.Cli -- status --json
dotnet run --project src\RagnaForge.Agent.Cli -- doctor --json
dotnet run --project src\RagnaForge.Agent.Cli -- health --json
dotnet run --project src\RagnaForge.Agent.Cli -- knowledge validate --json
dotnet run --project src\RagnaForge.Agent.Cli -- apply --json
if ($LASTEXITCODE -eq 0) { Pop-Location; throw "Apply was expected to be blocked." }
dotnet run --project src\RagnaForge.Agent.Cli -- rollback --list --json
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Rollback list smoke failed." }
Pop-Location

Write-Host "Local smoke completed. Apply remains blocked and rollback is informational."
