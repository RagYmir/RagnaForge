# Agent Health Integration v1.0.1 - Hardening e limpeza

Data: 2026-05-18

## Objetivo

Fortalecer a integracao read-only entre o RagnaForge principal e o Agente Setimmo local, mantendo a API e a UI sem apply/rollback e sem escrita em rAthena, Patch/client, GRFs ou arquivos `.lub`.

## Escopo

Diretorio usado:

- `<WORKSPACE_ROOT>`

Endpoint auditado:

- `GET /api/agent/health`

Pagina frontend auditada:

- `Agent Health`

## Estado Git inicial

Branch inicial:

- `feature/spr-act-preview-readonly`

Hash inicial:

- `5a4761225d230ba9f253307ad17bdedb1c27b122`

Arquivos modificados ja existentes antes desta rodada:

- `backend/src/RagnaForge.Api/ApiSafetyPolicy.cs`
- `backend/src/RagnaForge.Api/Program.cs`
- `backend/src/RagnaForge.Api/appsettings.json`
- `frontend/src/App.tsx`
- `frontend/src/api/client.ts`
- `frontend/src/api/types.ts`
- `frontend/src/layouts/AppShell.tsx`
- `frontend/tsconfig.app.tsbuildinfo`

Arquivos untracked ja existentes antes desta rodada:

- `.codex/`
- `backend/src/RagnaForge.Api/AgentIntegration.cs`
- `frontend/src/pages/AgentHealthPage.tsx`
- `frontend/src/pages/AgentHealthPage.test.tsx`

## Comandos do Agente Setimmo executados

- `ragnaforge status --json`
- `ragnaforge doctor --json`
- `ragnaforge scan --project --json`
- `ragnaforge index --entities --json`
- `ragnaforge validate --json`

Resultados:

- `status`: OK, profile `teste`, fingerprint `11896c4101e0f6a3ed9e10acadf3b1e98c38225e2769aa823f99a470c739d9cb`
- `doctor`: OK, 31 checks, sem warnings/errors
- `scan`: OK, 284 files indexed
- `index`: OK, 76679 items, 2677 monsters, 13860 NPCs, 1100 maps
- `validate`: executou OK, mas `safeForAutomation=false` por 1084 issues do dataset externo: 1 error e 1083 warnings

O resultado de validate foi tratado como diagnostico. Ele nao autorizou nem provocou escrita externa.

## Backend

Arquivos auditados/reforcados:

- `backend/src/RagnaForge.Api/AgentIntegration.cs`
- `backend/src/RagnaForge.Api/Program.cs`
- `backend/src/RagnaForge.Api/ApiResponse.cs`
- `backend/src/RagnaForge.Api/appsettings.json`
- `backend/tests/RagnaForge.Tests/Program.cs`

Melhorias aplicadas:

- `GET /api/agent/health` usa caminho async em `ApiEndpointExecutor.ExecuteAsync`.
- Removido sync-over-async do endpoint.
- `RagnaForgeAgentCommandRunner` usa allowlist rigida.
- Comandos permitidos:
  - `status --json`
  - `doctor --json`
  - `scan --project --json`
  - `index --entities --json`
  - `validate --json`
- Comandos arbitrarios, apply e rollback nao sao permitidos.
- Execucao do processo usa `UseShellExecute=false`.
- stdout e stderr sao lidos de forma assincrona.
- Timeout retorna falha segura.
- Processo com timeout e encerrado pelo executor.
- DTO de health permanece pequeno e sem stdout bruto.
- Cache `entities_index.json` e `project_index.json` so e exibido quando `activeProfile` e `configFingerprint` batem com o status ativo.
- Cache ausente ou obsoleto retorna warning e nao exibe contagens como confiaveis.
- `AgentExePath`, `AgentCacheDir` e timeout seguem configuracao.

Configuracao:

- `appsettings.json` usa valores portaveis:
  - `AgentExePath`: `ragnaforge.exe`
  - `AgentCacheDir`: `cache/agent`
  - `AgentTimeoutSeconds`: `30`
- Caminhos locais reais devem ser fornecidos por `appsettings.Development.json`, `appsettings.Local.json` ignorado, ou env vars como `RagnaForge__Agent__AgentExePath` e `RagnaForge__Agent__AgentCacheDir`.

## Frontend

Arquivos auditados/reforcados:

- `frontend/src/pages/AgentHealthPage.tsx`
- `frontend/src/pages/AgentHealthPage.test.tsx`
- `frontend/src/api/client.ts`
- `frontend/src/api/types.ts`
- `frontend/src/App.tsx`
- `frontend/src/layouts/AppShell.tsx`

Melhorias aplicadas:

- Pagina `Agent Health` continua read-only.
- Renderiza estado OK.
- Renderiza warnings do Agent.
- Renderiza estado offline/agente indisponivel.
- Renderiza cache stale/obsoleto com aviso claro.
- Nao existe campo para comando arbitrario.
- Nao existe botao apply.
- Nao existe botao rollback.

## Testes

Backend:

- `dotnet build RagnaForge.slnx`: OK
- `dotnet test`: OK
- `dotnet run --project backend/tests/RagnaForge.Tests/RagnaForge.Tests.csproj`: OK, 123 tests passed

Frontend:

- `npm test`: OK, 30 tests passed
- `npm run build`: OK

Testes especificos adicionados/reforcados:

- runner bloqueia comando arbitrario
- runner trata agente indisponivel
- runner trata timeout
- runner le stdout e stderr
- summary trata cache ausente
- summary rejeita cache stale por profile/fingerprint
- summary retorna DTO sanitizado
- pagina Agent Health renderiza OK
- pagina Agent Health renderiza warnings
- pagina Agent Health renderiza agente offline
- pagina Agent Health renderiza cache stale
- pagina Agent Health nao mostra apply/rollback

## Limpeza e pacote

`.gitignore` cobre:

- `frontend/node_modules/`
- `frontend/dist/`
- `bin/`
- `obj/`
- `**/bin/`
- `**/obj/`
- `*.tsbuildinfo`
- `TestResults/`
- `*.trx`
- `logs/`
- `cache/`
- `data/cache/*`
- `data/logs/*`

Observacao:

- `frontend/tsconfig.app.tsbuildinfo` e `frontend/tsconfig.node.tsbuildinfo` foram removidos do indice Git nesta consolidacao e seguem preservados apenas como artefatos locais ignorados.

## Confirmacao de seguranca

- API continua sem endpoint apply.
- API continua sem endpoint rollback.
- Frontend continua sem botao apply.
- Frontend continua sem botao rollback.
- Endpoint `GET /api/agent/health` e read-only.
- Allowlist impede comando arbitrario.
- Nenhum `--confirm APPLY` foi usado.
- Nenhum `--confirm ROLLBACK` foi usado.
- rAthena externo nao foi modificado.
- Patch/client externo nao foi modificado.
- GRFs originais nao foram modificadas.
- `.lub` nao foi editado.
- Cache stale e tratado como warning e nao como dado confiavel.

## Limitacoes conhecidas

- Para a API encontrar o Agent em ambiente local, e necessario configurar `AgentExePath` e `AgentCacheDir` por arquivo local ignorado ou env vars.
- `validate` do Agent ainda reporta 1084 issues no dataset externo; isso permanece fora do escopo desta rodada.

## Proxima recomendacao

Rodar um smoke HTTP dedicado do `GET /api/agent/health` com `AgentExePath` configurado localmente e depois consolidar a documentacao historica que ainda cita checkpoints antigos apenas como contexto cronologico.
