# MCP Resources

The MCP server exposes small read-only resources for AI operators and UI/API diagnostics.

## Resources

- `ragnaforge://status`
- `ragnaforge://safety`
- `ragnaforge://docs/readme`
- `ragnaforge://docs/safety`
- `ragnaforge://docs/mcp`
- `ragnaforge://reports`
- `ragnaforge://reports/{id}`
- `ragnaforge://inputs/dry-run`

## Safety rules

- Resources are read-only.
- Resource paths stay inside `agentRoot`.
- Absolute paths are rejected.
- `..` traversal is rejected.
- Report reads are size-limited.
- Dry-run input resources list metadata only; they do not execute payloads.
