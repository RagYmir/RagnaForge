# Ragna_Forge

Ragna_Forge e uma esteira segura para analisar, validar e preparar mudancas de servidor Ragnarok baseado em rAthena, Patch/client e GRFs.

Ele nao e um CRUD simples. O uso normal hoje e:

1. ler o estado do projeto;
2. resolver dependencias;
3. rodar `dry-run`;
4. ver `diff-preview`;
5. gerar relatorio;
6. revisar com uma pessoa.

## Partes do projeto

- `backend/`: API local do Ragna_Forge.
- `frontend/`: interface administrativa React/Vite.
- `Agente_Setimmo/`: Agente Setimmo, ferramenta local de diagnostico, validacao, conhecimento e auditoria.
- `data/manifests/`: templates de configuracao local.
- `scripts/`: limpeza, publicacao, smoke e pacote limpo.
- `dist/`: saida local publicada da API e do Agente. Binarios gerados nao entram no Git.

## O que esta liberado hoje

- API/UI em modo `read-only`.
- `dry-run`.
- `diff-preview`.
- Validacao de recursos.
- Preview passivo de assets.
- RagnaKnowledge.
- Pipeline API.
- Agent Health com o Agente Setimmo.
- MCP read-only no Agente Setimmo.

## O que NAO esta liberado hoje

- Apply real pela API.
- Rollback real pela API.
- Botao Apply na UI.
- Botao Rollback na UI.
- Escrita direta em rAthena.
- Escrita direta no Patch/client.
- Alteracao de GRF original.
- Edicao de `.lub`.

O Agente Setimmo tambem mantem `apply` bloqueado por politica neste MVP. Rollback real nao e executado pela API/UI.

## Primeiros passos

### 1. Instale os pre-requisitos

- .NET SDK usado pelo projeto.
- Node.js LTS.
- PowerShell.

### 2. Configure caminhos locais

Copie o template:

```powershell
Copy-Item .\data\manifests\repositories.example.json .\data\manifests\repositories.local.json
Copy-Item .\Agente_Setimmo\config\paths.example.json .\Agente_Setimmo\config\paths.json
```

Edite os arquivos locais com os caminhos reais de rAthena, Patch/client, GRFs e GRF Editor.

Nunca commite `repositories.local.json`, `.env` ou `Agente_Setimmo/config/paths.json`.

### 3. Publique os executaveis locais

```powershell
.\scripts\publish-all.ps1
```

Saidas esperadas:

- API: `.\dist\api\`
- Agente Setimmo: `.\dist\agent\agente-setimmo.exe`
- Compatibilidade do agente: `.\dist\agent\ragnaforge.exe`

### 4. Rode o Agente Setimmo

```powershell
cd .\Agente_Setimmo
dotnet run --project src\RagnaForge.Agent.Cli -- status --json
dotnet run --project src\RagnaForge.Agent.Cli -- doctor --json
dotnet run --project src\RagnaForge.Agent.Cli -- health --json
```

### 5. Rode a API

```powershell
dotnet run --project .\backend\src\RagnaForge.Api\RagnaForge.Api.csproj
```

### 6. Rode a UI

```powershell
cd .\frontend
npm.cmd ci
npm.cmd run dev
```

Acesse a URL exibida pelo Vite, normalmente `http://localhost:5173`.

## Testes

```powershell
dotnet build .\RagnaForge.slnx
dotnet run --project .\backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj

cd .\frontend
npm.cmd ci
npm.cmd run build
npm.cmd run test
cd ..

cd .\Agente_Setimmo
dotnet build .\RagnaForge.Agent.slnx
dotnet test .\RagnaForge.Agent.slnx
cd ..
```

Ultima validacao antes da reorganizacao:

- Backend: `145/145` testes.
- Frontend: `33/33` testes.
- Agente Setimmo: `199/199` testes.

## Limpeza e pacote limpo

Limpar artefatos locais:

```powershell
.\scripts\clean-workspace.ps1
```

Gerar pacote limpo:

```powershell
.\scripts\package-clean.ps1
```

O pacote nao deve conter `.git`, `node_modules`, `bin`, `obj`, `tmp` real, cache/log real, `.env`, `repositories.local.json` ou assets privados.

## Leitura recomendada

- `docs/GUIA_RAPIDO_PARA_LEIGOS.md`
- `docs/INSTALACAO_E_CONFIGURACAO.md`
- `docs/COMO_RODAR_API_UI_AGENT.md`
- `docs/AGENTE_SETIMMO.md`
- `docs/ESTRUTURA_DO_PROJETO.md`
- `docs/LIMPEZA_E_RELEASE.md`
- `docs/TROUBLESHOOTING.md`
- `SECURITY.md`
