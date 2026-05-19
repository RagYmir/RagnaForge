# baseline-check

Use this workflow when starting a task that depends on current project state.

## Command

```sh
ragnaforge baseline --json
```

## Decision guide

- If `doctor.ok` is false, stop and fix environment or config.
- If `scan.ok` or `index.ok` is false, do not trust project or entity counts.
- If `safeForReadOnlyWork` is true, read-only work can proceed.
- If `safeForDryRun` is true, dry-run can proceed.
- If `safeForApply` is false, do not discuss apply as if it were safe.
