# Roadmap - Agente Setimmo

## Delivered

### Foundation

- safe Core library
- CLI
- config loader
- fingerprinting
- PathGuard
- tests
- docs

### MVP

- `status`
- `doctor`
- `scan`
- `config`
- `profile`
- `index`
- `find`
- `validate`
- `dry-run`
- `diff`
- `report`
- rollback preview
- apply blocked
- real rollback blocked

### MCP preview

- stdio MCP server
- safe tool allowlist
- blocked apply and rollback confirm
- response limiter

### 1.2.0-operational-ux

- `ragnaforge baseline --json`
- `ragnaforge health --json`
- `config set` preflight validation
- validation issue scope and impact classification
- explicit stale cache details
- MCP tools:
  - `ragnaforge_baseline`
  - `ragnaforge_health`
- MCP resources and prompts for read-only operators

## Current status

- version: `1.2.0-operational-ux`
- tests: `183/183` passing

## Next likely improvements

- richer per-entity stale cache details for specialized indexes
- incremental scan/index refresh
- stronger config diagnostics for partial patch layouts
- packaged release refresh after operational UX validation

## Still blocked by policy

- real apply
- real destructive rollback
- GRF writes
- `.lub` editing
