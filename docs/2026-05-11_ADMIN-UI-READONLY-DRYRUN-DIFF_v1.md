# Admin UI read-only, dry-run e diff-preview

Data: 2026-05-11

## Objetivo

Iniciar a interface administrativa do RagnaForge consumindo apenas endpoints seguros da API endurecida:

- `health`
- `status`
- `config validate`
- `discover`
- `grf index`
- `grf inspect`
- `item dry-run`
- `item diff-preview`
- `equipment dry-run`
- `equipment diff-preview`
- `npc dry-run`
- `npc diff-preview`
- `monster dry-run`
- `monster diff-preview`
- `map dry-run`
- `map diff-preview`

Nao existem rotas, botoes ou atalhos de `apply`/`rollback` nesta fase.

## Stack escolhida

- React 19
- TypeScript
- Vite
- TanStack Query
- React Router
- Vitest + Testing Library

## Estrutura criada

`frontend/`

- `package.json`
- `vite.config.ts`
- `vitest.config.ts`
- `tsconfig*.json`
- `index.html`
- `src/main.tsx`
- `src/App.tsx`
- `src/api/`
- `src/components/`
- `src/features/connection/`
- `src/features/shared/`
- `src/layouts/`
- `src/pages/`
- `src/styles/`
- `src/test/`

## Seguranca refletida na UI

- Banner de modo seguro no dashboard.
- Exibicao explicita de:
  - `ReadOnlyMode`
  - `ApplyEnabled = false`
  - `RollbackEnabled = false`
- Cliente HTTP sempre envia:
  - `X-RagnaForge-Api-Key`
  - `X-Correlation-Id`
- `ProblemDetails` e `ApiResponse<T>` sao tratados sem mascarar warnings e errors.
- A interface nao chama CLI nem cria qualquer workaround de escrita.
- Rotas de `apply/rollback` mostram apenas:
  - `Apply/Rollback via interface esta desabilitado nesta fase.`

## Telas implementadas

- Dashboard
- Configuracao
- Discovery
- GRF
- Itens
- Equipamentos
- NPCs
- Monstros
- Mapas
- Auditoria/Relatorios
- Seguranca/API

## Componentes principais

- `ConnectionPanel`
- `SafetyBanner`
- `ApiStatusBadge`
- `ProblemDetailsView`
- `DiffViewer`
- `ClientSidePlanPanel`
- `AssetLookupPanel`
- `ApplyReadinessPanel`
- `BytecodeBlockPanel`
- `PipelineReportView`
- `JsonInspector`

## Smoke e validacao desta rodada

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- `npm install`
- `npm run build`
- `npm run test`

Resultado:

- backend OK
- frontend build OK
- frontend tests OK

## Limites atuais

- Nenhum `apply` ou `rollback` por UI.
- Nenhuma copia de asset para Patch.
- Nenhuma edicao de `.lub` bytecode.
- Auditoria/Relatorios ainda sem endpoint dedicado; a tela fica como placeholder seguro.
- UX visual ainda precisa de rodada propria antes de ser considerada fechada.

## Proximo passo

Rodada de UX e auditoria visual da interface read-only antes de qualquer conversa sobre escrita por HTTP.
