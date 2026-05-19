# MCP Tools

Agente Setimmo MCP exposes only read-only, dry-run, diff, report, and informational rollback tools.

## Core tools

- `ragnaforge_status`
- `ragnaforge_doctor`
- `ragnaforge_scan_project`
- `ragnaforge_index_entities`
- `ragnaforge_validate`
- `ragnaforge_baseline`
- `ragnaforge_health`
- `ragnaforge_security_policy`
- `ragnaforge_triage`

## Safe planning tools

- `ragnaforge_find_item`
- `ragnaforge_find_npc`
- `ragnaforge_find_monster`
- `ragnaforge_find_map`
- `ragnaforge_knowledge_sources`
- `ragnaforge_knowledge_validate`
- `ragnaforge_knowledge_search`
- `ragnaforge_knowledge_explain`
- `ragnaforge_knowledge_entry`
- `ragnaforge_knowledge_schema`
- `ragnaforge_dry_run_item`
- `ragnaforge_dry_run_npc`
- `ragnaforge_dry_run_monster`
- `ragnaforge_dry_run_map`
- `ragnaforge_diff`
- `ragnaforge_report`
- `ragnaforge_report_list`
- `ragnaforge_report_read`
- `ragnaforge_rollback_list`
- `ragnaforge_rollback_dry_run`

## Blocked tools

- `ragnaforge_apply`
- `ragnaforge_rollback_confirm`

No MCP tool exposes shell, arbitrary command execution, real apply, or real rollback.

Knowledge MCP tools are read-only. They do not run `knowledge build`, do not write
`knowledge/index/knowledge.index.json`, and return a safe warning when they fall
back to a transient in-memory index.
