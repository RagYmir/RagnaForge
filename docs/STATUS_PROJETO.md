# STATUS_PROJETO

Atualizado: 2026-05-12
Estado: dry-run, diff-preview, apply e rollback protegidos por confirmacao explicita. API backend endurecida. Interface administrativa atualizada com Validation Center dinamico (badges dinâmicos e grupos de Itens, Equipamentos, NPCs, Monstros, Mapas) e preview visual real read-only seguro para BMP, PNG, JPG, JPEG, WEBP. Backend com 104/104 testes OK; frontend com 22/22 testes OK.

## Macro-etapa 2026-05-12 - Preview Visual Real Read-Only

- Implementado endpoint `POST /api/assets/preview` no backend com `ApiOperationGuard` (ReadOnly).
- Criado `AssetPreviewService` com suporte a extração em memória via `GrfAssemblyFileExtractor`.
- Suporte inicial a preview visual (DataURL/base64) para formatos BMP, PNG, JPG, JPEG e WEBP.
- Formatos complexos (SPR, ACT, RSM, GAT, etc.) mantidos como placeholders informativos no frontend.
- Segurança endurecida: bloqueio de path traversal, validação de extensão permitida, limite de 1MB por asset e limpeza imediata de arquivos temporários em bloco `finally`.
- `PassiveAssetPreviewPanel` atualizado para consumir a API e renderizar previews visuais reais com contenção via CSS.
- Backend atualizado para 104 testes de integração (incluindo suíte de segurança de assets); frontend com 22 testes OK.
- Branch `feature/asset-preview-readonly` pronta para merge (após PR).

## Macro-etapa 2026-05-12 - UI, produtividade local e validacao read-only

- Auditoria visual consolidada nas telas principais da interface administrativa.
- Presets locais seguros adicionados para item, equipamento, NPC, monstro e mapa.
- Historico local no navegador para `dry-run`, `diff-preview` e relatorios locais.
- Comparacao simples entre dois resultados locais com foco em readiness, warnings/errors e planos principais.
- Exportacao local em JSON e Markdown para resultado atual, historico e comparacao.
- `DiffWorkbench`, `ValidationMatrix` e `DependencyTree` ampliados para agrupar melhor diff, filtragem de validacao e estados de dependencia.
- Preview passivo de assets preparado em modo somente leitura, sem extracao, copia ou escrita externa.
- Auditoria anti-apply da UI concluida: sem botoes, sem rotas ativas, sem chamadas `/apply` ou `/rollback` no cliente HTTP.

## Macro-etapa 2026-05-12 - validacao de recursos e preview passivo

- `Validation Center` ampliado para consolidar riscos de server-side, client-side, assets, GRF, mapas, bytecode e conflitos a partir da API segura e do historico local.
- Camada `resourceValidation` criada no frontend para classificar issues e recursos em estados `resolved`, `missing`, `ambiguous`, `blocked`, `read-only` e `needs-copy-future`.
- `ValidationMatrix` ganhou filtros por severidade, tag, categoria, origem e entidade.
- `PassiveAssetPreviewPanel` ganhou categoria, path esperado, origem, proveniencia e placeholder explicito para preview visual futuro.
- Bateria read-only de casos reais executada para todos os tipos principais via CLI backend e frontend validado.
- Os badges no Validation Center deixaram de ser fixos e agora renderizam de forma dinâmica lendo diretamente a saúde de `Itens`, `Equipamentos`, `NPCs`, `Monstros` e `Mapas`.
- Nenhum endpoint novo de validacao foi necessario; a rodada reaproveitou endpoints existentes.
- Politica futura de `apply/rollback` documentada, sem implementacao em API ou UI.

## O que foi analisado

- Projeto atual em `<WORKSPACE_ROOT>`.
- Instalacao local do GRF Editor em `<GRF_EDITOR_PATH>`.
- Repositorio rAthena informado: `<RATHENA_PATH>`.
- Patch/client informado: `<PATCH_PATH>`.
- Repositorio de GRFs informado: `<GRF_REPOSITORY_PATH>`.
- Contexto de progressao informado: servidor progressivo por episodios, com updates mensais ate chegar futuramente ao Renewal.
- Metadados de `GRF Editor.exe` e `GrfCL.exe`.
- Configs `.exe.config`.
- Referencias .NET e recursos embutidos dos executaveis.
- Fontes upstream rAthena para item DB, mob DB, map config, map index e scripts.
- Codigo-fonte publico do `GrfCL` para confirmar sintaxe e comandos.

## Plano registrado 2026-05-11 - monster apply/rollback

- Reutilizar o padrao transacional ja criado para item e NPC.
- Criar modelos de dominio para log, resultado de apply e resultado de rollback de monstro.
- Criar `MonsterApplyService` com preflight, backup, escrita atomica, log, rollback automatico em falha e rollback manual por manifest.
- Restringir escrita aos alvos esperados do rAthena: `db/import` para `mob_db.yml`, `mob_avail.yml` e `mob_skill_db.txt`, e `npc` para loader/spawn custom.
- Bloquear conflitos antes de escrever: alvo fora das raizes permitidas, `create` em arquivo existente, append duplicado exato/normalizado/linha-ancora, ID/AegisName/spawn/loader ja presentes.
- Adicionar CLI `monster apply --confirm APPLY` e `monster rollback --confirm ROLLBACK --log <arquivo>`.
- Adicionar testes cobrindo apply+rollback completo e bloqueio de alvo fora das raizes permitidas.
- Atualizar `STATUS_PROJETO.md`, `PIPELINES.md`, `DEPENDENCIAS_RATHENA_PATCH.md` e criar/atualizar `APPLY_ROLLBACK.md`.

## Plano registrado 2026-05-11 - equipment apply/rollback

- Reutilizar o padrao transacional de `item` e `monster`, mas limitado aos alvos que o `equipment dry-run` ja propoe.
- Criar modelos de dominio para log, resultado de apply e resultado de rollback de equipamento.
- Criar `LegacyEquipmentApplyService` com preflight, backup, escrita atomica, log, rollback automatico em falha e rollback manual por manifest.
- Restringir escrita aos alvos esperados de equipamento: `rAthena/db/import` e `Patch/data`.
- Revalidar antes de escrever: colisao de ID/AegisName no `item_db`, colisao de ID nas tabelas TXT legado, simbolo/ViewID ja presentes em `accessoryid.lub`, `accname.lub`, `spriterobeid.lub`, `spriterobename.lub` ou `weapontable.lub`, alem dos conflitos estruturados de append/create.
- Adicionar CLI `equipment apply --confirm APPLY` e `equipment rollback --confirm ROLLBACK --log <arquivo>`.
- Adicionar testes cobrindo apply+rollback completo, bloqueio de ViewID introduzido depois do dry-run e bloqueio de alvo fora das raizes permitidas.
- Atualizar `STATUS_PROJETO.md`, `PIPELINES.md`, `DEPENDENCIAS_RATHENA_PATCH.md`, `APPLY_ROLLBACK.md`, `ROADMAP.md` e `DECISOES_TECNICAS.md`.

## Plano registrado 2026-05-11 - map apply/rollback

- Endurecer `map dry-run` para explicitar plano de assets do Patch, dependencia de `mapcache.exe` e limite de seguranca para nomes/binarios.
- Bloquear apply quando o trio `.rsw/.gnd/.gat` precisar de rename binario entre `MapName` e resource names, porque essa fase ainda nao reescreve os binarios.
- Criar modelos de dominio para log, resultado de apply e rollback de mapa, separados dos snapshots textuais de item/NPC/monstro porque mapa tambem mexe com binarios.
- Criar `MapApplyService` com backup, escrita atomica/copia segura, extracao controlada de GRF, rebuild de `map_cache.dat` em staging e rollback por manifest.
- Isolar a geracao de `map_cache.dat` em adaptador proprio para poder usar `mapcache.exe` real no futuro e fake builder nos testes.
- Restringir escrita aos alvos esperados: `rAthena/db/import`, `rAthena/conf` e `Patch/data`.
- Adicionar CLI `map apply --confirm APPLY` e `map rollback --confirm ROLLBACK --log <arquivo>`.
- Adicionar testes cobrindo dry-run com plano de assets, apply+rollback completo e bloqueio de alvo fora das raizes permitidas.
- Atualizar `STATUS_PROJETO.md`, `PIPELINES.md`, `DEPENDENCIAS_RATHENA_PATCH.md`, `APPLY_ROLLBACK.md`, `ROADMAP.md` e `DECISOES_TECNICAS.md`.

## Plano registrado 2026-05-11 - monster advanced + post-write validation

- Evoluir o dominio de monstro para aceitar drops multiplos, skills multiplas e spawns multiplos sem quebrar a compatibilidade com os comandos simples ja existentes.
- Validar drops por ID ou `AegisName` contra os bancos reais do rAthena antes do apply, bloqueando chance invalida, item ausente e duplicidade exata.
- Detectar e validar o formato local de `mob_skill_db.txt`, suportando as 19 colunas classicas com multiplas skills e bloqueando campos avancados ainda nao mapeados com erro claro.
- Permitir spawns mais completos em script custom seguro, incluindo label visivel, label de evento, area, respawn, mapa e multiplas entradas por monstro, sempre com validacao de mapa e coordenadas obvias.
- Adicionar validacao sintatica pos-write reaproveitavel para YAML, TXT de `mob_skill_db`, script rAthena e texto Lua, executada sobre o arquivo final montado em staging antes da substituicao definitiva.
- Integrar a validacao pos-write ao `monster apply`, com bloqueio da substituicao final quando o staging falhar e rollback automatico se algum erro ocorrer depois de iniciar a escrita real.
- Expandir a CLI, os testes e a documentacao para expor `Drops`, `Skills`, `Spawns`, `UnsupportedFields`, `ValidationWarnings`, `ValidationErrors`, `ApplyReadiness` e `PostWriteValidationPlan`.

## Plano registrado 2026-05-11 - NPC client identity seguro

- Detectar `jobname`, `jobidentity` e `npcidentity` reais no Patch por busca controlada, classificando cada alvo em `Missing`, `TextLua`, `TextLub`, `BinaryLub`, `LegacyTxt` ou `Unknown`.
- Tratar `.lub` textual como manipulavel, bloquear `.lub` bytecode/binario sem tentar decompilar ou converter.
- Evoluir o `npc dry-run` para expor um `NpcClientIdentityPlan` com proveniencia do sprite, registro existente, propostas seguras, bloqueios por bytecode/ambiguidade e readiness separado de server-side.
- Permitir gerar diff client-side somente quando os arquivos detectados forem textuais e o sprite custom estiver resolvido sem ambiguidade.
- Evoluir `npc apply` para suportar client identity textual com staging, validacao pos-write, backup, hash e rollback; manter bloqueio padrao quando a identidade client-side for necessaria mas insegura.
- Adicionar `--allow-server-only` como escape explicito para aplicar apenas o lado server-side quando o bloco client-side estiver pendente ou bloqueado.
- Atualizar `npc rollback` para restaurar tambem arquivos client-side textuais alterados e recusar rollback cego se o hash divergir do estado aplicado.
- Ampliar testes cobrindo arquivos textuais, bytecode bloqueado, sprite resolvido por Patch/GRF, diff seguro, apply server-only e rollback com hashes.

## O que foi encontrado no projeto atual

- Existe `.git`.
- Existe `docs/`.
- Os artefatos prematuros do MVP FastAPI/frontend ja foram removidos.
- O repositorio foi copiado para `<WORKSPACE_ROOT>`.

## O que foi criado nesta fase

- `docs/DEPENDENCIAS_RATHENA_PATCH.md`
- `docs/ANALISE_GRF_EDITOR.md`
- `docs/ARQUITETURA.md`
- `docs/PIPELINES.md`
- `docs/DECISOES_TECNICAS.md`
- `docs/ROADMAP.md`
- `docs/STATUS_PROJETO.md`
- `docs/ANALISE_REPOSITORIOS_LOCAIS.md`
- `docs/2026-05-06_SCANNERS-READONLY_v1.md`
- `docs/2026-05-07_MANIFEST-CONFIG-PROGRESSIVO_v1.md`
- `docs/2026-05-07_CACHE-GRF-INCREMENTAL_v1.md`
- `docs/2026-05-07_ITEM-DRYRUN-CLIENTDATE_v1.md`
- `docs/2026-05-07_GRF-ASSEMBLY-INDEX_v1.md`
- `docs/2026-05-07_ITEM-GRF-ASSET-LOOKUP_v1.md`
- `docs/2026-05-07_ITEM-DIFF-PREVIEW_v1.md`
- `docs/2026-05-07_EQUIPMENT-DRYRUN_v1.md`
- `docs/2026-05-07_EQUIPMENT-HARDENING_v1.md`
- `docs/2026-05-07_VISUAL-EQUIPMENT-THEMES_v1.md`
- `docs/2026-05-07_THEME-ASSET-LOOKUP_v1.md`
- `docs/2026-05-07_INDEXED-EXACT-ASSET-LOOKUP_v1.md`
- `docs/2026-05-07_GRF-LOOKUP-PROVENANCE_v1.md`
- `docs/2026-05-07_ITEM-APPLY-ROLLBACK_v1.md`
- `docs/2026-05-07_ENTITY-DIFF-PREVIEWS_v1.md`
- `docs/2026-05-07_SHIELD-VISUAL-PIPELINE_v1.md`
- `docs/2026-05-07_LUB-HANDLING_v1.md`
- `docs/2026-05-07_MAP-DEPENDENCY-SCAN_v1.md`
- `docs/2026-05-07_GRF-MAP-TEMP-EXTRACTION_v1.md`
- `docs/2026-05-07_ITEM-APPLY-AUDIT_v1.md`
- `docs/2026-05-07_MONSTER-NPC-VALIDATION_v1.md`
- `docs/2026-05-10_NPC-APPLY-ROLLBACK_v1.md`
- `docs/2026-05-11_MONSTER-APPLY-ROLLBACK_v1.md`
- `docs/2026-05-11_EQUIPMENT-APPLY-ROLLBACK_v1.md`
- `docs/2026-05-11_MAP-APPLY-ROLLBACK_v1.md`
- `docs/2026-05-11_MONSTER-ADVANCED-DROPS-SKILLS-SPAWNS_v1.md`
- `docs/2026-05-11_API-READONLY-DRYRUN-DIFF_v1.md`
- `docs/2026-05-11_API-HARDENING-AUTH-GUARDS_v1.md`
- `docs/2026-05-12_ADMIN-UI-SDE-VISUAL-PASS_v1.md`
- `docs/2026-05-12_ADMIN-UI-SDE-VISUAL-PASS-2_v1.md`
- `docs/APPLY_ROLLBACK.md`
- Estrutura .NET/C# inicial em `backend/`.
- Pastas locais de cache/manifest/log em `data/`.
- Manifest local criado em `data/manifests/repositories.local.json`.
- Manifest local de equipamentos visuais criado em `data/manifests/visual-equipment-themes.local.json`.
- Cache local de indice GRF criado em `data/cache/grf-repository.index.json`.
- Resolver inicial de item legado e dry-run de item.
- Spike funcional de indexacao interna de container GRF via `GRF.dll` embutida no `GrfCL.exe`.
- Cache local de indice interno salvo em `data/indexes/`.
- Lookup interno de asset GRF conectado ao `item dry-run` por flag opt-in.
- Diff preview especializado de item conectado ao `dry-run` e exposto na CLI por `item diff-preview`.
- `equipment dry-run` e `equipment diff-preview` implementados em cima do pipeline de item.
- `equipment dry-run` endurecido para validar `Locations`, `ViewID`, simbolos Lua, duplicidade visual, `weapontable.lub` e shield restrito por `View` embutido.
- Catalogo local de temas para equipamentos visuais implementado com escopo `equipment-visuals`.
- Matcher de temas conectado ao `equipment dry-run`, com suporte a sugestoes automaticas e selecao explicita por `--visual-theme`.
- Lookup assistido por tema conectado ao `equipment dry-run` para Patch e GRF quando o nome exato do sprite visual nao bate.
- Lookup assistido por tema agora prefere `data/indexes/*.index.json` antes do scan GRF ao vivo.
- Lookup exato de assets GRF agora tambem prefere `data/indexes/*.index.json` antes do fallback de scan ao vivo.
- Relatorio de dry-run agora expoe a proveniencia do lookup GRF com `Source`, `LocalIndexesLoaded` e `LiveContainersScanned`.
- Base inicial de `item apply` e `item rollback` implementada com backup/log no workspace.
- Diff previews iniciais adicionados para NPC, monstro e mapa.
- Lookup GRF ampliado para mapas via `.rsw`, `.gnd` e `.gat`.
- Detector de `.lub` textual vs bytecode adicionado para futuras leituras seguras.
- Scan profundo inicial de mapa adicionado para texturas, modelos, sons, efeitos e sprites quando `.rsw/.gnd` estao acessiveis como arquivos soltos.
- Scan profundo de mapa tambem quando `.rsw/.gnd` estao apenas em GRF, usando extracao temporaria controlada via `GRF.dll` do GRF Editor.
- Auditoria de `item apply` enriquecida com hashes, trilha de eventos, conflitos detalhados de append e log de bloqueio antes de qualquer escrita.
- `monster diff-preview` ampliado para `mob_skill_db.txt` e spawn com coordenadas/area mais completas.
- `npc dry-run` agora valida sprite padrao em `jobname.lub`, `jobidentity.lub` e `npcidentity.lub`, e marca sprites nao padrao para verificacao client-side adicional.
- Base inicial de `npc apply` e `npc rollback` implementada com backup/log no workspace e escrita limitada a `rAthena/npc`.
- Base inicial de `monster apply` e `monster rollback` implementada com backup/log no workspace, escrita limitada a `db/import` e `npc`, e rollback com validacao de hash.
- Base inicial de `equipment apply` e `equipment rollback` implementada com backup/log no workspace, escrita limitada a `db/import` e `Patch/data`, e rollback com validacao de hash.
- Base inicial de `map apply` e `map rollback` implementada com backup/log no workspace, copia segura de assets em `Patch/data`, rebuild de `db/import/map_cache.dat` em staging e rollback com validacao de hash.
- Pipeline de shield visual custom consolidado para redirecionar oficialmente para `robe`/`Costume_Garment` quando o client assim indicar pelas robe tables.
- Comandos CLI adicionados:
  - `config init`
  - `config validate`
  - `discover --config`
  - `grf index`
  - `grf inspect`
  - `item dry-run`
  - `item diff-preview`
  - `equipment apply`
  - `equipment rollback`
  - `equipment dry-run`
  - `equipment diff-preview`
  - `visual-equipment-themes init`
  - `visual-equipment-themes validate`
  - `visual-equipment-themes list`

## O que foi alterado

- Nenhum arquivo rAthena/Patch/GRF foi alterado.
- Codigo funcional read-only foi criado apenas no workspace.
- Escrita desta fase ficou restrita a `<WORKSPACE_ROOT>`.
- `data/manifests/repositories.local.json` guarda apenas caminhos locais e perfil progressivo; nao substitui rAthena/Patch/GRFs como fonte da verdade.
- `data/cache/grf-repository.index.json` guarda apenas metadados de containers e pode ser recriado.
- O dry-run de item gera apenas proposta em memoria/JSON; nao altera rAthena nem Patch/client.
- O comando `grf inspect` gera apenas indice JSON local em `data/indexes/`; nao altera GRFs originais.
- O `item dry-run` pode consultar GRFs em modo read-only quando `--asset-grf-container` ou `--scan-grf-assets` for usado.
- O `item diff-preview` so le arquivos alvo para montar hunk/contexto; continua sem alterar nada fora do workspace.
- `equipment dry-run` continua 100% em modo proposta; nesta fase ele cobre visual `headgear`, `accessory`, `robe`, `weapon` e `shield` restrito.
- `shield` agora aceita apenas `View` embutido `1..6`, sem append em `.lub`.
- tentativa de registrar visual custom de shield por `client-symbol`/`client-sprite` continua bloqueada.
- O dry-run de weapon propoe append em `weapontable.lub` com `Weapon_IDs`, `WeaponNameTable`, `Expansion_Weapon_IDs` e `WeaponHitWaveNameTable`.
- O dry-run de equipamento bloqueia simbolo inseguro, `ViewID` duplicado e location desconhecida antes de gerar proposta visual.
- O catalogo local de temas foi restringido a equipamentos visuais in-game; NPC, monstro e mapa ficaram fora do manifest.
- O nome canonico da CLI para esse catalogo passou a ser `visual-equipment-themes`, com arquivo padrao em `data/manifests/visual-equipment-themes.local.json`.
- O `equipment dry-run` agora anexa `VisualTheme` ao relatorio quando encontra manifest local valido, e aceita `--visual-theme` para validar a classificacao escolhida.
- O `equipment dry-run` agora usa `VisualTheme.LookupTokens` como ajuda opcional no lookup de assets visuais em Patch e GRF.
- Quando existir indice local do container, o `equipment dry-run` tenta o indice antes do fallback de scan GRF assistido por tema.
- O lookup exato de assets para `item dry-run` e `equipment dry-run` agora passa primeiro pelo wrapper `IndexedGrfAssetLookupService`, consultando `data/indexes` antes de tocar no scan de container ao vivo.
- `ItemDryRunReport` agora inclui `AssetLookup`, e `EquipmentDryRunReport` agora inclui `ItemAssetLookup` e `VisualAssetLookup`.
- `item apply` exige `--confirm APPLY` e escreve logs/backups somente em `data/logs/items` e `data/backups/items`.
- `item rollback` exige `--confirm ROLLBACK` e usa log de apply dentro do workspace.
- `equipment apply` exige `--confirm APPLY` e escreve logs/backups somente em `data/logs/equipment` e `data/backups/equipment`.
- `equipment rollback` exige `--confirm ROLLBACK`, valida hashes antes de restaurar e usa log de apply dentro do workspace.
- `npc`, `monster` e `map` ganharam `dry-run` e `diff-preview` na CLI.
- `npc apply` exige `--confirm APPLY` e escreve logs/backups somente em `data/logs/npcs` e `data/backups/npcs`.
- `npc rollback` exige `--confirm ROLLBACK` e usa log de apply dentro do workspace.
- `monster apply` exige `--confirm APPLY` e escreve logs/backups somente em `data/logs/monsters` e `data/backups/monsters`.
- `monster rollback` exige `--confirm ROLLBACK`, valida hashes antes de restaurar e usa log de apply dentro do workspace.
- `map apply` exige `--confirm APPLY`, escreve logs/backups somente em `data/logs/maps` e `data/backups/maps`, reaproveita GRF por extracao controlada e reconstrui `map_cache.dat` em staging antes de substituir o alvo final.
- `map rollback` exige `--confirm ROLLBACK`, valida hashes antes de restaurar e usa log de apply dentro do workspace.
- `map dry-run` agora tenta GRF para `.rsw`, `.gnd` e `.gat` quando o loose patch nao basta.
- `map dry-run` agora expoe `AssetPlans` e `MapCachePlan`, bloqueia rename binario entre `MapName` e resource names e normaliza referencias iniciadas por `\\` antes de montar alvos em `Patch/data`.
- `shield` custom agora emite warning quando o sprite informado ja aparece em robe tables do client.
- `map dry-run` agora tenta extrair referencias de texturas/modelos/sons/efeitos a partir de `.rsw/.gnd` soltos e mantem essas dependencias visiveis no relatorio.
- `map dry-run` agora tenta extrair temporariamente `.rsw/.gnd` de GRFs apenas para leitura, roda o scanner profundo e limpa os arquivos temporarios ao final.
- `item apply` agora grava hashes SHA-256, contagem de linhas, conflitos detectados e trilha de auditoria por arquivo/etapa.
- `item apply` passou a bloquear de forma estruturada `create` com alvo existente e `append` que ja apareca no destino por match exato, normalizado ou por linha-ancora.
- `monster diff-preview` agora inclui `mob_skill_db.txt` quando o dry-run recebe skill, e o spawn usa `spawn-x`, `spawn-y`, `spawn-area-x`, `spawn-area-y` e `spawn-label`.
- `npc dry-run` agora diferencia sprite padrao de sprite nao padrao e marca quando a validacao do client precisa continuar em assets custom/GRF.
- `npc dry-run` agora preserva no `DetectionSource` quando um sprite custom foi confirmado por GRF via `local-index`, `live-scan-fallback` ou `live-scan`.
- `visual-themes.local.json` permanece apenas como artefato anterior/local, sem virar nome canonico do fluxo novo.
- O servidor local do MVP anterior foi encerrado.
- Com autorizacao do usuario, os artefatos do MVP anterior foram removidos:
  - `backend/`
  - `frontend/`
  - `data/`
  - `requirements.txt`
  - `.gitignore`
  - `STATUS_PROJETO.md` da raiz
  - `docs/2026-05-06_api-interface-conteudo_v1.md`

## Riscos conhecidos

- O GRF Editor possui origem publica em GitHub sem licenca detectada pela verificacao automatica, mas o usuario informou autorizacao direta do criador para uso da base tecnica e das DLLs necessarias.
- `GrfCL.exe` nao expoe ajuda por `--help` ou `/?`; comandos precisam de spike controlado.
- `GrfCL.exe` tem sintaxe parcialmente confirmada, mas em PowerShell algumas operacoes podem emitir excecao textual mesmo quando concluem a acao principal.
- O lookup por `GRF.dll` esta ligado ao resolver de item, mas ainda opera como opt-in para evitar varrer GRFs grandes sem escolha explicita.
- rAthena local parece sem flags Renewal ativas agora, mas isso representa apenas o episodio atual do servidor progressivo.
- Patch local mistura tabelas TXT antigas com LUB; resolver nao pode assumir `ItemInfo.lua`.
- Nao foi encontrada tabela client-side dedicada de shield visual nos datainfo locais; por isso shield ficou em modo restrito, sem registro visual novo.
- Weapon esta suportado por append simples em `weapontable.lub`, mas ainda depende de teste visual real dentro do client antes de virar apply.
- O catalogo de equipamentos visuais agora influencia o relatorio do `equipment dry-run` e o lookup assistido, mas ainda nao altera proposta de arquivos nem decide apply automaticamente.
- O indice local de container pode estar truncado; nesses casos o sistema ainda pode cair para scan GRF ao vivo como fallback.
- `use_grf: no` no map-server torna `map_cache.dat` obrigatorio em pipeline de mapas.
- O scan profundo de mapa agora cobre Patch solto e GRF por extracao temporaria, mas ainda usa leitura de strings; parser binario dedicado para `.rsw/.gnd` continua pendente para maior precisao.
- `npc` ainda nao injeta automaticamente tabelas/client assets para sprite custom; nesta fase ele valida, classifica risco e deixa o apply bloqueado para esse trecho.
- `monster` agora cobre drops normais/MVP, multiplas skills e multiplos spawns, mas ainda nao modela quantity por drop, variancia de respawn, handlers de evento gerados automaticamente ou formatos alternativos alem do `mob_skill_db.txt` classico.
- `equipment` ainda nao copia sprites/ACT para o Patch e nao manipula `.lub` bytecode nao legivel.
- `map` ainda usa leitura baseada em strings internas de `.rsw/.gnd`; parser binario dedicado continua como proxima camada de hardening.
- `map` agora bloqueia lookup GRF ambiguo de dependencias referenciadas, mas isso pode reduzir bastante `CanApply` em mapas grandes ate existir parser/lookup mais preciso por caminho.
- `map` ainda nao gera automaticamente warps, mapflags, NPCs iniciais ou spawns acoplados ao mesmo apply.
- `item apply` e `item rollback` estao implementados, mas ainda nao foram executados contra os repositórios reais porque a confirmação explícita não foi fornecida.
- Artefatos do MVP anterior foram removidos; a stack final ainda precisa ser criada do zero apos aprovacao da arquitetura.
- O SDK local criou projetos `net10.0`; se for necessario mirar LTS, sera preciso ajustar templates/SDK antes.

## Validacao executada

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- `cmd /c npm.cmd run build`
- `cmd /c npm.cmd run test`
- Smoke HTTP seguro local da Admin UI/API em portas livres, confirmando `/health`, `/api/status`, frontend servido localmente, `item dry-run`, `npc dry-run`, `ProblemDetails` estruturado em erro e `POST /api/items/apply` = `404`.
- `dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- discover ... --max-grf-containers 20`
- `dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- config init ... --out data\manifests\repositories.local.json`
- `dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- config validate --config data\manifests\repositories.local.json`
- `dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- discover --config data\manifests\repositories.local.json --max-grf-containers 20`
- Smoke test de seguranca: `config init --out outside.json` foi recusado e nao criou arquivo fora de `data/manifests`.
- `dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- grf index --config data\manifests\repositories.local.json --cache data\cache\grf-repository.index.json --max-containers 20`
- Smoke test de seguranca: `grf index --cache outside-index.json` foi recusado e nao criou arquivo fora de `data/cache`.
- `dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- item dry-run --config data\manifests\repositories.local.json --aegis RF_Test_Item --name "RagnaForge Test Item" --resource RF_Test_Item --type Etc --buy 10 --sell 5 --weight 10 --identified-desc "Linha 1|Linha 2"`
- `GrfCL.exe -version`
- Laboratorio temporario controlado com `GrfCL.exe -makeGrf`, `-open -grfInfo` e `-extractGrf`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj` com 14/14 testes OK
- `dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- grf inspect --config data\manifests\repositories.local.json --container "<GRF_REPOSITORY_PATH>\data_0.grf" --cache data\indexes\data_0.index.json --limit 25`
- Smoke de seguranca: `grf inspect --cache outside-index.json` foi recusado e nao criou arquivo fora de `data/indexes`.
- Smoke de caminho relativo: `grf inspect --config data\manifests\repositories.local.json --container data_0.grf --limit 3 --no-save`
- Smoke real de item com asset dentro de GRF: `item dry-run --resource c_rabbit_winged_robe --asset-grf-container data_0.grf` encontrou 4 candidatos dentro de `data_0.grf`.
- Smoke real de diff: `item diff-preview --aegis RF_Test_Item --name "RagnaForge Test Item"` retornou 7 hunks com contexto real dos arquivos alvo.
- Smoke real de diff com asset em GRF: `item diff-preview --resource c_rabbit_winged_robe --asset-grf-container data_0.grf` retornou 7 hunks e manteve `CanApply = true`.
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj` com 16/16 testes OK.
- Smoke real de equipamento robe: `equipment dry-run --resource c_loli_ruri_moon --visual-category robe --asset-grf-container data_0.grf` retornou `CanApply = true` e `10` propostas.
- Smoke real de diff de equipamento: `equipment diff-preview ...` retornou `10` hunks com `spriterobeid.lub` e `spriterobename.lub`.
- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos.
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj` com 20/20 testes OK.
- Smoke real de weapon: `equipment dry-run --visual-category weapon --weapon-base-type SWORD ...` retornou `CanApply = true` e 8 propostas em memoria.
- Smoke real de diff de weapon: `equipment diff-preview ...` retornou 8 hunks, incluindo append em `weapontable.lub`.
- Smoke real de shield restrito: `equipment dry-run --visual-category shield --view 3 ...` retornou `CanApply = true` e 7 propostas, sem arquivos `.lub`.
- Smoke real de diff de shield restrito: `equipment diff-preview ...` retornou 7 hunks apenas para server-side e tabelas de item legado.
- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos apos consolidar o catalogo `equipment-visuals`.
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj` com 22/22 testes OK.
- `dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- visual-equipment-themes init --out data\manifests\visual-equipment-themes.local.json --force`
- `dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- visual-equipment-themes validate --config data\manifests\visual-equipment-themes.local.json`
- `dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- visual-equipment-themes list --config data\manifests\visual-equipment-themes.local.json`
- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos apos integrar o matcher de temas ao `equipment dry-run`.
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj` com 24/24 testes OK.
- Smoke real de equipamento com tema explicito: `equipment dry-run ... --visual-theme fofo` retornou `CanApply = true` e `VisualTheme.SelectedTheme = fofo`.
- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos apos integrar lookup assistido por tema.
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj` com 26/26 testes OK.
- Smoke real de tema assistindo lookup visual: `equipment dry-run ... --visual-theme fofo --asset-grf-container data_0.grf` retornou `CanApply = true`, `LookupTokens = ["rabbit"]` e candidatos assistidos em Patch/GRF.
- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos apos priorizar indice local no lookup assistido.
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj` com 27/27 testes OK.
- `dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- grf inspect --config data\manifests\repositories.local.json --container "<WORKSPACE_ROOT>\tmp\grf-smoke\sample.grf" --limit 10 --force` gerou `data\indexes\sample-ee82064d1a8f.index.json` com `sample.act`, `sample.spr` e `grid.tga`.
- Smoke real de item com indice local dedicado: `item dry-run --resource sample --asset-grf-container "<WORKSPACE_ROOT>\tmp\grf-smoke\sample.grf"` retornou `CanApply = true` com 2 candidatos GRF.
- Smoke real de equipamento weapon com indice local dedicado: `equipment dry-run --type Weapon --resource sample --visual-category weapon --client-symbol WEAPONTYPE_SAMPLE_INDEX --client-sprite sample --asset-grf-container "<WORKSPACE_ROOT>\tmp\grf-smoke\sample.grf"` retornou `CanApply = true` com append proposto em `weapontable.lub`.
- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos apos consolidar o lookup exato index-first.
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj` com 29/29 testes OK.
- Smoke real de item com proveniencia: `item dry-run --resource sample --asset-grf-container "<WORKSPACE_ROOT>\tmp\grf-smoke\sample.grf"` retornou `AssetLookup.Source = LocalIndex`.
- Smoke real de equipamento weapon com proveniencia: `equipment dry-run --type Weapon --resource sample ... --asset-grf-container "<WORKSPACE_ROOT>\tmp\grf-smoke\sample.grf"` retornou `ItemAssetLookup.Source = LocalIndex` e `VisualAssetLookup.Source = LocalIndex`.
- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos apos expor proveniencia de lookup no relatorio.
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj` com 30/30 testes OK.
- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos apos adicionar apply/rollback de item, diff previews de NPC/monstro/mapa e detector de `.lub`.
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj` com 36/36 testes OK.
- Smoke real de `npc diff-preview` em `prontera` retornou 2 hunks: loader em `npc/scripts_custom.conf` e script novo em `npc/custom`.
- Smoke real de `monster diff-preview` em `prontera` retornou 4 hunks: loader, `mob_db.yml`, `mob_avail.yml` e script de spawn.
- Smoke real de `map dry-run --map-name prontera --asset-grf-container data_0.grf` encontrou `prontera.rsw`, `prontera.gnd` e `prontera.gat` por `LiveScanFallback` e executou scan profundo via `ControlledGrfExtraction`.
- Smoke de segurança: `item apply` sem `--confirm APPLY` foi recusado antes de qualquer escrita.
- Smoke real de shield hint: `equipment dry-run` com `C_Lord_Of_Death_Shield` apontou `spriterobename.lub` como robe-table hint.
- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos apos endurecer e aplicar o pipeline de mapa.
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj` com 52/52 testes OK.
- Smoke real resumido de mapa: `map dry-run --map-name prontera --asset-grf-container data_0.grf` retornou `CanApply = false` porque `prontera` ja existe e porque `18` dependencias GRF ficaram ambiguas, com `AssetPlans = 28`, `NeedsCopy = 10` e `MapCacheTool = true`.
- Smoke CLI de seguranca: `map apply` sem `--confirm APPLY` retornou exit code `2` e nao iniciou escrita externa.

## Proximos passos

1. Cobertura avancada de monstro para drops, skills e spawns/eventos mais complexos.
2. Estrategia segura para identidades client-side de NPC custom quando houver necessidade de editar `jobname/jobidentity/npcidentity`.
3. Client-side avancado para `itemInfo`, `jobname`, `jobidentity`, `npcidentity` e `.lub` somente quando houver estrategia segura.
4. Validacao sintatica pos-write de YAML/TXT/Lua antes do apply real em repositorios externos.
5. API backend somente depois de consolidar esses blocos.

## Decisoes pendentes

- Nenhuma pendencia aberta sobre politica de reuso do GRF Editor; a autorizacao direta permite avancar com integracao encapsulada.

## Atualizacao 2026-05-07 - rodada atual

### Entregas concluidas

- `map dry-run` agora faz scan profundo de dependencias de mapa quando `.rsw/.gnd` estao acessiveis como arquivos soltos, expondo referencias de texturas, modelos, sons, efeitos e sprites no relatorio.
- `item apply` ganhou auditoria mais rica com hashes SHA-256, contagem de linhas, conflitos estruturados de append/create e trilha de eventos.
- `monster diff-preview` agora inclui `mob_skill_db.txt` quando o dry-run recebe skill, e o spawn aceita coordenadas e area mais completas.
- `npc dry-run` agora distingue sprite padrao de sprite nao padrao e exige verificacao client-side adicional quando necessario.
- Shield visual custom fica oficialmente redirecionado para `robe`/`Costume_Garment` quando o client ja referencia esse visual nas robe tables.

### Validacao desta rodada

- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos.
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj` com 40/40 testes OK.
- Smoke real de mapa solto: `map dry-run --map-name 1@colo` retornou `DeepScan = true`, `TextureRefs = 11`, `ModelRefs = 40`.
- Smoke real de monstro com skill/spawn completo: `monster diff-preview ... --skill-id 175 --spawn-x 120 --spawn-y 140 --spawn-area-x 8 --spawn-area-y 6` retornou 5 arquivos, incluindo `mob_skill_db.txt`.
- Smoke real de NPC custom nao padrao: `npc dry-run ... --sprite CustomGuide` retornou `RequiresAdditionalClientValidation = true`.
- Smoke real de decisao de shield: `equipment dry-run ... --visual-category shield --client-sprite C_Lord_Of_Death_Shield` retornou bloqueio com redirecionamento para `robe/Costume_Garment`.

## Atualizacao 2026-05-10 - NPC apply/rollback

### Entregas concluidas

- `NpcApplyService` criado com preflight, backup, escrita atomica, log, conflito estruturado e rollback.
- `npc apply` e `npc rollback` adicionados na CLI.
- Escrita de NPC fica limitada a `rAthena/npc`; qualquer alvo fora dessa raiz e bloqueado antes de escrever.
- Rollback restaura `npc/scripts_custom.conf` e remove scripts criados pelo apply.

### Validacao desta rodada

- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos.
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj` com 42/42 testes OK.
- Smoke CLI de seguranca: `npc apply` sem `--confirm APPLY` retornou exit code `2` e nao criou novos logs.

### Proximos passos atualizados

1. Evoluir `npc` para lookup opcional de sprite custom via GRF quando o Patch solto nao bastar.
2. Implementar `monster apply/rollback` transacional cobrindo `mob_db.yml`, `mob_avail.yml`, `mob_skill_db.txt` e spawn.
3. Cobrir mais colunas de `mob_skill_db.txt` e formatos de spawn/evento no pipeline de monstro.
4. Preparar apply/rollback de mapa somente depois de parser/validacao mais forte para `.rsw/.gnd/.gat`.
5. Avancar na indexacao GRF por categorias adicionais para reduzir fallback ao scan ao vivo.

## Atualizacao 2026-05-11 - monster apply/rollback

### Entregas concluidas

- `MonsterApplyService` criado com preflight, backup, escrita atomica, validacao de hash pos-write, log, rollback automatico em falha e rollback manual.
- `monster apply` e `monster rollback` adicionados na CLI.
- Escrita de monstro fica limitada a `rAthena/db/import` e `rAthena/npc`.
- Apply cobre `mob_db.yml`, `mob_avail.yml`, `mob_skill_db.txt`, loader em `npc/scripts_custom.conf` e script custom de spawn.
- Rollback valida se os arquivos ainda batem com o SHA aplicado antes de restaurar backups ou remover arquivos criados.

### Validacao desta rodada

- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos.
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj` com 45/45 testes OK.
- Smoke CLI de seguranca: `monster apply` sem `--confirm APPLY` retornou exit code `2` e nao criou novos logs.

### Proximos passos atualizados

1. Implementar `equipment apply/rollback` para item DB, tabelas client-side e datainfo visual.
2. Evoluir `npc` para lookup opcional de sprite custom via GRF quando o Patch solto nao bastar.
3. Cobrir mais colunas de `mob_skill_db.txt`, drops e formatos avancados de spawn/evento no pipeline de monstro.
4. Preparar apply/rollback de mapa somente depois de parser/validacao mais forte para `.rsw/.gnd/.gat`.
5. Avancar na indexacao GRF por categorias adicionais para reduzir fallback ao scan ao vivo.

## Atualizacao 2026-05-11 - equipment apply/rollback e NPC GRF lookup

### Entregas concluidas

- `LegacyEquipmentApplyService` criado com preflight, backup, escrita atomica, validacao de hash pos-write, log, rollback automatico em falha e rollback manual.
- `equipment apply` e `equipment rollback` adicionados na CLI.
- Escrita de equipamento fica limitada a `rAthena/db/import` e `Patch/data`.
- Apply cobre `item_db.yml`, tabelas TXT legado e datainfo visual atualmente suportado (`accessoryid.lub`, `accname.lub`, `spriterobeid.lub`, `spriterobename.lub`, `weapontable.lub`).
- Rollback valida se os arquivos ainda batem com o SHA aplicado antes de restaurar backups ou remover arquivos criados.
- `npc dry-run` agora preserva a proveniencia do match GRF em `DetectionSource` quando um sprite custom e resolvido por `local-index`, `live-scan-fallback` ou `live-scan`.

### Validacao desta rodada

- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos.
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj` com 49/49 testes OK.
- Smoke CLI de seguranca: `equipment apply` sem `--confirm APPLY` retornou exit code `2` e nao criou novos logs.

### Proximos passos atualizados

1. Hardening de mapa antes de `map apply/rollback`.
2. Cobertura avancada de monstro para drops, skills e spawns/eventos mais complexos.
3. Estrategia segura para identidades client-side de NPC custom quando houver necessidade de editar `jobname/jobidentity/npcidentity`.

## Atualizacao 2026-05-11 - map apply/rollback

### Entregas concluidas

- `MapDryRunReport` agora expoe `AssetPlans` e `MapCachePlan`.
- `map dry-run` passou a bloquear rename binario entre `MapName` e resource names, evitando copy inseguro de `.rsw/.gnd/.gat`.
- `MapApplyService` criado com preflight, backup, escrita atomica/copia segura, extracao controlada de assets GRF, rebuild de `map_cache.dat` em staging, log, rollback automatico em falha e rollback manual.
- `RathenaMapCacheBuilder` criado para encapsular `mapcache.exe`, gerar listas temporarias de GRF/mapa e validar a saida por leitura do cache.
- `map apply` e `map rollback` adicionados na CLI.
- Escrita de mapa fica limitada a `rAthena/db/import`, `rAthena/conf` e `Patch/data`.

### Validacao desta rodada

- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos.
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj` com 52/52 testes OK.
- Smoke real resumido: `map dry-run --map-name prontera --asset-grf-container data_0.grf` retornou `CanApply = false` porque o mapa ja existe e porque `18` dependencias GRF ficaram ambiguas, com `AssetPlans = 28`, `NeedsCopy = 10` e `MapCacheTool = true`.
- Smoke CLI de seguranca: `map apply` sem `--confirm APPLY` retornou exit code `2`.

### Proximos passos atualizados

1. Cobrir drops/skills/spawns avancados de monstro.
2. Fechar estrategia segura para identidades client-side de NPC custom.
3. Ampliar deteccao client-side (`itemInfo`, `jobname`, `jobidentity`, `npcidentity`, `.lub`) antes da API.

## Atualizacao 2026-05-11 - monster advanced + post-write validation

### Entregas concluidas

- `MonsterDryRunReport` agora expoe `Drops`, `Skills`, `Spawns`, `UnsupportedFields`, `ValidationWarnings`, `ValidationErrors`, `ApplyReadiness` e `PostWriteValidationPlan`.
- `MonsterDryRunService` agora valida drops contra `db/item_db.yml`, `db/import/item_db.yml`, `db/pre-re/item_db*.yml` e `db/re/item_db*.yml`.
- `MonsterDryRunService` agora valida skill IDs contra `skill_db.yml` local e suporta multiplas linhas no `mob_skill_db.txt` classico de 19 colunas.
- `monster dry-run` e `monster diff-preview` agora suportam multiplos drops, MVP drops, multiplas skills e multiplos spawns via `--drops`, `--skills` e `--spawns`.
- `MonsterApplyService` agora monta o arquivo final em staging dentro do backup da operacao, executa validacao sintatica antes da substituicao final e grava o resultado no log de apply.
- Validadores reaproveitaveis adicionados: `YamlSyntaxValidator`, `RathenaTxtValidator`, `RathenaScriptValidator`, `LuaTextValidator` e `ApplyPostWriteValidator`.
- `monster apply` agora bloqueia substituicao final quando o staging falha e continua tentando rollback automatico se algum erro ocorrer depois do inicio da escrita real.

### Validacao desta rodada

- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos.
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj` com 63/63 testes OK.
- Smoke real seguro: `monster dry-run --config data\\manifests\\repositories.local.json --aegis RF_ADV_SMOKE ... --drops \"item=Apple,chance=5000;item=Jellopy,chance=1000,mvp=true\" --skills \"id=175,...;id=176,...\" --spawns \"map=prontera,...;map=geffen,...\"` retornou `CanApply = true`, `ApplyReadiness = Ready`, `Drops = 2`, `Skills = 2`, `Spawns = 3`.
- Smoke CLI de seguranca: `monster apply` sem `--confirm APPLY` retornou exit code `2`.
- Smoke CLI de seguranca: `monster rollback` sem `--confirm ROLLBACK` retornou exit code `2`.

### Proximos passos atualizados

1. Fechar estrategia segura para identidades client-side de NPC custom (`jobname`, `jobidentity`, `npcidentity`).
2. Avancar na deteccao/manipulacao segura de client-side textual (`itemInfo`, `jobname`, `npcidentity`) sem tocar `.lub` bytecode sem estrategia segura.
3. So depois disso seguir para API backend e interface administrativa.

## Atualizacao 2026-05-07 - extracao temporaria de mapa via GRF

### Entregas concluidas

- Adicionado `GrfAssemblyFileExtractor`, adaptador read-only que usa `GRF.dll` do GRF Editor para extrair entradas selecionadas para uma raiz temporaria controlada.
- `map dry-run` agora usa esse adaptador quando `.rsw/.gnd` nao existem soltos no Patch, mas foram encontrados em GRF.
- O relatorio de mapa expoe `DependencyScan.Source = ControlledGrfExtraction`, mantendo separada a origem do match GRF e a origem do scan profundo.
- Arquivos temporarios de scan sao limpos no fim da operacao; GRFs originais nao sao alteradas.

### Validacao desta rodada

- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos.
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj` com 40/40 testes OK.
- Smoke real: `map dry-run --config data\\manifests\\repositories.local.json --map-name prontera --asset-grf-container data_0.grf` retornou `Source = ControlledGrfExtraction`, `DeepScan = true`, `TextureRefs = 9`, `ModelRefs = 10`, `SoundRefs = 6` e `TempEntries = 0`.

## Atualizacao 2026-05-11 - NPC client identity seguro

### Entregas concluidas

- `NpcClientIdentityPlanner` criado para detectar `jobname`, `jobidentity` e `npcidentity`, classificar formato e planejar registros seguros.
- `NpcDryRunReport` agora expoe `SpriteResolution`, `ClientIdentityPlan`, `ServerCanApply`, `ApplyReadiness`, `BytecodeBlocks`, `RequiredClientFiles`, `ExistingClientRegistration`, `ProposedClientRegistration` e `PostWriteValidationPlan`.
- `npc diff-preview` mostra hunks client-side apenas para arquivos textuais.
- `npc apply` revalida server-side e client-side, monta staging completo, executa validacao pos-write e aplica `jobname`, `jobidentity` e `npcidentity` quando forem textuais.
- `npc apply` suporta `--allow-server-only` como escape explicito quando o lado server estiver seguro e o client-side estiver bloqueado.
- `npc rollback` restaura arquivos client-side textuais alterados, validando SHA-256 antes da restauracao.

### Validacao desta rodada

- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos.
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj` com 71/71 testes OK.
- Smoke real seguro: `npc dry-run --config data\\manifests\\repositories.local.json --name "RagnaForge Guide" --map prontera --x 150 --y 180 --dir 2 --sprite 4_M_JOB_BLACKSMITH` retornou `CanApply = true`, `ApplyReadiness = Ready`, `jobname.lub = TextLub`, `jobidentity.lub = TextLub` e `npcidentity.lub = TextLub`.
- Smoke CLI de seguranca: `npc apply` sem `--confirm APPLY` foi recusado antes de escrita real.

### Proximos passos atualizados

1. Fechar client-side avancado para `itemInfo` e clients hibridos.
2. Consolidar estrategia geral de `.lua/.lub/.txt`.
3. So depois disso seguir para API backend e interface administrativa.

## Atualizacao 2026-05-11 - client-side itemInfo e clients hibridos

### Entregas concluidas

- Criado `ClientSidePlan` comum para item/equipamento, cobrindo modo, arquivos detectados, formatos, bytecode bloqueado, registros existentes/propostos, targets de apply/rollback e plano de validacao pos-write.
- Criado `ClientSidePlanner` para detectar `itemInfo.lua/lub`, tabelas TXT legadas e datainfo visual textual.
- `item dry-run` agora expoe `ClientSidePlan`, `ClientSideMode`, `BytecodeBlocks`, `ExistingClientRegistration`, `ProposedClientRegistration` e `PostWriteValidationPlan`.
- `equipment dry-run` agora expoe `ClientSidePlan` e `VisualClientSidePlan`.
- `.lub` bytecode em `itemInfo` ou datainfo visual bloqueia hunk/edit/apply; `.lub` textual continua permitido com staging.
- Clients hibridos sao detectados. No Patch atual, o modo foi `Hybrid`: `system/iteminfo_true.lub` textual existe junto com TXT legado completo.
- Para o Patch atual, a estrategia segura desta rodada manteve TXT legado como alvo de escrita e `itemInfo` como leitura/validacao read-only.
- `item apply` e `equipment apply` agora executam validacao pos-write em staging antes da escrita final.
- `item rollback` passou a bloquear rollback se arquivos client-side mudarem manualmente depois do apply, alinhando com os demais pipelines.

### Validacao desta rodada

- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos.
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj` com 81/81 testes OK.
- Smoke real seguro de item: `item dry-run ... RF_ClientSide_Smoke` retornou `ClientSideMode = Hybrid`, `itemInfo:TextLub`, tabelas TXT como `LegacyTxt`, `CanApply = true` e 7 propostas.
- Smoke real seguro de equipamento: `equipment dry-run ... RF_ClientSide_Visual` retornou `ClientSideMode = Hybrid`, `VisualClientSidePlan` com `accessoryid.lub = TextLub` e `accname.lub = TextLub`.
- Smokes complementares de equipamento confirmaram `spriterobeid.lub`, `spriterobename.lub` e `weapontable.lub` como `TextLub`.
- Smoke CLI de seguranca: `item apply` sem `--confirm APPLY` foi recusado antes de escrita real.

### Proximos passos atualizados

1. Auditoria pre-producao de dry-run/diff-preview para item, equipamento, NPC, monstro e mapa.
2. Depois da auditoria, API backend.
3. Depois da API, interface administrativa.

## Atualizacao 2026-05-11 - auditoria pre-producao

### Entregas concluidas

- Criado `docs/2026-05-11_PRE-PRODUCTION-AUDIT_v1.md`.
- Auditoria read-only executada para configuracao, GRF/indexacao, item, equipamento, NPC, monstro, mapa, client-side, guards de apply/rollback e prontidao para API.
- `grf index` completo com limite 200 encontrou 38 containers e nao atingiu limite.
- `grf inspect data_0.grf` confirmou 131185 entradas e extensoes centrais: `.bmp`, `.act`, `.spr`, `.tga`, `.rsm`, `.wav`, `.str`, `.pal`, `.rsm2`, `.gat`, `.rsw`, `.gnd`.
- Item auditado com casos de Etc simples, asset no Patch, asset em GRF, client hibrido, conflito de ID/AegisName e asset ausente.
- Equipamento auditado com headgear, robe, weapon, shield restrito, shield custom bloqueado, ViewID duplicado e simbolo inseguro.
- NPC auditado com sprite padrao, sprite ja registrado, sprite custom solto com identidade nova e sprite resolvido apenas em GRF.
- Monstro auditado com caso simples, `mob_avail`, drops/MVP, skills/spawns em multiplos mapas e bloqueios de item/skill/mapa invalidos.
- Mapa auditado com `prontera`, `1@colo`, mapa inexistente e rename binario bloqueado.
- Guards de apply/rollback sem confirmacao foram testados para todos os pipelines e retornaram exit code `2` sem criar novos logs/backups.
- Amostra de 25 arquivos externos criticos manteve `Length` e `LastWriteTimeUtc` inalterados.

### Resultado da auditoria

- API read-only, dry-run e diff-preview estao prontos como proximo passo tecnico.
- API apply/rollback deve nascer bloqueada por confirmacao forte, permissao explicita e revisao humana.
- `map apply` nao deve ser liberado na primeira API de producao: mapas reais auditados ficaram bloqueados por existencia, ambiguidade, dependencia ausente ou rename binario.
- O manifest ainda marca `ClientDate` como desconhecido, mas `discover` detecta `2025-07-16`; antes de automatizar client-side por API, confirmar se esse valor deve ser persistido.
- Item com asset ausente ainda pode ficar aplicavel em alguns casos com warning; antes de apply via API, definir politica de asset obrigatorio por tipo/categoria.

### Validacao desta rodada

- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos.
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj` com 81/81 testes OK.

### Proximos passos atualizados

1. Iniciar API backend em modo read-only/dry-run/diff-preview.
2. Manter apply/rollback fora da primeira liberacao ou atras de feature flag/confirmacao forte.
3. Antes de liberar apply via API, fechar politica de assets obrigatorios e persistencia/confirmacao do client date.

## Atualizacao 2026-05-11 - API backend read-only/dry-run/diff-preview

- Criado projeto `backend/src/RagnaForge.Api`.
- API ASP.NET Core exposta em modo `read-only-dry-run-diff-preview`.
- Endpoints criados para `health`, `status`, capacidades de seguranca, `config validate`, `discover`, `grf index/inspect`, dry-run e diff-preview de item, equipamento, NPC, monstro e mapa.
- `apply` e `rollback` nao foram expostos por endpoint nesta fase.
- `ApiSafetyPolicy` declara writes desabilitados para item, equipamento, NPC, monstro e mapa.
- A API resolve o workspace root por `RagnaForge.slnx` ou `data/manifests/repositories.local.json`, evitando cair em `backend/src/RagnaForge.Api` quando executada com `dotnet run --project`.
- Smoke HTTP seguro confirmou `GET /health`, `GET /api/status`, `POST /api/config/validate` e ausencia de `POST /api/items/apply` (`404`).

### Validacao desta rodada

- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos.
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj` com 84/84 testes OK.
- Nenhum `--confirm APPLY` ou `--confirm ROLLBACK` foi usado.

### Proximos passos atualizados

1. Endurecer a API com autenticacao, autorizacao local e contrato de erros antes de qualquer uso administrativo real.
2. Manter endpoints de apply/rollback fora da API ate fechar politica de asset obrigatorio, client date e confirmacao forte.
3. Depois disso, iniciar interface administrativa consumindo apenas endpoints read-only/dry-run/diff-preview.

## Atualizacao 2026-05-11 - API hardening auth/guards

- Criada autenticacao local por header `X-RagnaForge-Api-Key`.
- `/health` e `/openapi/v1.json` ficam publicos; `/api/*` exige chave por padrao.
- Criado `ApiOperationGuard` com `OperationKind` para permitir apenas `ReadOnly`, `DryRun`, `DiffPreview` e `CacheWrite` local.
- `Apply`, `Rollback`, `FileWrite`, `ExternalRepoWrite` e `GrfWrite` ficam bloqueados na API.
- Criado wrapper `ApiResponse<T>` para respostas de sucesso com `correlationId`, `operationKind`, `readOnlyMode` e `durationMs`.
- Erros usam `ProblemDetails` com `errorCode`, `correlationId`, `path`, `timestamp` e `validationErrors`.
- Adicionado `X-Correlation-Id` por request.
- Adicionados limites de payload, rate limit em memoria e concurrency guard para operacoes pesadas.
- CORS restrito a `http://127.0.0.1:5173` e `http://localhost:5173`.
- OpenAPI local em `/openapi/v1.json` documenta API key e registra que apply/rollback nao existem.
- `RagnaForge.Api` agora possui `appsettings.json` com defaults seguros.

### Validacao desta rodada

- `dotnet build RagnaForge.slnx` com 0 erros e 0 avisos.
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj` com 97/97 testes OK.
- Smoke HTTP seguro:
  - `/health` sem key: `200`.
  - `/api/status` sem key: `401` com `ProblemDetails`.
  - `/api/status` com key: `200` com `ApiResponse`.
  - `/api/items/dry-run` com key: `200`.
  - `/api/items/apply` com key: `404`.
  - payload invalido em `/api/grf/inspect`: `422`.
  - rate limit reduzido retornou `429`.
  - CORS permitiu apenas origin local configurada.
- Nenhum `--confirm APPLY` ou `--confirm ROLLBACK` foi usado.

### Proximos passos atualizados

1. Iniciar interface administrativa consumindo apenas endpoints seguros.
2. Manter apply/rollback fora da API e da interface nesta fase.
3. Antes de qualquer escrita por HTTP, definir autenticacao forte, confirmacao humana, politica de asset obrigatorio e persistencia/confirmacao de client date.
