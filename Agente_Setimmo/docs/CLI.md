# CLI

## Version

Current agent version: `1.2.0-operational-ux`

## Core read-only commands

### status

```sh
ragnaforge status --json
```

Use for a fast state snapshot: active profile, config fingerprint, safety flags, and cache hints.

### doctor

```sh
ragnaforge doctor --json
```

Use for configuration, path, and safety checks. If `doctor` fails, treat the agent as degraded until the error is fixed.

### baseline

```sh
ragnaforge baseline --json
```

`baseline` composes:

- `status`
- `doctor`
- `scan --project`
- `index --entities`
- `validate`

Use `baseline` when an AI operator needs a full operational checkpoint without manually chaining five commands.

Important:

- `baseline` is read-only.
- `baseline` never applies changes.
- `baseline` never performs real rollback.
- If `doctor` fails, `baseline` fails.
- If `validate` reports only `external-data` issues, `safeForReadOnlyWork` can still be `true`.

### health

```sh
ragnaforge health --json
```

Use `health` for API, UI, Codex, Claude, and Antigravity integrations that need a compact summary instead of raw command fan-out.

`health` returns:

- agent version
- active profile
- config fingerprint
- project cache trust
- entity counts and trust
- validation summary
- core safety flags
- recommended next action

## Project scan

```sh
ragnaforge scan --project --json
```

Read-only scan of the configured RagnaForge main project. Writes only internal cache under `cache/agent`.

## Knowledge

```sh
ragnaforge knowledge sources --json
ragnaforge knowledge build --json
ragnaforge knowledge validate --json
ragnaforge knowledge search --query "item_db" --json
ragnaforge knowledge explain --topic "map dependencies" --json
ragnaforge knowledge entry --id "item-database" --json
ragnaforge knowledge schema --entity item --json
```

Knowledge cache semantics are intentionally split:

- `knowledge build` is the only knowledge command that persists `knowledge/index/knowledge.index.json`.
- `knowledge search`, `knowledge explain`, `knowledge entry`, `knowledge schema`, and `knowledge validate` are read-only.
- If the persisted index is missing or unreadable, read-only commands use a transient in-memory index and return a safe warning instead of writing cache.
- MCP knowledge tools follow the same read-only rule and never persist the knowledge index.

Input limits:

- `query` and `topic`: maximum 512 characters.
- `id`: maximum 128 characters.
- `entity`: strict enum: `item`, `equipment`, `mob`, `npc`, `map`, `asset`.
- Null, control-character, quote, and path-like input is rejected where it is not meaningful.

## Configuration

### config get

```sh
ragnaforge config get --json
```

### config validate

```sh
ragnaforge config validate --json
```

### config set

```sh
ragnaforge config set ragnaforgeMainProjectPath "C:\Users\Allis\Desktop\Ragna_Forge" --json
ragnaforge config set rathenaPath "E:\Ragnarok\Testes\rAthena_teste" --json
ragnaforge config set patchPath "E:\Ragnarok\Testes\Patch_teste" --json
ragnaforge config set grfRepositoryPath "E:\Ragnarok\Conteudo Ragnarok\GRF'S" --json
ragnaforge config set grfEditorPath "C:\Program Files (x86)\GRF Editor" --json
```

Allowed keys:

- `ragnaforgeMainProjectPath`
- `rathenaPath`
- `patchPath`
- `grfRepositoryPath`
- `grfEditorPath`

`config set` now runs real preflight validation before saving:

- path exists
- path is readable
- PathGuard allows read access
- path type has minimum expected structure
- `grfRepositoryPath` stays read-only and never becomes writable

Success returns:

- `oldFingerprint`
- `newFingerprint`
- preflight checks
- `cacheInvalidated`
- `nextRequiredAction = run_baseline`

Failure returns:

- `ok = false`
- clear preflight error
- `nextRequiredAction = choose_valid_path`

## Profiles

```sh
ragnaforge profile list --json
ragnaforge profile use teste --json
ragnaforge profile validate --json
```

## Entity indexing

```sh
ragnaforge index --entities --json
ragnaforge index --items --json
ragnaforge index --npcs --json
ragnaforge index --monsters --json
ragnaforge index --maps --json
```

## Find

```sh
ragnaforge find item --id 501 --json
ragnaforge find item --name "Red Potion" --json
ragnaforge find npc --name "Kafra" --json
ragnaforge find monster --id 1002 --json
ragnaforge find monster --name "Poring" --json
ragnaforge find map --name "prontera" --json
```

## Validate

```sh
ragnaforge validate --json
ragnaforge validate --items --json
ragnaforge validate --npcs --json
ragnaforge validate --monsters --json
ragnaforge validate --maps --json
ragnaforge validate --client --json
ragnaforge validate --server --json
```

Validation now classifies issues by operational scope and impact.

Per issue:

- `severity`
- `scope`
- `blockingFor`
- `notBlockingFor`
- `safeForCurrentTask`
- `code`
- `message`
- `recommendation`

Summary fields:

- `safeForReadOnlyWork`
- `safeForDryRun`
- `safeForApply`
- `issueSummaryByScope`
- `issueSummaryByBlockingTarget`

Interpretation:

- `external-data` issues may block `apply` without blocking read-only audits.
- `config` or `security` critical issues block everything.
- `cache` issues mean counts or validations are not trustworthy until re-indexed.

## Dry-run

```sh
ragnaforge dry-run item --input pedido.json --json
ragnaforge dry-run npc --input pedido.json --json
ragnaforge dry-run monster --input pedido.json --json
ragnaforge dry-run map --input pedido.json --json
```

## Diff

```sh
ragnaforge diff --last --json
ragnaforge diff --operation <operation_id> --json
```

## Report

```sh
ragnaforge report --last --format json
ragnaforge report --last --format md
ragnaforge report --operation <operation_id> --format json
ragnaforge report --operation <operation_id> --format md
```

## Rollback

Informational only:

```sh
ragnaforge rollback --list --json
ragnaforge rollback --id <rollback_id> --dry-run --json
```

## Blocked commands

```sh
ragnaforge apply --json
ragnaforge rollback --confirm --json
```

Expected result:

- blocked by safety policy
- no real apply
- no real rollback

## Cache stale guidance

When cache trust is lost, the CLI returns fields like:

- `cacheTrusted = false`
- `cacheStaleReason`
- `cacheFingerprint`
- `activeFingerprint`
- `cacheProfile`
- `activeProfile`
- `recommendedAction`

Typical fix:

1. `ragnaforge scan --project --json`
2. `ragnaforge index --entities --json`
3. `ragnaforge validate --json`

Or simply:

```sh
ragnaforge baseline --json
```
