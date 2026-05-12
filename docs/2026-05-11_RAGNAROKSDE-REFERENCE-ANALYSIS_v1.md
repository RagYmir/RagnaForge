# RagnarokSDE reference analysis v1

Data da analise: 2026-05-12
Escopo: leitura tecnica e visual/UX em modo somente leitura.

## Caminhos analisados

- RagnarokSDE: `<RAGNAROK_SDE_PATH>`
- RagnaForge: `<WORKSPACE_ROOT>`
- Interface atual do RagnaForge: `frontend/`

Nenhum arquivo em `<RAGNAROK_SDE_PATH>` foi alterado.
Nenhum arquivo de rAthena, Patch ou GRF foi alterado.
Nenhum fluxo de apply/rollback foi criado.

## Arquivos e areas inspecionadas

- `README.md`
- `SDE.sln`
- `SDE/SDE.csproj`
- `SDE/View/SdeEditor.xaml`
- `SDE/View/MultiGrfExplorer.xaml`
- `SDE/View/Controls/DbSearchPanel.xaml`
- `SDE/View/Dialogs/ValidationDialog.xaml`
- `SDE/View/Dialogs/LuaTablesDialog.xaml`
- `SDE/View/Dialogs/ViewIdPreviewDialog.xaml`
- `SDE/Tools/SDEMapcache/MapcacheDialog.xaml`
- `SDE/Editor/Generic/TabsMakerCore/GDbTab.xaml`
- `SDE/Editor/Generic/TabsMakerCore/GTabsMaker.cs`
- `SDE/Editor/Generic/TabsMakerCore/TabGenerator.cs`
- `SDE/Editor/Generic/Core/Databases.cs`
- `SDE/Editor/Generic/Lists/ServerDBs.cs`
- `SDE/Editor/Generic/Parsers/DbIOClientItems.cs`
- `SDE/Editor/Generic/UI/CustomControls/PreviewItemInGame.xaml`
- `SDE/Editor/Generic/UI/CustomControls/CustomResourceProperty.cs`
- `SDE/Editor/Generic/UI/CustomControls/QueryNormalDrops.cs`
- `SDE/Editor/Generic/UI/CustomControls/QueryMvpDrops.cs`
- `SDE/Editor/Generic/UI/CustomControls/QueryMobSkills.cs`
- `SDE/Editor/Engines/RepairEngine/DbValidationEngine.cs`
- `SDE/Editor/Engines/RepairEngine/ClientItemsValidation.cs`
- `SDE/Editor/Engines/RepairEngine/ResourceValidation.cs`
- `SDE/Editor/Engines/LuaEngine/LuaHelper.cs`
- `SDE/Editor/Engines/LuaEngine/AccessoryTable.cs`

## Licenca e reuso

Nao encontrei arquivo `LICENSE`, `COPYING` ou `NOTICE` no pacote local analisado. O `README.md` descreve o projeto como editor de databases de Ragnarok Online e cita compatibilidade recente com rAthena/client, `GRF.dll` e `utilities.dll`, mas nao define licenca no material local.

Decisao recomendada:

- Usar RagnarokSDE como referencia tecnica, visual e conceitual.
- Nao copiar codigo, XAML, estilos, assets ou estruturas proprietarias sem verificacao de licenca/autorizacao explicita.
- Nao portar WPF para o RagnaForge.
- Manter React/Vite no frontend atual.
- Reaproveitar ideias de organizacao, nao implementacoes.

## O que o RagnarokSDE faz bem

O SDE organiza database complexa em um modelo de abas por entidade, com uma lista/grid persistente na lateral e um painel de propriedades detalhado para o item selecionado. Esse padrao aparece em `GDbTab.xaml`: coluna esquerda de busca/listagem, `GridSplitter`, e painel principal gerado por metadados.

Ele usa busca local forte por entidade. `DbSearchPanel.xaml` combina campo de busca, reset e submenu/filtros, o que reduz atrito em databases grandes.

Ele trata server-side e client-side como universos relacionados. `Databases.cs`, `ServerDBs.cs` e `DbIOClientItems.cs` mostram separacao entre `Items`, `Items2/import`, `CItems`, tabelas TXT antigas e Lua/Lub.

Ele tem uma ideia boa de validacao agrupada. `ValidationDialog.xaml` separa `Table errors`, `Resource errors`, `Client Items` e `Results`, com list view estruturado e raw view alternavel.

Ele da importancia a preview e asset lookup. `PreviewItemInGame.xaml`, `CustomResourceProperty.cs`, `ViewIdPreviewDialog.xaml` e `MultiGrfExplorer.xaml` mostram recursos visuais perto dos campos que dependem deles.

Ele separa drops e skills de monstro em subpaineis dedicados. `QueryNormalDrops.cs`, `QueryMvpDrops.cs` e `QueryMobSkills.cs` sao boas referencias de UX para nao transformar monstro em um formulario linear gigante.

Ele usa icones e contexto de acao de forma consistente. Menus e context menus indicam copiar, exportar, validar, configurar, preview, erro e warning com simbolos visuais.

## O que nao deve ser copiado

Nao copiar o modelo desktop WPF, menus globais pesados e comandos destrutivos como save/delete/import/export para a UI web atual.

Nao copiar o fluxo de edicao direta. O RagnaForge deve continuar baseado em `read-only`, `dry-run`, `diff-preview` e analise de dependencias.

Nao copiar reparos automaticos ou geracao direta de arquivos client-side pela UI. O SDE possui `RepairEngine`, comandos de repair e escrita de `.lua/.lub`; no RagnaForge, esses pontos devem ficar bloqueados na interface ate haver politica futura de apply/rollback.

Nao copiar a estrategia de decompilar `.lub` automaticamente. O RagnaForge ja decidiu bloquear bytecode e manipular apenas formatos textuais seguros.

Nao copiar campos tecnicos sem traducao. O SDE expÃµe muitos campos como editor de DB; o RagnaForge precisa apresentar esses dados como plano, risco, dependencia e diff.

Nao copiar assets/PNG/estilos do SDE sem licenca clara.

## Referencia tecnica util

### Abas genericas de database

O SDE usa `GDbTab`, `GDbTabWrapper`, `TabGenerator`, `GTabSettings` e `GTabsMaker` para gerar abas baseadas em metadados de database.

Adaptacao recomendada para RagnaForge:

- Criar no futuro um `PipelineWorkspaceLayout` web com tres regioes fixas: lista/propostas, editor de proposta e resultado/validacao.
- Criar configuracoes por modulo em vez de formularios avulsos: `ItemWorkspaceConfig`, `EquipmentWorkspaceConfig`, `MonsterWorkspaceConfig`.
- Manter a fonte dos campos no contrato da API, nao em metadados copiados do SDE.

### Server/client lado a lado

O SDE aproxima `Items`, `Items2`, `CItems`, `ClientResourceDb`, `MobAvail`, `MobSkills` e tabelas Lua/Lub.

Adaptacao recomendada para RagnaForge:

- Mostrar sempre um painel `Server-side`, um painel `Client-side`, um painel `Assets` e um painel `Diff`.
- Em itens/equipamentos, destacar explicitamente `ClientSidePlan`, `ClientSideMode`, `ExistingClientRegistration`, `ProposedClientRegistration` e `BytecodeBlocks`.
- Em NPCs, destacar `ClientIdentityPlan` e proveniencia do sprite.

### Validacao

O SDE separa validacao de tabela, recurso e client item. Essa divisao conversa diretamente com o RagnaForge.

Adaptacao recomendada para RagnaForge:

- Criar aba `Validacao` como centro read-only de riscos.
- Agrupar validacoes em `Server DB`, `Client-side`, `Assets`, `GRF`, `Mapas`, `Bytecode`, `Conflitos`.
- Mostrar severidade, entidade, arquivo, dependencia, fonte e proxima acao recomendada.
- Nao oferecer `Repair` automatico nesta fase.

### Preview e recursos

O SDE usa preview de item in-game, preview de viewId e explorador GRF.

Adaptacao recomendada para RagnaForge:

- Implementar no futuro previews web como paineis passivos.
- Primeiro preview: icone/item bitmap, recurso encontrado/ausente, sprite/source path e proveniencia.
- Preview de sprite/mapa deve ficar atras de pipelines seguros de leitura, sem copiar asset.

### Lua/Lub e identidades visuais

O SDE manipula `itemInfo`, `accessoryid`, `accname`, `weapontable`, `jobname`, `npcidentity` e tabelas correlatas.

Adaptacao recomendada para RagnaForge:

- Usar a lista de arquivos como referencia de cobertura, nao a implementacao.
- Manter deteccao `TextLua`, `TextLub`, `BinaryLub`, `LegacyTxt`, `Unknown`.
- Na UI, mostrar bytecode como bloqueio visivel, nao como diff editavel.

## Recomendacao por aba do RagnaForge

### Dashboard

Inspiracao SDE: paineis compactos e estado tecnico visivel.

Proposta:

- Cards compactos para API, rAthena, Patch, GRFs, client date, profile progressivo e riscos.
- Matrix de prontidao por modulo.
- Lista read-only de ultimos dry-runs quando houver historico local.
- Banner permanente: `ReadOnlyMode = true`, `ApplyEnabled = false`, `RollbackEnabled = false`.

### Itens

Inspiracao SDE: `Items` e `Client Items` separados, mas relacionados.

Proposta:

- Layout em tres regioes: grid/lista de propostas, formulario agrupado e painel de resultado.
- Secoes: Identificacao, Server DB, Client-side, Descricao, Resource/Sprite, Validacao, Diff.
- Painel de risco dedicado para `Unknown Item/Apple`.
- Diff server/client lado a lado quando existir.

### Equipamentos

Inspiracao SDE: item base + view/class/client tables.

Proposta:

- Deixar visualmente claro que equipamento e item especializado.
- Secoes: Item base, Equip restrictions, Visual registration, Assets, Client-side, Validation, Diff.
- Subpaineis para `Headgear/Accessory`, `Robe`, `Weapon` e `Shield restriction`.
- Indicadores fortes para `ViewID duplicado`, `simbolo inseguro`, `shield custom bloqueado` e `.lub bytecode`.

### NPCs

Inspiracao SDE: `jobname`, `npcidentity`, preview de viewId e lookup de sprite.

Proposta:

- Painel de coordenadas/mapa simples.
- Painel de sprite com status: padrao, Patch, GRF, ambiguo, ausente.
- Painel `ClientIdentityPlan` com arquivos detectados e bloqueios.
- Mostrar `--allow-server-only` apenas como informacao de CLI, sem botao na UI.

### Monstros

Inspiracao SDE: subpaineis especificos de mob DB, drops, MVP drops e skills.

Proposta:

- Layout por subabas internas: Dados base, Stats, Drops, MVP, Skills, Spawns, Validacoes, Diff.
- Drops e skills em grids editaveis apenas como proposta local, com linha por entrada.
- Spawns em tabela com mapa, quantidade, area, respawn e label.
- Readiness sempre visivel no topo do modulo.

### Mapas

Inspiracao SDE: mapcache editor e grids de mapa.

Proposta:

- Painel principal em arvore de dependencias: Required files, Resolved assets, Missing assets, Ambiguous assets, Cache plan, Config plan.
- Mostrar `.rsw/.gnd/.gat`, texturas, modelos, sons e efeitos como grupos separados.
- Destacar bloqueios de rename binario e `map_cache.dat`.
- Nao oferecer apply de mapa na UI.

### GRF / Assets

Inspiracao SDE: `MultiGrfExplorer` com lista, busca, encoding e preview.

Proposta:

- Lista de containers com contagens e estado de indice.
- Busca por nome/extensao e filtros por `.spr`, `.act`, `.bmp`, `.tga`, `.rsw`, `.gnd`, `.gat`, `.rsm`, `.wav`.
- Colunas de proveniencia: `LocalIndex`, `LiveScan`, `LiveScanFallback`, `ControlledGrfExtraction`.
- Preview passivo quando o backend disponibilizar imagem segura.

### Validacao

Inspiracao SDE: `ValidationDialog` com categorias e results.

Proposta:

- Dashboard de erros/warnings por categoria.
- Filtros: entidade, severidade, arquivo, origem, bloqueio.
- Alternancia entre lista estruturada e raw JSON/texto.
- Sem repair automatico.

### Historico / Relatorios

Inspiracao SDE: backups, debug tables e resultados raw/list view.

Proposta:

- Lista read-only de documentos e auditorias.
- Ultimos dry-runs/diff-previews quando houver endpoint seguro.
- Readiness matrix historica.
- Botao seguro apenas para exportar relatorio local do navegador, se aprovado depois.

### Seguranca / API

Inspiracao SDE: settings tecnicos acessiveis.

Proposta:

- Mostrar API URL, chave configurada ou ausente, status auth, CORS esperado, rate limit, correlationId e OpenAPI.
- Mostrar operacoes permitidas/bloqueadas com linguagem direta.
- Reforcar ausencia de apply/rollback.

## Componentes visuais recomendados

- `EntityGrid`: grid virtualizado de entidades/propostas com busca, filtros e badges.
- `PipelineWorkspaceLayout`: lista esquerda, editor central, resultado direito ou inferior.
- `FieldGroup`: secao compacta com titulo, descricao curta e campos relacionados.
- `DependencyTree`: arvore de dependencias com resolved/missing/ambiguous/blocked.
- `ValidationMatrix`: tabela de riscos com severidade e origem.
- `ServerClientSplitPanel`: comparacao server/client lado a lado.
- `ResourcePreviewPanel`: preview passivo de icone/sprite/resource.
- `ReadinessRibbon`: barra fixa do modulo com readiness, warnings, errors e correlationId.
- `DiffWorkbench`: diff viewer com agrupamento por server/client/assets.
- `BytecodeBlockPanel`: painel de bloqueio para `.lub` bytecode.

## Layout sugerido

Para modulos densos, usar layout operacional em vez de pagina longa:

```text
Header: modulo, modo seguro, readiness, actions seguras

Left rail:
  busca, filtros, presets, propostas recentes

Main:
  formulario agrupado por dominio

Right rail ou bottom panel:
  dry-run result, validation, dependencies, diff, raw JSON
```

Em telas menores, o right rail deve virar abas internas: `Resultado`, `Validacao`, `Dependencias`, `Diff`, `JSON`.

## Melhorias prioritarias para a UI atual

1. Reorganizar formularios por grupos de dominio em vez de grids longos de duas colunas.
2. Adicionar lista/grid de propostas ou entidades no lado esquerdo de Itens, Equipamentos, NPCs, Monstros e Mapas.
3. Criar subabas internas por modulo: `Formulario`, `Dry-run`, `Validacao`, `Dependencias`, `Diff`, `JSON`.
4. Melhorar `SafetyBanner` para mostrar operacoes permitidas e bloqueadas, nao apenas status booleano.
5. Evoluir `DiffViewer` para agrupar hunks por `server-side`, `client-side`, `assets`, `config`, `map_cache`.
6. Criar `DependencyTree` para mapa, assets, client-side e GRF.
7. Criar `ValidationMatrix` centralizada e reutilizada nas abas e na tela de Validacao.
8. Corrigir textos com encoding quebrado na UI atual antes de polir visualmente.
9. Substituir inputs livres por selects/toggles em campos com dominio conhecido, como tipo de item, categoria visual, formato client-side e severidade.
10. Adicionar presets de formulario seguros para casos comuns, sem apply.

## Riscos

- Risco legal/compliance: nao ha licenca local detectada no SDE; usar apenas como referencia ate autorizacao/licenca ser formalizada.
- Risco de UX: copiar o modelo desktop diretamente deixaria a UI web pesada e confusa.
- Risco operacional: o SDE e editor com escrita; o RagnaForge nesta fase e planejador seguro. A UI precisa evitar linguagem que sugira apply.
- Risco tecnico: SDE usa decompilacao `.lub`; RagnaForge deve continuar bloqueando bytecode.
- Risco de escopo: previews ricos podem puxar copia/extracao de assets. Manter preview como leitura controlada.
- Risco de performance: grids grandes e GRF inspect precisam virtualizacao e paginacao quando virarem dados reais.

## Proximos passos recomendados

1. Aprovar a direcao visual baseada em workspace de tres regioes: lista, formulario, resultado.
2. Fazer uma etapa pequena de UX sem novos endpoints: reorganizar Itens e Equipamentos primeiro.
3. Criar `DependencyTree`, `ValidationMatrix` e `DiffWorkbench` como componentes read-only.
4. Depois aplicar o mesmo padrao em NPCs, Monstros, Mapas e GRF/Assets.
5. Manter apply/rollback ausentes da UI ate uma politica futura separada.

## Documento complementar

A referencia visual/UX detalhada por aba foi separada em:

- `docs/2026-05-11_RAGNAROKSDE-UI-REFERENCE_v1.md`
