# API read-only, dry-run e diff-preview v1

Data: 2026-05-11

## Resumo

Primeira API ASP.NET Core criada em modo seguro. Esta versao expoe apenas operacoes de leitura, validacao, discovery, indexacao/inspect de GRF, dry-run e diff-preview.

Apply e rollback nao foram expostos por endpoint nesta etapa.

## Projeto

- Projeto novo: `backend/src/RagnaForge.Api/RagnaForge.Api.csproj`
- Entry point: `backend/src/RagnaForge.Api/Program.cs`
- Servico de orquestracao: `backend/src/RagnaForge.Api/RagnaForgeApiService.cs`
- Politica de seguranca: `backend/src/RagnaForge.Api/ApiSafetyPolicy.cs`
- Contratos HTTP: `backend/src/RagnaForge.Api/ApiContracts.cs`

## Endpoints seguros

- `GET /health`
- `GET /api/status`
- `GET /api/safety/capabilities`
- `POST /api/config/validate`
- `POST /api/discover`
- `POST /api/grf/index`
- `POST /api/grf/inspect`
- `POST /api/items/dry-run`
- `POST /api/items/diff-preview`
- `POST /api/equipment/dry-run`
- `POST /api/equipment/diff-preview`
- `POST /api/npcs/dry-run`
- `POST /api/npcs/diff-preview`
- `POST /api/monsters/dry-run`
- `POST /api/monsters/diff-preview`
- `POST /api/maps/dry-run`
- `POST /api/maps/diff-preview`

## Endpoints nao expostos

- `item apply/rollback`
- `equipment apply/rollback`
- `npc apply/rollback`
- `monster apply/rollback`
- `map apply/rollback`

Chamadas como `POST /api/items/apply` retornam `404`, confirmando que escrita real nao existe nesta primeira API.

## Politica de seguranca

`ApiSafetyPolicy` declara explicitamente os writes desabilitados:

- `item.apply`, `item.rollback`
- `equipment.apply`, `equipment.rollback`
- `npc.apply`, `npc.rollback`
- `monster.apply`, `monster.rollback`
- `map.apply`, `map.rollback`

Tambem expoe capacidades por categoria, sempre com `Apply = false` e `Rollback = false` para pipelines de conteudo.

## Workspace root

A API resolve o workspace automaticamente procurando `RagnaForge.slnx` ou `data/manifests/repositories.local.json` ao subir a partir de subpastas.

Tambem aceita override por configuracao:

```powershell
dotnet run --project backend\src\RagnaForge.Api\RagnaForge.Api.csproj --urls http://127.0.0.1:5099 --RagnaForge:WorkspaceRoot="<WORKSPACE_ROOT>"
```

## Smokes executados

```powershell
dotnet build RagnaForge.slnx
dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj
```

Smoke HTTP local:

- `GET /health` retornou `ok`.
- `GET /api/status` retornou `Mode = read-only-dry-run-diff-preview`.
- `POST /api/config/validate` validou `data/manifests/repositories.local.json`.
- `POST /api/items/apply` retornou `404`.

## Testes adicionados

- `API safety policy disables write endpoints`
- `API service status reports safe mode`
- `API service runs item dry-run from manifest`

Resultado: `84/84` testes OK.

## Limitacoes

- API nao possui apply/rollback.
- API nao possui fila/job runner.
- API nao possui upload/copia de assets GRF para Patch.
- `map apply` continua fora da API ate haver caso real sem ambiguidade/dependencia pendente.
- Antes de liberar writes por API, ainda faltam politica de asset obrigatorio por categoria e decisao sobre persistir `ClientDate` detectado.

## Hardening posterior

Em `docs/2026-05-11_API-HARDENING-AUTH-GUARDS_v1.md`, a API passou a ter:

- chave local por `X-RagnaForge-Api-Key`;
- `ProblemDetails`;
- `ApiResponse<T>`;
- operation guards;
- rate/concurrency limits;
- CORS restrito;
- correlationId;
- OpenAPI com security scheme.

## Proximo passo recomendado

Implementar a camada de API read-only completa para consumo administrativo, com autenticacao/controle de origem antes de qualquer endpoint de escrita.
