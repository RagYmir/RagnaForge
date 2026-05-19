# Workflow: Dry-Run Entity Change

1. **Prepare Input**: Create a JSON file with entity data (id, aegisName, name, etc.).
2. **Execute**: Run `ragnaforge dry-run <type> --input <file.json>`.
3. **Validate**: Check JSON output for `ok: true`.
4. **Review**: Analyze `affectedFiles`, `diffPath`, and `rollbackPlanPath` in `logs/operations/`.
5. **Report**: Run `ragnaforge report --last --format md` for human review.

**Note**: Real apply is blocked by safety policy.
