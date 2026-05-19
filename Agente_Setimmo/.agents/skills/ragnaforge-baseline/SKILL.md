---
name: ragnaforge-baseline
description: Use for a full safe operational checkpoint via ragnaforge baseline --json.
---

# RagnaForge Baseline

Use when you need one read-only command that refreshes the operational picture.

## Command

```sh
ragnaforge baseline --json
# or
dotnet run --project src/RagnaForge.Agent.Cli -- baseline --json
```

## What it includes

- status
- doctor
- scan
- index
- validate

## How to read it

- `safeForReadOnlyWork` tells you whether read-only audit work can proceed
- `safeForDryRun` tells you whether dry-run can proceed
- `safeForApply` tells you whether future apply discussions should stop

## Safety

- read-only only
- no real apply
- no real rollback
- no GRF writes
- no `.lub` editing
