# health-summary

Use this workflow when you need a compact safe summary for dashboards or API/UI glue.

## Command

```sh
ragnaforge health --json
```

## Read it like this

- `project.cacheTrusted` tells you if project counts are reliable
- `entities.trustedCounts` tells you if entity totals are reliable
- `validation.safeForReadOnlyWork` tells you if read-only work is still okay
- `validation.safeForApply` tells you if apply remains unsafe
- `recommendedAction` tells you the next safe operator step
