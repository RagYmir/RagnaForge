# Agente Setimmo - MCP Preview Report

## Version

`1.1.0-mcp-preview`

## Status

- Tests: `166/166 PASS`
- MCP preview: implemented
- Installed/portable mode: implemented
- Real apply: blocked by safety policy
- Real rollback: blocked by safety policy

## MCP

The MCP server is available at `src/RagnaForge.Agent.Mcp` and exposes only safe read-only/planning tools. Destructive tools such as real apply and real rollback are not exposed.

## Installed Mode

The portable executable is expected at `dist/agente-setimmo/ragnaforge.exe`, with `dist/agente-setimmo/ragnaforge.agentroot` pointing to the configured agent root. The CLI also supports `RAGNAFORGE_AGENT_ROOT` for explicit resolution.

## Packaging Notes

Final packages must exclude build/test/cache/log artifacts while preserving source, documentation, configuration templates, `.gitkeep` files, and the installed executable payload under `dist/agente-setimmo`.
