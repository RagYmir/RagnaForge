# Analise dos repositorios locais

Data: 2026-05-06
Status: Discovery read-only

## Caminhos informados

| Papel | Caminho | Estado |
| --- | --- | --- |
| rAthena | `<RATHENA_PATH>` | encontrado |
| Patch/client | `<PATCH_PATH>` | encontrado |
| GRFs/Thor/GPF de origem | `<GRF_REPOSITORY_PATH>` | encontrado |
| GRF Editor | `<GRF_EDITOR_PATH>` | encontrado |

Nenhum desses repositorios foi alterado nesta fase.

## rAthena

### Estrutura encontrada

O repositorio tem estrutura rAthena completa:

- `.github`
- `3rdparty`
- `conf`
- `db`
- `doc`
- `generated`
- `npc`
- `sql-files`
- `src`
- `tools`

Tambem existem executaveis compilados:

- `login-server.exe`
- `char-server.exe`
- `map-server.exe`
- `web-server.exe`
- `mapcache.exe`
- `map-server-generator.exe`
- `csv2yaml.exe`
- `yaml2sql.exe`
- `yamlupgrade.exe`

### Modo Renewal / Pre-Renewal

Arquivo analisado:

- `src/config/renewal.hpp`

Resultado:

- `#define PRERE` esta comentado.
- `#define RENEWAL` esta comentado como `/// #define RENEWAL`.
- Demais flags Renewal tambem estao comentadas.

Interpretacao inicial corrigida pelo usuario: a build atual parece operar sem flags Renewal ativas, mas o servidor e progressivo por episodios. Portanto o scanner deve tratar isso como estado atual do episodio, nao como decisao permanente de projeto. A arquitetura precisa suportar transicao futura para Renewal quando o calendario progressivo chegar nesse ponto.

Implicacoes:

- `RenewalModeDetector` deve virar parte de um `EpisodeProfileDetector`.
- O sistema deve armazenar historico/manifesto do episodio ativo.
- O Dependency Resolver deve receber o alvo de episodio/progressao antes de escolher arquivos `db/pre-re`, `db/re` ou regras hibridas.
- Generators nao devem hard-codear pre-renewal.
- Quando o servidor migrar para Renewal, os scanners devem reavaliar IDs, bancos e dependencias sem recriar a arquitetura.

### Banco de itens

Arquivos relevantes:

- `db/item_db.yml`
- `db/pre-re/item_db.yml`
- `db/pre-re/item_db_equip.yml`
- `db/pre-re/item_db_etc.yml`
- `db/pre-re/item_db_usable.yml`
- `db/re/item_db.yml`
- `db/re/item_db_equip.yml`
- `db/re/item_db_etc.yml`
- `db/re/item_db_usable.yml`
- `db/import/item_db.yml`

`db/item_db.yml` possui `Footer: Imports` apontando para:

- `db/pre-re/item_db.yml` em modo `Prerenewal`
- `db/re/item_db.yml` em modo `Renewal`
- `db/import/item_db.yml`

`db/import/item_db.yml` existe e no levantamento inicial nao tem entradas ativas de item, apenas template/comentarios.

Conclusao:

- Para criar item/equipamento, o alvo preferencial e `db/import/item_db.yml`.
- O ID allocator deve varrer `db/pre-re`, `db/re` e `db/import`.
- Equipamento deve entrar no mesmo fluxo de item, usando campos extras como `Locations`, `View`, `Slots`, `Jobs`, `Gender` e `EquipLevelMin`.

### Banco de monstros

Arquivos relevantes:

- `db/mob_db.yml`
- `db/pre-re/mob_db.yml`
- `db/re/mob_db.yml`
- `db/import/mob_db.yml`
- `db/import/mob_avail.yml`
- `db/import/mob_skill_db.txt`

`db/mob_db.yml` possui imports por modo e `db/import/mob_db.yml`.

Achado local:

- `db/import/mob_db.yml` tem 2 entradas ativas detectadas.
- `db/import/mob_skill_db.txt` tem 1 entrada ativa detectada.
- `db/import/mob_avail.yml` existe, sem entrada ativa detectada no scan superficial.

Exemplo de custom observado:

- `CRAFT_TREE_01`
- `CRAFT_TREE_03`

Conclusao:

- O servidor ja usa customizacao de monstro.
- O scanner deve preservar esse padrao e nunca sobrescrever essas entradas.
- O dry-run de monstro deve conferir `mob_db`, `mob_avail`, `mob_skill_db` e scripts/spawns.

### Mapas

Arquivos relevantes:

- `conf/map_athena.conf`
- `conf/maps_athena.conf`
- `conf/import/map_conf.txt`
- `db/map_index.txt`
- `db/map_cache.dat`
- `db/pre-re/map_cache.dat`
- `db/re/map_cache.dat`
- `db/import/map_cache.dat`
- `db/import/map_index.txt`

Achados:

- `conf/map_athena.conf` define `db_path: db`.
- `conf/map_athena.conf` define `use_grf: no`.
- `conf/map_athena.conf` importa:
  - `conf/maps_athena.conf`
  - `conf/import/map_conf.txt`
- `conf/import/map_conf.txt` existe e esta vazio.
- `conf/maps_athena.conf` tem 1265 mapas ativos e 41 mapas comentados.
- `db/map_index.txt` tem 1323 linhas no levantamento inicial.

Conclusao critica:

- Este ambiente nao deve depender de leitura direta de GRF para mapa, porque `use_grf: no`.
- Implantar mapa exige gerar/atualizar `map_cache.dat`.
- O alvo seguro para registro de carregamento de mapa e `conf/import/map_conf.txt`.
- `map_index.txt` continua sensivel: append-only, sem reordenar indices existentes.

### NPCs

Arquivos relevantes:

- `npc/scripts_athena.conf`
- `npc/scripts_custom.conf`
- `npc/custom/**`

Achados:

- `npc/custom` existe e contem scripts custom prontos.
- `npc/scripts_custom.conf` esta ativo e atualmente carrega:
  - `npc/custom/vote_points.txt`
  - `npc/custom/craft_arvores.txt`

Conclusao:

- Para novos NPCs, o padrao local deve ser `npc/custom/...` + registro em `npc/scripts_custom.conf`.
- O scanner deve detectar NPCs comentados e ativos separadamente.
- Desativacao deve preferir comentar/overlay reversivel em `scripts_custom.conf` quando o NPC for custom.

## Patch/client

### Estrutura encontrada

Top-level relevante:

- `ai`
- `bgm`
- `data`
- `emblem`
- `GRF_CARTAS_EM_HD`
- `memo`
- `navigationdata`
- `savedata`
- `screenshot`
- `skin`
- `system`
- `texture`

Executaveis/client:

- `2025-07-16_Ragexe_175220998_clientinfo_patched.exe`
- `RagYmir.exe`

GRFs no Patch:

- `cdata.grf`
- `data.grf`
- `data_0.grf`
- `GRF_HD.grf`

### DATA.INI

Conteudo:

```ini
[Data]
1=cdata.grf
2=GRF_CARTAS_EM_HD
3=data.grf
```

Observacoes:

- `data_0.grf` e `GRF_HD.grf` existem na pasta, mas nao aparecem no `DATA.INI`.
- `GRF_CARTAS_EM_HD` aparece como entrada sem extensao e existe como diretorio.
- A prioridade efetiva de patch precisa respeitar `DATA.INI` e tambem qualquer comportamento custom do client.

### Sistema client-side de item

Arquivos encontrados:

- `data/idnum2itemdesctable.txt`
- `data/idnum2itemdisplaynametable.txt`
- `data/idnum2itemresnametable.txt`
- `data/num2itemdesctable.txt`
- `data/num2itemdisplaynametable.txt`
- `data/num2itemresnametable.txt`
- `data/itemslotcounttable.txt`
- `system/iteminfo_true.lub`

Metricas superficiais:

- `idnum2itemresnametable.txt`: 8668 linhas.
- `num2itemresnametable.txt`: 8647 linhas.
- `itemslotcounttable.txt`: 1446 linhas.
- `system/iteminfo_true.lub`: 1628 bytes.

Conclusao:

- O Patch usa tabelas TXT antigas/legadas para item.
- `iteminfo_true.lub` existe, mas e pequeno demais para ser tratado como fonte principal completa sem decompilar/validar.
- O primeiro dry-run de item deve gerar diffs para TXTs legados e so tocar LUB se o resolver provar que e necessario.

### Datainfo e sprites visuais

Arquivos encontrados em `data/luafiles514/lua files/datainfo`:

- `accessoryid.lub`
- `accname.lub`
- `accname_f.lub`
- `jobname.lub`
- `jobname_f.lub`
- `npcidentity.lub`
- `spriterobeid.lub`
- `spriterobename.lub`
- `spriterobename_f.lub`
- `weapontable.lub`
- `weapontable_f.lub`

Conclusao:

- Equipamentos visuais, NPCs e monstros exigem leitura/geracao de LUB ou pipeline de decompilacao/compilacao.
- O GRF Editor possui suporte a `Lua/Lub`, portanto a integracao deve investigar se consegue ler/decompilar estes arquivos com seguranca.

### Assets soltos em `data`

Contagem superficial em `Patch_teste\data`:

- `.spr`: 72667
- `.act`: 55593
- `.bmp`: 10967
- `.png`: 262
- `.rsw`: 243
- `.gnd`: 216
- `.gat`: 244
- `.rsm`: 58
- `.rsm2`: 84
- `.str`: 149
- `.lua`: 7
- `.lub`: 1108

Conclusao:

- O Patch ja contem muitos assets extraidos, entao o primeiro Asset Scanner pode trabalhar em diretorio antes de indexar GRFs gigantes.
- Para mapas, ha arquivos `.rsw/.gnd/.gat` soltos em `data`, mas ainda e obrigatorio validar texturas/modelos/sons/efeitos.

## Repositorio de GRFs/Thor/GPF

Caminho: `<GRF_REPOSITORY_PATH>`

### Top-level encontrado

Diretorios:

- `GRF BLOODBATH`
- `GRF ELVES`
- `GRF HISTORY`
- `GRF_Playground_RO`
- `GRF_Ranastar`
- `History Reborn 3.0`
- `RagYmir`

Containers/top-level:

- `CLIENTE SERVIDOR PROMISE.grf`
- `data_0.grf`
- `GRF FORSAKEN.grf`
- `GRF HD.grf`
- `GRF OSRO.grf`
- `GRF RISO F VALHALLA.grf`
- `GRF SERVIDOR ADVENTURERO.grf`
- `GRF_Monstros personalizados.grf`
- `IRO data.grf`
- `ITENS SERVIDOR PINAS.grf`
- `Mobs Collection.thor`
- `PARA ABRIR - xenaro.grf`
- `RUNEHOST data.grf`
- `Super Custom itend 3d.grf`
- `GRF_CARTAS_EM_HD.zip`
- `Texture_Allcards.zip`

Observacao:

- O diretÃ³rio e grande; tentativas de contagem recursiva completa de containers/extension groups atingiram timeout de 30s.
- O scanner real deve ser incremental, cancelavel e cacheado, com progresso e limite por lote.

## Implicacoes para a arquitetura

1. O primeiro scanner deve ser read-only e incremental.
2. O Patch Scanner deve priorizar:
   - `DATA.INI`
   - `data/*.txt` de item legado
   - `data/luafiles514/lua files/datainfo/*.lub`
   - assets soltos em `data`.
3. O rAthena Scanner deve priorizar:
   - `db/*_db.yml`
   - `db/import`
   - `src/config/renewal.hpp`
   - `conf/map_athena.conf`
   - `npc/scripts_custom.conf`.
4. O GRF Scanner deve vir depois do scanner de diretorio, usando `GrfEditorIntegration` para containers grandes.
5. O primeiro dry-run mais seguro e "criar item simples" sem equipamento visual, mirando:
   - `db/import/item_db.yml`
   - `data/idnum2item*table.txt`
   - `data/num2item*table.txt`
   - icone/resource existente ja presente no Patch.
6. A escolha pre-re/renewal deve ser parametrizada por perfil de episodio, porque o servidor e progressivo.

## Riscos locais

- Patch mistura TXT legado e LUB; gerar so `ItemInfo` provavelmente falharia.
- `use_grf: no` torna `map_cache.dat` obrigatorio para mapas.
- `conf/import/map_conf.txt` esta vazio, bom alvo de mapas, mas `map_index.txt` ainda exige append controlado.
- Ja existem customs em `db/import/mob_db.yml` e `npc/custom`; ID allocator precisa respeita-los.
- GRFs de origem sao muito grandes; indexacao completa deve ter cache, progresso e cancelamento.
- O estado atual sem flags Renewal nao pode congelar a arquitetura, pois o servidor avancara por episodios ate chegar ao Renewal.
