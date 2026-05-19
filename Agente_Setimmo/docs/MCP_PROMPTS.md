# MCP Prompts

The MCP server exposes static prompts for safe AI operation.

## Prompts

- `ragnaforge_validate_project`
- `ragnaforge_prepare_dry_run`
- `ragnaforge_review_validation_errors`
- `ragnaforge_generate_report_summary`
- `ragnaforge_mcp_safety_briefing`

## Safety rules

- Prompts do not execute commands.
- Prompts do not request apply.
- Prompts do not request real rollback.
- Prompts route write-like work through scan, validation, dry-run, diff-preview, and report only.
