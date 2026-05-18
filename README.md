# RagnaForge

RagnaForge e uma ferramenta administrativa para pipelines seguros sobre rAthena, Patch/client e GRFs. O foco atual nao e CRUD nem escrita direta por API/UI; e uma esteira de leitura, analise, `dry-run`, `diff-preview`, validacao e auditoria.

## Estado atual

- API: `read-only`, `dry-run` e `diff-preview`
- Admin UI: `read-only`, `dry-run` e `diff-preview`
- CLI: pipelines de apply/rollback existem, mas continuam fora da API e da interface e exigem confirmacao explicita
- Agent auxiliar: disponivel para `status`, `doctor`, `scan`, `index`, `validate`, `baseline` e `health`

## Regras de seguranca

- Nao existem endpoints de `apply` ou `rollback` na API
- Nao existem botoes ou rotas funcionais de `apply` ou `rollback` na UI
- `.lub` bytecode continua bloqueado
- GRFs originais nao sao alteradas
- rAthena e Patch/client externos nao sao alterados por API/UI
- Asset preview segue read-only

## Preview de assets

- Bitmaps (`.bmp`, `.png`, `.jpg`, `.jpeg`, `.webp`): preview visual read-only
- `.spr`: preview visual best-effort com fallback para metadados
- `.act`: metadata-only no v1
- `.tga`, `.gat`, `.gnd`, `.rsw`, `.rsm`: placeholders informativos nesta fase

## Agent Health integration

A branch atual inclui uma integracao read-only com o RagnaForge Agent:

- endpoint `GET /api/agent/health`
- pagina `Agent Health` no frontend
- allowlist rigida no backend
- sem comando arbitrario
- sem apply/rollback
- sem shell generico

## Como configurar e rodar

### 1. Manifesto local

Crie `data/manifests/repositories.local.json` a partir do template:

```sh
cp data/manifests/repositories.example.json data/manifests/repositories.local.json
```

Preencha os caminhos locais reais para rAthena, Patch/client, GRFs e GRF Editor.

> Nunca commite `repositories.local.json`.

### 2. Backend

```sh
dotnet build RagnaForge.slnx
dotnet run --project backend/tests/RagnaForge.Tests/RagnaForge.Tests.csproj
dotnet run --project backend/src/RagnaForge.Api/RagnaForge.Api.csproj
```

### 3. Frontend

```sh
cd frontend
npm install
npm run test
npm run dev
```

## Status de testes desta consolidacao

- Backend: `126/126` testes passando
- Frontend: `30/30` testes passando
- Agent auxiliar: `183/183` testes passando

## Avisos finais

- Nao commite `.env`, `repositories.local.json`, caches, indexes, logs reais, `node_modules`, `dist`, `tmp` ou `.tsbuildinfo`
- Toda mudanca deve passar por build, testes e auditoria anti-apply antes de merge
