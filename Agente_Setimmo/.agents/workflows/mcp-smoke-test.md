# Workflow: MCP Smoke Test

1. Run `dotnet build RagnaForge.Agent.slnx`.
2. Run `dotnet test RagnaForge.Agent.slnx`.
3. Run `dotnet run --project src/RagnaForge.Agent.Mcp -- --list-tools`.
4. Run `dotnet run --project src/RagnaForge.Agent.Mcp -- --list-resources`.
5. Run `dotnet run --project src/RagnaForge.Agent.Mcp -- --list-prompts`.
6. Confirm allowed tools, resources, and prompts are listed.
7. Confirm `ragnaforge_apply` and `ragnaforge_rollback_confirm` are not listed as available tools.
8. Run CLI smoke checks:
   - `dotnet run --project src/RagnaForge.Agent.Cli -- status --json`
   - `dotnet run --project src/RagnaForge.Agent.Cli -- doctor --json`
9. Confirm no rAthena, Patch/client, GRF or `.lub` files were modified.
