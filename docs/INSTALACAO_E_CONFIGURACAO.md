# Instalacao e configuracao

## Pre-requisitos

- .NET SDK compativel com a solucao.
- Node.js LTS.
- PowerShell.
- Acesso local aos caminhos de rAthena, Patch/client, GRFs e GRF Editor.

## Configuracao local

Crie os arquivos locais a partir dos templates:

```powershell
Copy-Item .\data\manifests\repositories.example.json .\data\manifests\repositories.local.json
Copy-Item .\Agente_Setimmo\config\paths.example.json .\Agente_Setimmo\config\paths.json
```

Depois edite:

- `data/manifests/repositories.local.json`
- `Agente_Setimmo/config/paths.json`

Esses arquivos sao ignorados pelo Git porque contem caminhos reais do computador.

## AgentExePath

A API usa `RagnaForge:Agent:AgentExePath`.

Valor recomendado apos publicar:

```json
"AgentExePath": "dist/agent/agente-setimmo.exe"
```

O executavel de compatibilidade `dist/agent/ragnaforge.exe` tambem pode existir, mas o nome publico do agente e Agente Setimmo.

## Publicar executaveis locais

```powershell
.\scripts\publish-all.ps1
```

Isso gera:

- `dist/api/`
- `dist/agent/agente-setimmo.exe`
- `dist/agent/ragnaforge.exe`
- `dist/agent/ragnaforge.agentroot`
