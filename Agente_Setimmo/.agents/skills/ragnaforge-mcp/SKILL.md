# Skill: RagnaForge MCP

description: Use para operar o Agente Setimmo MCP Preview v1.1.0 com ferramentas seguras e sem apply/rollback real.

## Commands

```sh
dotnet run --project src/RagnaForge.Agent.Mcp -- --list-tools
dotnet run --project src/RagnaForge.Agent.Mcp
```

## Safety

- Nao chamar apply real.
- Nao chamar rollback confirm.
- Usar apenas ferramentas MCP permitidas em `docs/MCP.md`.
- Dry-run MCP deve salvar input apenas em `inputs/dry-run`.
- Verificar `safeForAutomation`, `activeProfile` e `configFingerprint` em respostas.
