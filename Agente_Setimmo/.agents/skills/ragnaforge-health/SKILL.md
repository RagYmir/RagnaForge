---
name: ragnaforge-health
description: Use for compact operational summaries via ragnaforge health --json.
---

# RagnaForge Health

Use when an API, UI, or another agent needs a compact answer instead of multiple raw command calls.

## Command

```sh
ragnaforge health --json
# or
dotnet run --project src/RagnaForge.Agent.Cli -- health --json
```

## Focus

- cache trust
- entity counts
- validation summary
- safety flags
- recommended next action

## Safety

- read-only only
- no arbitrary execution
- no apply
- no real rollback
