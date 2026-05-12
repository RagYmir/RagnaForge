# API hardening: auth, guards e contratos v1

Data: 2026-05-11

## Resumo

A API foi endurecida antes da interface administrativa. Ela continua limitada a leitura, discovery, GRF index/inspect, dry-run e diff-preview.

Apply e rollback seguem ausentes da API.

## Autenticacao local

A API usa chave local por header:

```text
X-RagnaForge-Api-Key
```

A chave pode ser configurada por:

```powershell
$env:RAGNAFORGE_API_KEY = "sua-chave-local"
```

ou por configuracao:

```json
{
  "RagnaForge": {
    "Api": {
      "ApiKey": "sua-chave-local"
    }
  }
}
```

Por padrao:

- `RequireApiKey = true`
- `ReadOnlyMode = true`
- `EnableApplyEndpoints = false`
- `EnableRollbackEndpoints = false`
- `AllowDevelopmentWithoutApiKey = false`

`/health` e `/openapi/v1.json` ficam publicos. Os endpoints sob `/api` exigem a chave.

## Operation guards

Foi criado `ApiOperationGuard` com `OperationKind`:

- `ReadOnly`
- `DryRun`
- `DiffPreview`
- `Apply`
- `Rollback`
- `FileWrite`
- `CacheWrite`
- `ExternalRepoWrite`
- `GrfWrite`

Permitidos nesta API:

- `ReadOnly`
- `DryRun`
- `DiffPreview`
- `CacheWrite` somente para cache/index local do workspace

Bloqueados nesta API:

- `Apply`
- `Rollback`
- `FileWrite`
- `ExternalRepoWrite`
- `GrfWrite`

Tambem foram adicionados limites para requests de GRF, incluindo `MaxGrfContainersPerRequest`.

## ProblemDetails

Erros agora usam `ProblemDetails` com extensoes:

- `errorCode`
- `correlationId`
- `path`
- `timestamp`
- `validationErrors`, quando aplicavel

Casos cobertos:

- `400` request invalido
- `401` sem chave ou chave invalida
- `403` operacao bloqueada
- `404` endpoint inexistente
- `413` payload grande demais
- `422` validacao de payload/dominio
- `429` rate/concurrency limit
- `500` erro inesperado sanitizado

## ApiResponse

Respostas de sucesso passam pelo wrapper:

- `success`
- `data`
- `warnings`
- `errors`
- `generatedAt`
- `correlationId`
- `operationKind`
- `readOnlyMode`
- `durationMs`

## Correlation ID

A API aceita:

```text
X-Correlation-Id
```

Se ausente, gera um ID novo. O valor volta no header e no corpo de erro/sucesso.

## Rate e concurrency limits

Foram adicionados limites em memoria:

- global: `GlobalRequestsPerMinute`
- GRF: `GrfRequestsPerMinute`
- mapa: `MapRequestsPerMinute`
- operacoes pesadas: `HeavyOperationConcurrency`

GRF e mapa usam policy mais restritiva e concurrency guard.

## CORS

CORS e restrito por padrao:

- `http://127.0.0.1:5173`
- `http://localhost:5173`

Nao ha `AllowAnyOrigin`.

## OpenAPI

`/openapi/v1.json` expoe:

- descricao da API local
- esquema de API key `RagnaForgeApiKey`
- header `X-RagnaForge-Api-Key`
- tags por categoria
- nota de que apply/rollback nao existem
- respostas esperadas incluindo `ProblemDetails`

## Smokes executados

- `/health` sem key: `200`
- `/api/status` sem key: `401` com `ProblemDetails`
- `/api/status` com key: `200` com `ApiResponse`
- `/api/items/dry-run` com key: `200`, `operationKind = DryRun`
- `/api/items/apply` com key: `404`, sem rota de escrita
- payload invalido em `/api/grf/inspect`: `422` com `ProblemDetails`
- OpenAPI contem `RagnaForgeApiKey`
- rate limit com limite reduzido: terceira chamada retornou `429`
- CORS allowed origin retorna header; origin externo nao recebe `Access-Control-Allow-Origin`

## Testes adicionados

- API options default to safe local mode
- API key validator requires configured header
- API key validator accepts valid key
- API key validator rejects invalid key
- API operation guard blocks dangerous operations
- API operation guard enforces GRF container limit
- API ProblemDetails includes correlation id
- API response wrapper includes correlation and read-only mode
- API rate limiter rejects after configured limit
- API OpenAPI document exposes API key scheme
- API CORS defaults are restricted to local origins
- API service blocks workspace path traversal
- API service blocks oversized GRF index request

Resultado final: `97/97` testes OK.

## Como rodar

```powershell
$env:RAGNAFORGE_API_KEY = "local-dev-key"
dotnet run --project backend\src\RagnaForge.Api\RagnaForge.Api.csproj --urls http://127.0.0.1:5099
```

Exemplo:

```powershell
Invoke-RestMethod `
  -Uri http://127.0.0.1:5099/api/status `
  -Headers @{ "X-RagnaForge-Api-Key" = "local-dev-key"; "X-Correlation-Id" = "manual-test-1" }
```

## Limitacoes restantes

- Sem autenticacao multiusuario.
- Sem persistencia de rate limit entre processos.
- Sem apply/rollback via API.
- Sem fila/job runner.
- Sem politica final de asset obrigatorio para liberar escrita futura.
- Sem decisao final sobre persistir `ClientDate`.

## Proximo passo

Iniciar a interface administrativa consumindo somente endpoints seguros: health/status, config validate, discover, GRF index/inspect, dry-run e diff-preview.
