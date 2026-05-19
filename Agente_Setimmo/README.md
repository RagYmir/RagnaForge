# Agente Setimmo

Safe local operator for RagnaForge, rAthena, Patch/client, and GRF-aware read-only workflows.

Current version: `1.2.0-operational-ux`

## What it does

The agent reduces repeated AI work by handling these steps locally:

- status
- doctor
- baseline
- health
- project scan
- entity index
- find
- validate
- dry-run
- diff
- report
- rollback preview
- MCP-safe summaries

## What it does not do

- no real apply
- no real destructive rollback
- no GRF writes
- no `.lub` editing
- no HTTP write surface

## Supported operators

- Codex
- Antigravity
- Claude or any CLI+JSON orchestrator
- MCP-compatible hosts

## Development usage

```sh
dotnet build RagnaForge.Agent.slnx
dotnet test RagnaForge.Agent.slnx

dotnet run --project src/RagnaForge.Agent.Cli -- status --json
dotnet run --project src/RagnaForge.Agent.Cli -- doctor --json
dotnet run --project src/RagnaForge.Agent.Cli -- baseline --json
dotnet run --project src/RagnaForge.Agent.Cli -- health --json
```

## Installed usage

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install.ps1
ragnaforge --version
ragnaforge status --json
ragnaforge doctor --json
ragnaforge baseline --json
ragnaforge health --json
```

If the executable is launched outside the repo:

```powershell
$env:RAGNAFORGE_AGENT_ROOT = "C:\Users\Allis\Desktop\Ragna_Forge\Agente_Setimmo"
```

## Why baseline and health exist

### baseline

`ragnaforge baseline --json` is the one-shot operational checkpoint.

It bundles:

- status
- doctor
- scan
- index
- validate

Use it when a model or human operator wants a fresh decision surface before working.

### health

`ragnaforge health --json` is the compact integration summary for API/UI/MCP usage.

Use it when you need:

- trusted or untrusted cache counts
- validation summary
- safety flags
- recommended next action

without manually composing multiple commands.

## Config safety

All paths live in `config/paths.json`.

`ragnaforge config set ... --json` now performs real preflight validation before saving:

- path exists
- path is readable
- PathGuard allows read
- expected structure exists for the configured path type
- GRF repository remains read-only

On success it returns:

- `oldFingerprint`
- `newFingerprint`
- preflight checks
- `cacheInvalidated`
- `nextRequiredAction = run_baseline`

## Validation semantics

Validation now separates:

- `safeForReadOnlyWork`
- `safeForDryRun`
- `safeForApply`

and classifies issues by:

- `scope`
- `blockingFor`
- `notBlockingFor`

This means external dataset issues can still allow read-only audits while correctly blocking future apply decisions.

## Cache trust

Cache trust is no longer implied.

Stale caches report:

- fingerprint mismatch
- profile mismatch
- missing cache
- corrupt cache
- recommended recovery action

## MCP

Safe MCP tools now include:

- `ragnaforge_baseline`
- `ragnaforge_health`
- read-only resources
- static safe prompts

Blocked MCP tools remain:

- `ragnaforge_apply`
- `ragnaforge_rollback_confirm`

## Current test status

`183/183` tests passing in this version.

## Docs

- [CLI](docs/CLI.md)
- [MCP](docs/MCP.md)
- [AI Agent Contract](docs/AI_AGENT_CONTRACT.md)
- [Agente Setimmo](docs/RAGNAFORGE_AGENT.md)
- [Roadmap](docs/ROADMAP_AGENT.md)
- [Operational UX report](docs/reports/2026-05-18_1.2.0-operational-ux_v1.md)
