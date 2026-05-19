# Reports Guide

Agente Setimmo generates detailed reports for every operation.

## Locations
- **Operation Manifests**: `logs/operations/` (JSON)
- **Diffs**: `logs/diffs/` (Unified Diff)
- **Rollback Plans**: `logs/rollbacks/` (JSON)
- **Markdown Reports**: `logs/reports/` (Readable MD)

## Report Command
Generate a human-readable report for the last operation:
`ragnaforge report --last --format md`

## Safety Information
Reports explicitly state:
- `safeForAutomation`: If true, the plan followed all safety rules.
- `applied`: False in this MVP (all operations are read-only).
- `rollbackPlanPath`: Path to the informational rollback plan. Real rollback is blocked in this version.

Apply e rollback reais estão bloqueados nesta versão. Qualquer suporte futuro depende de nova decisão formal de segurança e não será exposto pela camada MCP v1.
