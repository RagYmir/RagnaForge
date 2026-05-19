# Agente Setimmo

## Purpose

The agent is the local operational layer for RagnaForge. It exists to reduce repetition and ambiguity for Codex, Antigravity, MCP hosts, and API/UI bridges.

## Core outputs

- `status` for current state
- `doctor` for safety and config checks
- `baseline` for full operational checkpoint
- `health` for compact integration summary

## Paths

Configured through `config/paths.json`.

Important roots:

- `agentRoot`
- `ragnaforgeMainProjectPath`
- `rathenaPath`
- `patchPath`
- `grfRepositoryPath`
- `grfEditorPath`

No path is meant to be hardcoded in automation logic.

## Read-only rules

- GRFs remain read-only
- `.lub` remains blocked
- apply remains blocked
- real rollback remains blocked

## Baseline usage

Run when you need a complete checkpoint:

```sh
ragnaforge baseline --json
```

Use cases:

- start of a new coding session
- pre-release audit
- before delegating work to another model
- before building dashboards from stale caches

## Health usage

Run when you need a compact dashboard-style summary:

```sh
ragnaforge health --json
```

Use cases:

- API/UI summary pages
- MCP summaries
- operator dashboards
- quick trust check for cache-backed counts

## Validation semantics

Three top-level decisions matter:

- `safeForReadOnlyWork`
- `safeForDryRun`
- `safeForApply`

This lets the agent say:

- "read-only work is okay"
- "dry-run is okay"
- "apply would still be unsafe"

without forcing humans or other agents to reinterpret every warning.

## Cache trust

The agent now explains stale caches directly instead of forcing callers to compare raw files by hand.

Example stale reasons:

- `cache_not_found`
- `cache_corrupt`
- `activeProfile_mismatch`
- `configFingerprint_mismatch`
- `scanRoot_mismatch`

## Config preflight

`config set` validates path type before save.

Examples:

- main project path must look like a RagnaForge repo
- rAthena path must look like rAthena
- GRF repository must remain read-only

After a successful config change, run:

```sh
ragnaforge baseline --json
```
