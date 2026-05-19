# AI Agent Contract

The Agente Setimmo is the deterministic local source of truth for safe operational state.

Current version: `1.2.0-operational-ux`

## Safe commands for AI operators

```sh
ragnaforge status --json
ragnaforge doctor --json
ragnaforge baseline --json
ragnaforge health --json
ragnaforge scan --project --json
ragnaforge index --entities --json
ragnaforge validate --json
ragnaforge diff --operation <id> --json
ragnaforge report --operation <id> --format json
```

## Blocked commands

- `ragnaforge apply`
- real `ragnaforge rollback`
- any command that writes to GRF
- any command that edits `.lub`

## Baseline contract

Use `baseline` when the operator needs one safe checkpoint for:

- status
- doctor
- scan
- index
- validate

Interpretation:

- if `doctor` fails, stop
- if `safeForReadOnlyWork` is `true`, read-only audit work can proceed
- if `safeForDryRun` is `true`, dry-run can proceed
- if `safeForApply` is `false`, no future write flow should be authorized

## Health contract

Use `health` when the operator needs a compact summary for:

- API
- UI
- MCP
- multi-agent orchestration

Health is designed to avoid manual cache interpretation.

## Validation contract

Every issue may include:

- `severity`
- `scope`
- `blockingFor`
- `notBlockingFor`
- `safeForCurrentTask`
- `code`
- `message`
- `recommendation`

Standard scopes:

- `external-data`
- `project-code`
- `config`
- `cache`
- `security`
- `agent-runtime`

Meaning:

- `external-data` can block apply while still allowing read-only work
- `cache` means results are not trustworthy until refreshed
- `security` critical means stop everything

## Cache trust contract

Count summaries are only trustworthy when cache trust is explicit.

When stale, the agent surfaces:

- `cacheTrusted = false`
- `cacheStaleReason`
- `cacheFingerprint`
- `activeFingerprint`
- `cacheProfile`
- `activeProfile`
- `recommendedAction`

## Config contract

`ragnaforge config set <key> <value> --json` performs preflight before saving.

Checks include:

- existence
- readability
- PathGuard approval
- expected structure by path type
- GRF read-only enforcement

On success:

- fingerprint changes are reported
- cache invalidation is signaled
- `nextRequiredAction = run_baseline`

On failure:

- config is not saved
- a clear preflight error is returned
- `nextRequiredAction = choose_valid_path`
