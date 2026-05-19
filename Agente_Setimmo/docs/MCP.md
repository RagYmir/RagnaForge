# MCP

## Version

Current MCP-safe agent version: `1.2.0-operational-ux`

The MCP server remains a thin read-only layer over `RagnaForge.Agent.Core`.
It uses a small stdio JSON-RPC implementation for the current preview rather than a heavier SDK dependency, because the exposed surface is intentionally limited to tools, resources, prompts, and response limiting.

It does not:

- expose real apply
- expose real rollback
- create HTTP write endpoints
- modify GRFs
- edit `.lub`

## How to run

```sh
dotnet run --project src/RagnaForge.Agent.Mcp
```

List tools:

```sh
dotnet run --project src/RagnaForge.Agent.Mcp -- --list-tools
```

List resources:

```sh
dotnet run --project src/RagnaForge.Agent.Mcp -- --list-resources
```

List prompts:

```sh
dotnet run --project src/RagnaForge.Agent.Mcp -- --list-prompts
```

## Safe MCP tools

- `ragnaforge_status`
- `ragnaforge_doctor`
- `ragnaforge_baseline`
- `ragnaforge_health`
- `ragnaforge_scan_project`
- `ragnaforge_config_get`
- `ragnaforge_config_validate`
- `ragnaforge_profile_list`
- `ragnaforge_profile_validate`
- `ragnaforge_index_entities`
- `ragnaforge_find_item`
- `ragnaforge_find_npc`
- `ragnaforge_find_monster`
- `ragnaforge_find_map`
- `ragnaforge_validate`
- `ragnaforge_dry_run_item`
- `ragnaforge_dry_run_npc`
- `ragnaforge_dry_run_monster`
- `ragnaforge_dry_run_map`
- `ragnaforge_diff`
- `ragnaforge_report`
- `ragnaforge_rollback_list`
- `ragnaforge_rollback_dry_run`

## Blocked MCP tools

- `ragnaforge_apply`
- `ragnaforge_rollback_confirm`

These remain blocked by safety policy.

## Safe MCP resources

- `ragnaforge://status`
- `ragnaforge://safety`
- `ragnaforge://docs/readme`
- `ragnaforge://docs/safety`
- `ragnaforge://docs/mcp`
- `ragnaforge://reports`
- `ragnaforge://reports/{id}`
- `ragnaforge://inputs/dry-run`

## Safe MCP prompts

- `ragnaforge_validate_project`
- `ragnaforge_prepare_dry_run`
- `ragnaforge_review_validation_errors`
- `ragnaforge_generate_report_summary`
- `ragnaforge_mcp_safety_briefing`

## When to use each MCP summary tool

### `ragnaforge_baseline`

Use when an operator wants the full operational checkpoint in one safe call:

- status
- doctor
- scan
- index
- validate

Best for:

- session start
- release checks
- handoff to another model
- confirming whether read-only work and dry-run are safe

### `ragnaforge_health`

Use when an API, UI, or orchestration layer needs a compact summary with trusted or untrusted cache counts.

Best for:

- dashboards
- status cards
- API bridging
- MCP integrations that should avoid parsing multiple raw command outputs

## Cache trust contract

`ragnaforge_health` and `ragnaforge_baseline` surface cache trust details directly.

When stale:

- `cacheTrusted` or `trustedCounts` becomes `false`
- `cacheStaleReason` explains why
- fingerprints and profiles are included
- `recommendedAction` points to `run_scan_or_index`

## Response size limits

MCP responses still pass through `McpResponseLimiter`.

That means:

- large payloads are truncated safely
- summary fields stay available
- no large raw cache dump is returned by default

## Example MCP config

```json
{
  "mcpServers": {
    "ragnaforge": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Users\\Allis\\Desktop\\Agente Setimmo\\src\\RagnaForge.Agent.Mcp"
      ]
    }
  }
}
```
