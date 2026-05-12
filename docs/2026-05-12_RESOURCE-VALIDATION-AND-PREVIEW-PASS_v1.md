# Resource Validation And Preview Pass

Data: 2026-05-12

## Resumo executivo

Esta rodada ampliou a validacao read-only de recursos e o preview passivo da interface administrativa sem criar escrita nova na API, no frontend, no rAthena, no Patch ou nas GRFs. O foco foi consolidar um centro unico de riscos para server-side, client-side e assets, reaproveitando `dry-run`, `diff-preview`, `discover`, `grf index` e `grf inspect`.

Nao foram criados endpoints de `apply` ou `rollback`.
Nao foram criados botoes de `apply` ou `rollback`.
Nao foi feita copia de assets.
Nao houve edicao de `.lub` bytecode.

## Escopo concluido

- Validacao read-only consolidada no frontend a partir de historico local e respostas seguras da API.
- `Validation Center` ampliado para categorias de risco inspiradas no RagnarokSDE.
- `PassiveAssetPreviewPanel` enriquecido com categoria, path esperado, origem, proveniencia e estado.
- `DependencyTree` reaproveitado como arvore consolidada de recursos.
- Bateria read-only de casos reais do servidor progressivo executada por HTTP local, sem `apply` e sem `rollback`.

## O que mudou tecnicamente

### Frontend

- Criado `frontend/src/features/shared/resourceValidation.ts`.
- `ValidationPage` agora agrega:
  - issues vivas de `status`, `config validate` e `discover`;
  - bloqueios de `ClientSidePlan`, `VisualClientSidePlan` e `ClientIdentityPlan`;
  - `AssetLookup`, `ItemAssetLookup`, `VisualAssetLookup` e `NpcSpriteLookup`;
  - `AssetPlans`, `DependencyScan.ReferencedAssets` e `MapCachePlan`;
  - warnings, errors, validation warnings e validation errors do historico local.
- `ValidationMatrix` passou a filtrar por:
  - severidade;
  - tag;
  - categoria;
  - origem;
  - entidade.
- `ValidationIssueTable` agora mostra tambem:
  - se o item bloqueia apply futuro;
  - raw JSON opcional por issue.
- `PassiveAssetPreviewPanel` agora mostra:
  - asset;
  - categoria/tipo;
  - origem/proveniencia;
  - path esperado;
  - estado consolidado;
  - placeholder explicito para preview visual futuro.

### API

Nao foi necessario criar endpoint novo de validacao. Os dados existentes dos endpoints seguros ja foram suficientes para a rodada:

- `GET /api/status`
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

## Validacoes read-only agora consolidadas

### Itens

- `item resource name`
- risco de `Unknown Item/Apple`
- bloqueios de client-side
- bloqueios por bytecode
- lookup de asset em Patch/GRF
- classificacao passiva de:
  - inventory BMP
  - collection BMP
  - drag `.act/.spr`
  - card illustration

### Equipamentos

- `ViewID` duplicado
- simbolo inseguro
- asset ausente
- asset ambiguo
- `shield restriction`
- client-side visual bloqueado
- lookup de `headgear`, `weapon`, `robe` e suporte passivo a `shield`

### NPCs

- sprite padrao
- sprite custom no Patch
- sprite custom em GRF
- sprite ambiguo
- `jobname`, `jobidentity`, `npcidentity`
- bloqueio por bytecode
- `ClientIdentityPlan`

### Monstros

- classe/sprite
- `mob_avail`
- drops com item inexistente
- skill invalida
- spawn em mapa inexistente
- mapa nao registrado

### Mapas

- `.rsw/.gnd/.gat`
- `map_cache.dat`
- `map_index`
- texturas, modelos, sons e efeitos
- dependencias ausentes
- dependencias ambiguas
- rename binario bloqueado

## Estados de recurso usados na UI

- `resolved`
- `missing`
- `ambiguous`
- `blocked`
- `read-only`
- `needs-copy-future`

### Proveniencia exibida

- `Patch`
- `LocalIndex`
- `LiveScan`
- `LiveScanFallback`
- `ControlledGrfExtraction`
- `Unknown`

## Bateria read-only de casos reais

## Itens

- `item-simple`: `200`, `CanApply = true`, `warnings = 5`, `errors = 2`
- `item-asset-patch`: `200`, `CanApply = true`, `warnings = 5`, `errors = 2`
- `item-asset-grf`: `200`, `CanApply = true`, `warnings = 5`, `errors = 2`
- `item-asset-missing`: `200`, `CanApply = true`, `warnings = 5`, `errors = 2`
- `item-diff-preview`: `200`, `DiffPreview`, `7 arquivos`

## Equipamentos

- `equipment-headgear`: `200`, `ApplyReadiness = Ready`, `CanApply = false`
- `equipment-robe`: `200`, `ApplyReadiness = Ready`, `CanApply = false`
- `equipment-weapon`: `200`, `ApplyReadiness = Ready`, `CanApply = false`
- `equipment-shield-custom-blocked`: `200`, `ApplyReadiness = Ready`, `CanApply = false`
- `equipment-diff-preview`: `200`, `7 arquivos`

## NPCs

- `npc-standard-sprite`: `200`, `ApplyReadiness = ReadyServerOnly`, `CanApply = false`
- `npc-custom-patch`: `200`, `ApplyReadiness = ReadyServerOnly`, `CanApply = false`
- `npc-custom-grf`: `200`, `ApplyReadiness = ReadyServerOnly`, `CanApply = false`
- `npc-identity-textual`: `200`, `ApplyReadiness = ReadyServerOnly`, `CanApply = false`
- `npc-diff-preview`: `200`, `2 arquivos`

## Monstros

- `monster-simple`: `200`, `ApplyReadiness = Ready`, `CanApply = true`
- `monster-drops`: `200`, `ApplyReadiness = Ready`, `CanApply = true`
- `monster-skills`: `200`, `ApplyReadiness = Ready`, `CanApply = true`
- `monster-invalid-item-skill-map`: `200`, `ApplyReadiness = Blocked`, `CanApply = false`, `errors = 4`
- `monster-diff-preview`: `200`, `4 arquivos`

## Mapas

- `map-existing`: `200`, `CanApply = false`, `warnings = 3`, `errors = 2`
- `map-new-grf-candidate`: `200`, `CanApply = false`, `warnings = 1`, `errors = 2`
- `map-ambiguous`: `200`, `CanApply = false`, `warnings = 1`, `errors = 2`
- `map-missing-dependency`: `200`, `CanApply = false`, `warnings = 3`, `errors = 2`
- `map-rename-blocked`: `200`, `CanApply = false`, `warnings = 3`, `errors = 2`
- `map-diff-preview`: `200`, `2 arquivos`

## GRF e discovery

- `discover`: `200`
- `grf index`: `200`
- `grf inspect data_0.grf`: `200`

### Ambiente real confirmado pela bateria

- rAthena: `E:\Ragnarok\Testes\rAthena_teste`
- Patch: `E:\Ragnarok\Testes\Patch_teste`
- GRFs: `E:\Ragnarok\Conteudo Ragnarok\GRF'S`
- GRF Editor: `C:\Program Files (x86)\GRF Editor`
- `discover` detectou `ClientDate = 2025-07-16`
- `discover` confirmou coexistencia de `TXT legado` e `itemInfo` textual no Patch atual

## O que o preview passivo faz agora

- mostra o nome do recurso;
- mostra o tipo/extensao;
- mostra a categoria;
- mostra o path encontrado e o path esperado quando diferem;
- mostra a origem e a proveniencia;
- mostra o estado consolidado;
- deixa explicito quando um recurso esta:
  - bloqueado por bytecode;
  - ausente;
  - ambiguo;
  - apenas em leitura;
  - dependente de copia futura segura.

## O que o preview passivo ainda nao faz

- nao renderiza icone real de item;
- nao renderiza sprite real de NPC/monstro;
- nao abre `.act/.spr`;
- nao extrai asset;
- nao copia asset para Patch;
- nao altera GRF;
- nao faz repair.

## Validation Center

Categorias expostas nesta rodada:

- `Server DB`
- `Client-side`
- `Assets`
- `GRF`
- `Mapas`
- `Bytecode`
- `Conflitos`
- `Unknown Item/Apple`
- `ViewID`
- `AegisName/ID`
- `Map cache`

Cada issue agora pode mostrar:

- severidade
- categoria
- entidade
- arquivo
- origem
- mensagem
- acao recomendada
- se bloqueia apply futuro
- raw JSON opcional

## Confirmacoes de seguranca

- Nenhum endpoint de `apply` ou `rollback` foi criado.
- Nenhum botao de `apply` ou `rollback` foi criado.
- Nenhuma chamada `/apply` ou `/rollback` foi adicionada ao cliente HTTP.
- Nenhum `--confirm APPLY` foi usado.
- Nenhum `--confirm ROLLBACK` foi usado.
- Nenhum arquivo de `rAthena`, `Patch` ou `GRF` foi alterado.

## Limitacoes restantes

- `grf inspect` ainda pode devolver resposta menos rica dependendo do estado do indice/cache do container analisado.
- O preview continua textual; o visual real de assets depende de endpoint seguro adicional ou leitura read-only mais especializada.
- A classificacao de recursos inspirada no RagnarokSDE e util para triagem, mas ainda nao substitui parser dedicado de formatos binarios como `.rsw/.gnd`.

## Proximos passos recomendados

1. Preview visual real read-only para icones e sprites, sem extracao nem copia.
2. Parser binario read-only dedicado para `.rsw/.gnd`.
3. Persistencia segura e confirmada de `ClientDate`.
4. Continuar discutindo `apply/rollback` apenas por politica e checklist, nao por implementacao.
