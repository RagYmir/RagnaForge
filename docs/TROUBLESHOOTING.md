# Troubleshooting

## O Agente Setimmo nao abre

1. Confirme se existe `Agente_Setimmo/config/paths.json`.
2. Se nao existir, copie o exemplo:

```powershell
Copy-Item .\Agente_Setimmo\config\paths.example.json .\Agente_Setimmo\config\paths.json
```

3. Rode:

```powershell
cd .\Agente_Setimmo
dotnet run --project src\RagnaForge.Agent.Cli -- doctor --json
```

## /api/agent/health mostra agente indisponivel

Publique o agente:

```powershell
.\scripts\publish-agent.ps1
```

Depois confira `backend/src/RagnaForge.Api/appsettings.json`:

```json
"AgentExePath": "dist/agent/agente-setimmo.exe"
```

## safeForApply esta false

Isso e esperado enquanto houver blockers de dados externos. `safeForReadOnlyWork` e `safeForDryRun` podem continuar true.

## Frontend falha apos limpeza

Reinstale dependencias:

```powershell
cd .\frontend
npm.cmd ci
```

## Pacote saiu sujo

Rode:

```powershell
.\scripts\clean-workspace.ps1 -IncludeNodeModules -CleanPublishOutput
.\scripts\package-clean.ps1
```
