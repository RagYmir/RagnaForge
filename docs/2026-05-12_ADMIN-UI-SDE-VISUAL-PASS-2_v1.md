# Admin UI SDE Visual Pass 2

Data: 2026-05-12

## Escopo

Segunda passada visual pequena e controlada da interface administrativa do RagnaForge, mantendo:

- React + Vite;
- consumo apenas de endpoints seguros;
- zero `apply`/`rollback` na UI;
- nenhuma chamada de CLI pelo frontend;
- nenhuma escrita em rAthena, Patch, GRF ou `.lub` bytecode.

## Telas alteradas

- `NPCs`
- `Monstros`
- `Mapas`
- `GRF / Assets`
- `Validacao`
- `Historico / Relatorios`

## Padrao reutilizado

Foi replicado o mesmo workspace operacional da primeira passada:

- lista ou grid lateral para navegacao contextual;
- formulario agrupado no painel central;
- inspetor lateral com readiness, validacao, diff e JSON;
- `warnings` e `errors` sempre visiveis;
- `correlationId` visivel quando a resposta da API existir;
- banner e mensagens reforcando modo seguro e ausencia de fluxos de escrita.

## Componentes novos ou expandidos

- `ClientIdentityPlanPanel`
- `MonsterDropsGrid`
- `MonsterSkillsGrid`
- `MonsterSpawnsGrid`
- `MapCachePlanPanel`
- `GrfAssetTable`
- `ReportListPanel`
- `ValidationIssueTable`
- helper compartilhado `viewData.ts`

## Componentes reutilizados

- `PipelineWorkspaceLayout`
- `EntityGrid`
- `FieldGroup`
- `ReadinessRibbon`
- `ValidationMatrix`
- `DiffWorkbench`
- `DependencyTree`
- `BytecodeBlockPanel`
- `AssetLookupPanel`
- `ClientSidePlanPanel`
- `SafetyBanner`

## Endpoints consumidos

- `GET /health`
- `GET /api/status`
- `POST /api/config/validate`
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

## Validacao executada

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- `cmd /c npm.cmd run build`
- `cmd /c npm.cmd run test`
- smoke HTTP seguro local:
  - `/health` = `200`
  - `/api/status` com chave = `200`
  - frontend servido localmente = `200`
  - `item dry-run` = `200`
  - `npc dry-run` = `200`
  - `ProblemDetails` estruturado confirmado em erro de chamada invalida
  - `POST /api/items/apply` = `404`

## Resultado desta passada

- a UI restante ficou consistente com o padrao visual/operacional ja introduzido;
- `NPCs`, `Monstros` e `Mapas` agora mostram melhor readiness, validacao e diff;
- `GRF / Assets` ganhou foco em tabela, filtro por extensao e proveniencia;
- `Validacao` virou um centro read-only de issues e bloqueios;
- `Historico / Relatorios` passou a expor documentos importantes e readiness como superficie segura.

## Limitacoes ainda abertas

- ainda nao existe historico local persistido de dry-runs;
- ainda nao existe exportacao de relatorios pela UI;
- ainda nao existe comparacao entre dry-runs;
- preview passivo de assets continua como proxima etapa;
- algumas superficies continuam dependentes de endpoint futuro ou agregacao local segura para sair do placeholder.

## Confirmacao de seguranca

- `apply` nao existe na UI;
- `rollback` nao existe na UI;
- nao foram criados botoes perigosos;
- nao foram criados endpoints perigosos;
- nenhum repositorio externo foi alterado por esta rodada.

## Proximo passo recomendado

Fazer uma auditoria visual completa da interface inteira e, em seguida, planejar ganhos de produtividade seguros:

- presets de formularios;
- historico local de dry-runs;
- exportacao de relatorios;
- comparacao entre dry-runs;
- preview passivo de assets.
