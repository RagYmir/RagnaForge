# RagnarokSDE UI reference for RagnaForge v1

Data da analise: 2026-05-12
Tipo: referencia visual/UX, sem implementacao.

## Principio de adaptacao

O RagnarokSDE e um editor desktop WPF de databases. O RagnaForge deve usar a experiencia dele como referencia de organizacao, mas manter a identidade propria:

- web React/Vite;
- API segura;
- read-only, dry-run e diff-preview;
- dependencias explicitas;
- sem apply/rollback na interface;
- sem repair automatico;
- sem edicao de `.lub` bytecode.

## Padroes visuais do SDE que valem inspirar

### Grid de entidade + painel de detalhes

O padrao mais importante e a tela com lista/grid a esquerda, divisor e propriedades a direita. Isso torna databases grandes navegaveis.

Adaptacao:

- Usar `EntityGrid` a esquerda com filtros e busca.
- Usar formulario agrupado no centro.
- Usar resultado/diff/validacao no lado direito ou em abas inferiores.

### Busca contextual

O SDE usa busca sempre proxima da lista. O `DbSearchPanel` tem texto, reset e submenu.

Adaptacao:

- Busca por ID, AegisName, nome, resource e mapa em todos os modulos.
- Filtros rapidos por status: ready, blocked, warnings, bytecode, missing assets.
- Reset claro e atalhos de filtro visual.

### Validacao categorizada

O SDE separa erros de tabela, recurso e client item.

Adaptacao:

- Aba Validacao com categorias: Server DB, Client-side, Assets, GRF, Mapas, Bytecode, Conflitos.
- Resultado em tabela com severidade, entidade, arquivo, mensagem e acao recomendada.
- Raw view alternavel para auditoria tecnica.

### Preview perto do campo

O SDE mostra preview de item/resource perto do campo de resource.

Adaptacao:

- Itens: preview de icone quando o backend disponibilizar imagem segura.
- Equipamentos: preview de sprite/view plan quando houver asset indexado.
- NPCs e Monstros: status de sprite e preview futuro.
- Mapas: primeiro arvore de dependencias; preview visual so depois de parser seguro.

### Contexto server/client

O SDE trabalha com server DB e client tables no mesmo ambiente.

Adaptacao:

- Todo modulo deve mostrar `Server-side`, `Client-side`, `Assets`, `Diff` e `Validation`.
- A UI deve indicar quando o conteudo fica funcional no servidor, mas ainda nao no client.

## Aba Dashboard

### Inspiracao do SDE

O SDE deixa configuracoes, recursos carregados, encoding e tabelas visiveis no inicio. Isso reduz misterio em setups complexos.

### Adaptacao para RagnaForge

Dashboard deve virar painel de controle compacto:

- API health e status.
- `ReadOnlyMode = true`.
- `ApplyEnabled = false`.
- `RollbackEnabled = false`.
- rAthena, Patch, GRF Editor e GRFs detectados.
- client date detectado.
- perfil progressivo atual.
- riscos pendentes.
- readiness por modulo.
- ultimos dry-runs quando houver historico.

### Prioridade

Alta. E a primeira tela que reduz ansiedade operacional.

## Aba Itens

### Inspiracao do SDE

O SDE separa `Item` e `Client Items`, com preview de resource, campos identificados/unidentified e relacionamento server/client.

### Adaptacao para RagnaForge

Layout recomendado:

- Grid de propostas/itens no lado esquerdo.
- Formulario central com grupos: Identificacao, Server DB, Client-side, Descricao, Resource/Sprite.
- Painel direito com `ClientSidePlan`, `AssetLookup`, warnings/errors e readiness.
- Diff em aba inferior com server/client separados.

### Campos e paineis obrigatorios

- `ClientSidePlan`
- `ClientSideMode`
- `AssetLookup`
- `ExistingClientRegistration`
- `ProposedClientRegistration`
- `BytecodeBlocks`
- risco de `Unknown Item/Apple`
- warnings/errors
- diff server/client

### Acoes permitidas

- Gerar dry-run.
- Gerar diff-preview.
- Limpar formulario.
- Exportar relatorio local futuramente, se seguro.

### Acoes proibidas

- Apply.
- Rollback.
- Escrita em Patch/rAthena/GRF.

## Aba Equipamentos

### Inspiracao do SDE

O SDE trata equipamento como item com campos extras e usa tabelas Lua/Lub para ViewID, accessory, weapon e sprites.

### Adaptacao para RagnaForge

Layout recomendado:

- Secao "Item base".
- Secao "Equipamento".
- Secao "Visual client-side".
- Secao "Assets".
- Secao "Validacoes".
- Secao "Diff".

Subpaineis:

- Headgear/accessory.
- Robe.
- Weapon.
- Shield restriction.

### Campos e paineis obrigatorios

- `ClientSidePlan`
- `VisualClientSidePlan`
- `ItemAssetLookup`
- `VisualAssetLookup`
- `VisualTheme`
- `ShieldRestriction`
- `BytecodeBlocks`
- ViewID duplicado
- simbolo inseguro
- warnings/errors

### Prioridade visual

Alta. Equipamento e uma das telas com maior chance de confusao, entao precisa de agrupamento forte e labels claros.

## Aba NPCs

### Inspiracao do SDE

O SDE usa `jobname`, `npcidentity`, tabelas Lua/Lub e preview de ViewID/sprite.

### Adaptacao para RagnaForge

Layout recomendado:

- Formulario de NPC com mapa, coordenadas, direcao e sprite.
- Painel de sprite resolution.
- Painel de client identity.
- Painel de server-side script.
- Diff server/client textual.

### Estados visuais importantes

- Sprite padrao.
- Sprite custom no Patch.
- Sprite custom apenas em GRF.
- Sprite ambiguo.
- Identity client-side textual.
- Identity bloqueada por bytecode.
- Identity ausente.

### Nota sobre server-only

`--allow-server-only` pode aparecer apenas como informacao textual de CLI. Nao deve ser botao ou acao da UI nesta fase.

## Aba Monstros

### Inspiracao do SDE

O SDE usa subpaineis para stats, drops, MVP drops e mob skills, o que evita formulario linear demais.

### Adaptacao para RagnaForge

Layout recomendado:

- Subabas internas: Dados base, Stats, Drops, MVP, Skills, Spawns, Validacoes, Diff.
- Drops em tabela com item, chance, quantidade, tipo e MVP.
- Skills em tabela com skill, estado, chance, cast, target e campos suportados.
- Spawns em tabela com mapa, quantidade, X/Y, area, respawn e label.

### Campos e paineis obrigatorios

- `Drops`
- `Skills`
- `Spawns`
- `UnsupportedFields`
- `ValidationWarnings`
- `ValidationErrors`
- `ApplyReadiness`
- `PostWriteValidationPlan`
- diff de `mob_db`, `mob_avail`, `mob_skill_db` e spawn

## Aba Mapas

### Inspiracao do SDE

O SDE tem mapcache editor com lista de mapas, contagem e comandos. O RagnaForge precisa ir alem por causa das dependencias de assets.

### Adaptacao para RagnaForge

Layout recomendado:

- Campo principal de map name.
- Arvore de dependencias.
- Painel de map cache.
- Painel de map_index/config.
- Diff.

Arvore recomendada:

- Required files.
- Resolved assets.
- Missing assets.
- Ambiguous assets.
- Cache plan.
- Config plan.

### Estados de risco obrigatorios

- Mapa ja existe.
- Dependencias ausentes.
- Dependencias ambiguas.
- `map_cache.dat` necessario.
- Rename binario bloqueado.
- Apply indisponivel na interface.

## Aba GRF / Assets

### Inspiracao do SDE

`MultiGrfExplorer` organiza lista de assets, busca, encoding e preview.

### Adaptacao para RagnaForge

Layout recomendado:

- Lista de containers.
- Busca por nome.
- Filtro por extensao.
- Tabela de assets.
- Painel de proveniencia.
- Preview passivo futuro.

Filtros iniciais:

- `.spr`
- `.act`
- `.bmp`
- `.tga`
- `.rsw`
- `.gnd`
- `.gat`
- `.rsm`
- `.wav`

Proveniencias:

- `LocalIndex`
- `LiveScan`
- `LiveScanFallback`
- `ControlledGrfExtraction`

Nao extrair, copiar ou alterar assets nesta fase.

## Aba Validacao

### Inspiracao do SDE

`ValidationDialog` e um bom modelo de validacao orientada por categorias.

### Adaptacao para RagnaForge

Criar uma tela dedicada para consolidar:

- item validation;
- equipment validation;
- NPC validation;
- monster validation;
- map validation;
- client-side validation;
- asset validation.

Mostrar:

- erros;
- warnings;
- riscos;
- bytecode bloqueado;
- dependencias ausentes;
- conflitos de ID;
- conflitos de ViewID;
- conflitos de AegisName;
- conflitos de mapa;
- risco de `Unknown Item/Apple`.

Nao criar `Repair`.

## Aba Historico / Relatorios

### Inspiracao do SDE

O SDE tem backups, debug tables e resultados raw/list view.

### Adaptacao para RagnaForge

Manter read-only:

- relatorios gerados;
- auditoria pre-producao;
- docs importantes;
- ultimos dry-runs/diff-previews quando houver endpoint;
- readiness matrix.

Se nao houver endpoint, placeholder claro continua correto.

## Aba Seguranca / API

### Inspiracao do SDE

Configuracoes tecnicas ficam acessiveis em dialogs e settings.

### Adaptacao para RagnaForge

Mostrar:

- API Base URL;
- API key configurada ou ausente;
- status da autenticacao;
- `ReadOnlyMode`;
- `ApplyEnabled`;
- `RollbackEnabled`;
- OperationKinds permitidos;
- OperationKinds bloqueados;
- CORS;
- rate limit;
- correlationId;
- link para OpenAPI.

Mensagem fixa recomendada:

`Apply e rollback nao existem nesta interface nesta fase. Esta UI e apenas para analise, dry-run e diff-preview.`

## Melhorias de UX priorizadas

1. Converter paginas longas em workspaces com lista, formulario e painel de resultado.
2. Substituir inputs livres por selects, toggles e chips em campos com dominio conhecido.
3. Criar subabas internas por categoria de resultado: Dry-run, Validacao, Dependencias, Diff, JSON.
4. Criar grids especificos para drops, skills e spawns.
5. Criar arvore de dependencias para mapas e assets.
6. Melhorar o diff viewer para agrupar server/client/assets/config.
7. Criar paines de preview passivo para item/resource/sprite quando a API suportar.
8. Criar validation center read-only inspirado no SDE, sem repair.
9. Criar presets de formulario por tipo de conteudo.
10. Corrigir encoding dos textos atuais da UI antes de qualquer polimento visual grande.

## Riscos de copiar errado

- Transformar a UI web em desktop portado.
- Expor comandos perigosos por imitar menus de editor.
- Misturar analise com escrita.
- Sugerir que bytecode `.lub` e editavel.
- Criar visual bonito mas menos seguro.
- Perder clareza de dry-run/diff-preview.

## Criterio para aprovar implementacao visual futura

Antes de implementar a adaptacao, cada mudanca deve respeitar:

- nenhum endpoint novo de escrita;
- nenhum botao de apply/rollback;
- nenhum CLI workaround;
- nenhum asset copy;
- nenhum `.lub` bytecode editavel;
- todos os warnings/errors visiveis;
- `correlationId` visivel em erro;
- diff-preview sempre revisavel antes de qualquer decisao futura.
