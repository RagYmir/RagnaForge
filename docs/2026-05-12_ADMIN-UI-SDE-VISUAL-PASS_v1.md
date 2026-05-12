# 2026-05-12 - Admin UI SDE visual pass v1

## Objetivo

Executar uma passada visual pequena e controlada na interface administrativa do RagnaForge, usando o RagnarokSDE apenas como referencia de organizacao visual e UX tecnica.

## Escopo desta rodada

- Dashboard
- Itens
- Equipamentos
- Seguranca/API

## O que mudou

### 1. Layout operacional de tres regioes

Foi criado um workspace visual com:

- navegacao/lista contextual a esquerda;
- formulario agrupado ao centro;
- painel de validacao, diff e JSON a direita.

Esse layout reduz rolagem desorganizada e aproxima a experiencia da leitura tecnica compacta observada no RagnarokSDE, sem copiar a interface desktop.

### 2. Componentes novos ou reforcados

- `PipelineWorkspaceLayout`
- `EntityGrid`
- `FieldGroup`
- `ReadinessRibbon`
- `ValidationMatrix`
- `DiffWorkbench`
- `DependencyTree`
- `SafetyBanner` reforcado
- `ClientSidePlanPanel` ampliado
- `AssetLookupPanel` ampliado
- `BytecodeBlockPanel` ampliado

### 3. Dashboard

Agora mostra:

- health/status da API;
- `ReadOnlyMode`, `ApplyEnabled` e `RollbackEnabled`;
- resumo de configuracao;
- status de `rAthena`, Patch, GRF Editor e contagem de GRFs via discovery;
- readiness por categoria;
- riscos pendentes;
- atalhos para os modulos de dry-run/diff-preview.

### 4. Itens

A aba foi reorganizada em:

- Identificacao
- Server DB
- Client-side
- Descricao
- Resource/Sprite

O inspetor lateral agora concentra:

- `ApplyReadiness`
- `ValidationMatrix`
- `ClientSidePlan`
- `AssetLookup`
- `BytecodeBlocks`
- risco de `Unknown Item/Apple`
- diff server/client
- JSON bruto

### 5. Equipamentos

A aba foi reorganizada em:

- Item base
- Equipamento
- Visual client-side
- Assets

O inspetor lateral agora concentra:

- `ClientSidePlan`
- `VisualClientSidePlan`
- `ItemAssetLookup`
- `VisualAssetLookup`
- `ShieldRestriction`
- `ViewID` / `ViewSprite`
- `BytecodeBlocks`
- validacoes
- diff
- JSON bruto

### 6. Seguranca/API

A aba agora mostra explicitamente:

- API Base URL
- API key configurada ou ausente
- autenticacao local
- operation guards
- CORS esperado
- limites de request
- correlationId
- link para OpenAPI

Mensagem fixa obrigatoria exibida:

`Apply e rollback nao existem nesta interface nesta fase. Esta UI e apenas para analise, dry-run e diff-preview.`

## Confirmacoes de seguranca

- Nenhum botao de `apply`
- Nenhum botao de `rollback`
- Nenhuma chamada CLI pelo frontend
- Nenhum novo endpoint perigoso
- UI continua consumindo apenas endpoints seguros da API

## Validacao executada

### Backend

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj`

Resultado:

- build OK
- testes OK: `97/97`

### Frontend

- `npm run build`
- `npm run test -- --run`

Resultado:

- build OK
- testes OK: `9/9`

## Smoke seguro desta rodada

- Confirmada ausencia de botoes/rotas operacionais de `apply` e `rollback` na navegacao principal.
- Confirmado que o cliente HTTP continua restrito a `health`, `status`, `safety/capabilities`, `config`, `discover`, `grf`, `dry-run` e `diff-preview`.
- Confirmado que `ProblemDetails`, warnings/errors e `correlationId` continuam renderizaveis nas telas auditadas.

## Limitacoes restantes

- O novo workspace visual ainda nao foi propagado para NPC, Monstro, Mapa, GRF e Auditoria.
- Ainda nao existe historico local de dry-runs.
- Ainda nao existe comparacao entre dry-runs.
- Ainda nao existe exportacao de relatorio pela UI.
- Ainda nao existe visualizador de assets mais rico.

## Proximo passo recomendado

Aplicar o mesmo padrao visual em:

1. NPC
2. Monstro
3. Mapa
4. GRF

Depois disso, fazer uma rodada de refinamento de UX com:

- filtros melhores;
- presets de formulario;
- historico local;
- comparacao entre dry-runs;
- exportacao de relatorios.
