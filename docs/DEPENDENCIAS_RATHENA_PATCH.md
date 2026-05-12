# Dependencias rAthena / Patch / GRF por tipo de conteudo

Data: 2026-05-11
Status: Dependencias centrais mapeadas; client-side avancado de item/equipamento agora cobre itemInfo, TXT legado, clients hibridos, TextLub e bloqueio de bytecode.
Escopo: rAthena + Patch/client + GRF/Thor/GPF

Este documento e o contrato tecnico inicial do projeto. Nenhuma implementacao funcional deve avancar sem que os scanners validem estes pontos contra os repositorios reais do servidor, do Patch/client e dos GRFs.

## Premissas verificadas

- rAthena separa bancos em `db/re`, `db/pre-re` e permite arquivos de customizacao em `db/import`.
- `conf/map_athena.conf` referencia mapas por `import: conf/maps_athena.conf` e tambem `import: conf/import/map_conf.txt` na configuracao atual upstream.
- `db/map_index.txt` mapeia nomes de mapas para indices; os proprios comentarios do arquivo indicam que IDs de mapas nao devem mudar e novos mapas devem ser adicionados ao fim.
- Scripts NPC sao arquivos texto carregados por linhas `npc: <path>` em arquivos `.conf`; a documentacao de script tambem menciona `delnpc: <path>`.
- O client pode variar: clientes novos usam `System/ItemInfo.lua` ou `System/ItemInfo.lub`; clientes antigos podem usar tabelas TXT/LUA/LUB em `data/luafiles514/lua files/datainfo` ou estruturas equivalentes.
- O GRF Editor instalado possui suporte tecnico a `.grf`, `.gpf`, `.thor`, `.rgz`, `.spr`, `.act`, `.gat`, `.gnd`, `.rsw`, `.rsm`, `.rsm2`, `.str`, `.tga`, `.pal`, `.lua` e `.lub`, conforme metadata/strings locais.

Fontes consultadas:

- Local: `<GRF_EDITOR_PATH>`
- rAthena upstream: `https://github.com/rathena/rathena`
- rAthena raw templates: `https://raw.githubusercontent.com/rathena/rathena/master/db/import-tmpl/item_db.yml`
- rAthena raw templates: `https://raw.githubusercontent.com/rathena/rathena/master/db/import-tmpl/mob_db.yml`
- rAthena map config: `https://raw.githubusercontent.com/rathena/rathena/master/conf/map_athena.conf`
- rAthena map index: `https://raw.githubusercontent.com/rathena/rathena/master/db/map_index.txt`
- rAthena script docs: `https://github.com/rathena/rathena/blob/master/doc/script_commands.txt`

## Variaveis que precisam ser detectadas antes de escrever qualquer coisa

- Caminho do repositorio rAthena: informado como `<RATHENA_PATH>`.
- Caminho do Patch/client: informado como `<PATCH_PATH>`.
- Caminhos dos repositorios GRF/Thor/GPF de origem: informado como `<GRF_REPOSITORY_PATH>`.
- Modo Renewal ou Pre-Renewal do episodio atual.
- Perfil progressivo do servidor: episodio atual, proximo episodio e regras de transicao ate Renewal.
- Client date/build.
- Se o client usa `ItemInfo.lua`, `ItemInfo.lub` ou tabelas antigas.
- Estrutura real de `data/`, `System/`, `luafiles514/`, `datainfo/` e eventuais GRFs de build.
- Se o servidor usa `map_cache.dat` em `db/map_cache.dat`, `db/re/map_cache.dat`, `db/pre-re/map_cache.dat` ou caminho custom em config.
- Se o servidor esta configurado para ler mapa por cache ou por GRF/data directory (`use_grf`).
- Convencoes locais de import/custom ja existentes.

## Achados locais ja detectados

- rAthena local tem `db/import`, `db/import-tmpl`, `db/pre-re`, `db/re`.
- `db/item_db.yml` importa `db/pre-re/item_db.yml`, `db/re/item_db.yml` e `db/import/item_db.yml`.
- `db/mob_db.yml` importa `db/pre-re/mob_db.yml`, `db/re/mob_db.yml` e `db/import/mob_db.yml`.
- `src/config/renewal.hpp` nao tem `#define RENEWAL` ativo no scan inicial, mas o usuario confirmou que o servidor e progressivo e chegara ao Renewal em updates futuros.
- `conf/map_athena.conf` define `use_grf: no`.
- `conf/map_athena.conf` importa `conf/maps_athena.conf` e `conf/import/map_conf.txt`.
- `conf/import/map_conf.txt` existe e esta vazio.
- `npc/scripts_custom.conf` carrega atualmente `npc/custom/vote_points.txt` e `npc/custom/craft_arvores.txt`.
- Patch local tem `DATA.INI` com `cdata.grf`, `GRF_CARTAS_EM_HD`, `data.grf`.
- Patch local tem TXT legado para item: `idnum2item*`, `num2item*`, `itemslotcounttable`.
- Patch local tem LUB/datainfo para equipamento/NPC/sprites: `accessoryid.lub`, `accname.lub`, `jobname.lub`, `npcidentity.lub`, `spriterobe*`, `weapontable*`.
- Patch local tem muitos assets soltos em `data`, incluindo sprites, ACT, mapas e modelos.
- `weapontable.lub` local define `Weapon_IDs`, `WeaponNameTable`, `Expansion_Weapon_IDs`, `WeaponHitWaveNameTable` e `BowTypeList`.
- Nao foi encontrada tabela dedicada de shield visual equivalente a `weapontable.lub`/`spriterobeid.lub`; shield visual novo nao deve ser registrado por `.lub` nesta fase.
- O banco local de equipment mostra shields `Left_Hand` usando `View` pequeno recorrente; no scan atual apareceram `1..6`.

## Regra de progressao por episodio

O projeto nao deve tratar pre-renewal como estado permanente. Cada dry-run/apply deve carregar um perfil de episodio contendo:

- episodio alvo.
- modo de formulas e bancos: pre-renewal, renewal ou hibrido.
- client date/build alvo.
- arquivos rAthena ativos naquele episodio.
- arquivos Patch/client ativos naquele episodio.
- regras de migracao esperadas para episodios futuros.

Quando o servidor migrar para Renewal, o scanner deve detectar a mudanca e recalcular dependencias em vez de depender de assumptions antigas.

## Taxonomia de dependencias

- Obrigatoria: sem isto o conteudo nao funciona ou pode quebrar login/map-server/client.
- Condicional: obrigatoria apenas em certos clients, modos, tipos de item ou assets.
- Opcional: melhora operacao, preview, organizacao, testes ou manutencao.
- Risco: ponto que exige diff, backup, dry-run e confirmacao.

## Criar item

### rAthena server-side

Obrigatorio:

- Arquivo de item DB ativo, preferencialmente `db/import/item_db.yml` quando suportado pela versao local.
- Validacao contra bases ativas em `db/re/*.yml`, `db/pre-re/*.yml` e `db/import/*.yml`.
- Campos minimos: `Id`, `AegisName`, `Name`, `Type`.
- Campos funcionais conforme tipo: `SubType`, `Buy`, `Sell`, `Weight`, `Attack`, `MagicAttack`, `Defense`, `Range`, `Slots`, `Flags`, `Delay`, `Stack`, `NoUse`, `Trade`, `Script`.
- ID livre.
- `AegisName` livre.
- YAML valido e parseavel pelo rAthena.

Condicional:

- `item_cash_db.yml`, grupos de item, combos, packages, refine/enchant/reform quando o item depender desses sistemas.
- SQL apenas se o servidor local usar pipeline custom de SQL para static DB, nao assumir.

Patch/client:

- Entrada client-side em `System/ItemInfo.lua` ou `System/ItemInfo.lub`, se client moderno.
- Alternativamente tabelas antigas de datainfo quando detectadas.
- Nome identificado e nao identificado.
- Descricao identificada e nao identificada.
- `identifiedResourceName` e `unidentifiedResourceName` corretos.
- Icone de inventario em caminho client-side correspondente ao resource name.
- Sprite/drop visual quando aplicavel.
- Codificacao correta para paths coreanos/legacy.

GRF/Patch assets:

- Icone `.bmp`, `.png` ou formato aceito pelo client.
- Sprite de drop/item, se aplicavel.
- Conferencia de paths dentro de GRF/Patch, sem confiar apenas no nome.

Validacoes criticas:

- O item nao pode aparecer como Apple, Unknown Item ou sem descricao.
- ID e `AegisName` nao podem colidir.
- Resource name precisa existir ou ser explicitamente marcado como ausente.
- Script do item precisa passar por scanner sintatico rAthena ou checklist de comandos.
- Dry-run deve mostrar diff server-side e client-side antes de aplicar.

## Editar item

Obrigatorio:

- Localizar origem real do item: base, import, custom, Patch/client e GRF.
- Separar dados server-side de dados client-side.
- Gerar diff por arquivo.
- Se desativar, preferir override/import ou alteracao reversivel, sem apagar base.

Condicional:

- Se alterar sprite/resource name, revalidar icone, drop sprite e client table.
- Se alterar script, validar comandos e impacto em reload.
- Se alterar stack/trade/nouse, validar comportamento de inventario/storage/mail/cart.

Riscos:

- Import pode sobrescrever ou completar entradas de modo diferente entre versoes rAthena.
- Alterar item base diretamente aumenta risco em updates.

## Criar equipamento

Equipamento e especializacao de item. No dominio, deve reutilizar o pipeline de item e adicionar propriedades de equipamento.

### rAthena server-side

Obrigatorio:

- Todos os requisitos de criar item.
- `Type` compativel com equipamento, normalmente arma/armadura conforme schema da versao local.
- `Locations` corretas: head, armor, weapon, shield, garment, shoes, accessory, costume, shadow etc conforme rAthena local.
- `View` quando o tipo exigir sprite visual.
- `Slots`, `Jobs`, `Classes`, `Gender`, `EquipLevelMin`, `EquipLevelMax`.
- `WeaponLevel` ou `ArmorLevel` quando aplicavel.
- `Refineable`, `Gradable`, `Script`, `EquipScript`, `UnEquipScript` quando aplicavel.

Patch/client:

- Entrada em `ItemInfo.lua/lub` ou tabelas antigas com `ClassNum` / ViewID coerente.
- Icone de inventario.
- Resource names de equipamento.
- Arquivos `datainfo` relacionados a sprite visual, dependendo do tipo e client:
  - headgear/accessory/costume head: tabelas de accessory/accname/sprite id equivalentes.
  - robe/costume garment: `spriterobeid.lub` e `spriterobename.lub`, quando presentes.
  - weapon: `weapontable.lub`, incluindo `Weapon_IDs`, `WeaponNameTable`, `Expansion_Weapon_IDs`, `WeaponHitWaveNameTable` e `BowTypeList` para arcos.
  - shield: usar apenas `View` embutido ja conhecido do client local; nao propor tabela visual nova sem mapeamento adicional.
  - shadow/costume/outros: tabelas client-side especificas da data do client.
  - genero masculino/feminino quando o asset exigir.
- Sprites `.spr/.act` visuais do equipamento quando aplicavel.

GRF/Patch assets:

- `.spr` e `.act` do visual.
- Icone.
- Paletas ou assets auxiliares se o sprite depender.
- Paths diferenciados por genero/job quando o client exigir.

Validacoes criticas:

- Equipamento deve equipar no local correto.
- ViewID/ViewSprite nao pode colidir de forma invisivel.
- Client-side precisa bater com server-side: `View` no rAthena e `ClassNum`/tabelas do client.
- Validar restricoes de job, genero, nivel e slot.
- Validar que simbolos emitidos em `.lub` sao identificadores Lua seguros.
- Validar que strings emitidas em `.lub` nao contem caracteres que quebrem literal Lua.
- Validar `Locations` contra a lista documentada do rAthena local.
- Para shield, validar que o `View` pertence ao conjunto embutido suportado pelo client local antes de permitir dry-run aplicavel.
- Preview visual deve ser opcional no MVP, mas a arquitetura deve permitir.
- Apply inicial de equipamento deve escrever somente em `rAthena/db/import` e `Patch/data`, com backup em `data/backups/equipment`, log em `data/logs/equipment` e rollback por log.
- Rollback de equipamento deve validar o SHA-256 do estado aplicado antes de restaurar backups, bloqueando rollback cego se o arquivo foi alterado manualmente depois do apply.
- Conflitos obrigatorios antes de escrever: ID ja usado, `AegisName` ja usado, ID legado duplicado nas tabelas TXT, simbolo visual duplicado, `ViewID` duplicado, alvo fora das raizes permitidas e mudanca de existencia desde o dry-run.

## Editar equipamento

Obrigatorio:

- Localizar item base e dependencias visuais.
- Mostrar origem de `View`, `Locations`, scripts, slots, jobs, client tables e sprites.
- Revalidar todas as dependencias se mudar sprite, ViewID, location, slot ou client description.
- Gerar rollback para server-side e patch-side.

Riscos:

- Equipamentos com mesmo ViewID podem ser intencionais; marcar colisao como risco, nao erro automatico.
- Alterar `AegisName` pode quebrar scripts, drops, lojas, quests e combos.

## Criar NPC

### rAthena server-side

Obrigatorio:

- Arquivo `.txt` de script em pasta custom, preferencialmente sob `npc/custom/` ou convencao local.
- Registro em arquivo `.conf` carregado pelo map-server, idealmente custom/import.
- Linha NPC valida: mapa, X, Y, direcao, tipo, nome, sprite e bloco de script.
- Mapa existente e carregavel.
- Nome sem colisao funcional ou colisao explicitamente aceita.
- Sintaxe basica do script: chaves, tabs/formatacao, ponto e virgula nos comandos, labels, fechamento.

Patch/client:

- Sprite do NPC precisa existir no client.
- Para sprite custom, atualizar arquivos de identidade/nome do NPC conforme client detectado, por exemplo `jobname.lua/lub` e `npcidentity.lua/lub` em clients que usam esse sistema.
- Arquivos `.spr/.act` do NPC no caminho esperado.
- Quando o sprite nao existir solto no Patch, tentar lookup opcional em GRF e manter no relatorio se o match veio de `local-index`, `live-scan-fallback` ou `live-scan`.

Validacoes criticas:

- O mapa existe em rAthena e Patch.
- Coordenadas dentro de limites do `.gat`.
- Sprite ID/nome resolvido.
- Arquivo `.conf` nao registra duas vezes o mesmo script.
- Desativacao deve ser reversivel: comentar entrada, `delnpc` controlado ou overlay custom.
- Apply inicial de NPC deve escrever somente em `rAthena/npc`, com backup em `data/backups/npcs`, log em `data/logs/npcs` e rollback por log.

## Editar NPC

Obrigatorio:

- Localizar por nome, mapa, coordenada, sprite e arquivo origem.
- Exibir diff de script e diff de registro `.conf`.
- Se mover mapa/coordenada, revalidar mapa e `.gat`.
- Se alterar sprite, revalidar client-side.

Riscos:

- `@reloadscript` pode ter efeitos colaterais em spawns; checklist deve preferir reinicio controlado quando apropriado.
- NPC duplicado pode ser renomeado/avisado pelo servidor; ferramenta deve detectar antes.

## Criar monstro

### rAthena server-side

Obrigatorio:

- Entrada ativa em `mob_db.yml`, preferencialmente `db/import/mob_db.yml` quando suportado.
- Campos minimos: `Id`, `AegisName`, `Name`.
- Stats: `Level`, `Hp`, `Sp`, `BaseExp`, `JobExp`, `Attack`, `Attack2`, `Defense`, `MagicDefense`, atributos, ranges, size, race, element, modes.
- Drops e MVP drops com indices quando houver override.
- ID livre.
- `AegisName` livre.

Condicional:

- `mob_avail.yml` se houver sprite custom/override ou disponibilidade visual especifica.
- `mob_skill_db.txt` se o monstro tiver skills na estrutura local atual.
- Spawn em script ou banco de spawn conforme convencao local.
- Drops que referenciam itens custom precisam validar esses itens em `db/item_db.yml`, `db/import/item_db.yml`, `db/pre-re/item_db*.yml` e `db/re/item_db*.yml`.
- Skills precisam validar IDs em `db/skill_db.yml`, `db/import/skill_db.yml`, `db/pre-re/skill_db.yml` e `db/re/skill_db.yml`.

Patch/client:

- Sprite `.spr/.act` do monstro.
- Tabelas de monster/resource name conforme client detectado.
- Possiveis arquivos de nome/identity/datainfo do monstro.

Validacoes criticas:

- Monstro nasce sem erro de sprite.
- Drops referenciam itens existentes.
- MVP drops usam a mesma validacao de item existente.
- Quantity por drop nao entra automaticamente no `mob_db.yml` atual; quando solicitada, o dry-run deve bloquear e explicar a limitacao.
- Skills referenciam skills validas para a versao.
- `mob_skill_db.txt` precisa permanecer no layout TXT classico de 19 colunas para este milestone.
- Spawn referencia mapa carregavel.
- Respawn, quantidade e coordenadas precisam ser coerentes com `.gat`.
- Spawn com label de evento e suportado quando o label ja estiver no formato rAthena seguro (`Namespace::OnEvent` ou equivalente textual seguro).
- Spawn multiplo deve ficar em script custom proprio, sem tocar scripts base.
- Apply inicial de monstro deve escrever somente em `rAthena/db/import` e `rAthena/npc`, com backup em `data/backups/monsters`, log em `data/logs/monsters` e rollback por log.
- Rollback de monstro deve validar o SHA-256 do estado aplicado antes de restaurar backups, bloqueando rollback cego se o arquivo foi alterado manualmente depois do apply.
- Conflitos obrigatorios antes de escrever: ID ja usado, `AegisName` ja usado, entrada existente em `mob_avail`, anchor/linha de skill duplicado, drop duplicado, spawn/loader duplicado, alvo fora das raizes permitidas e mudanca de existencia desde o dry-run.
- Antes da substituicao final, o arquivo completo em staging deve passar por validacao sintatica de YAML/TXT/script.

## Editar monstro

Obrigatorio:

- Localizar origem em `mob_db`, `mob_avail`, `mob_skill_db` e spawns.
- Mostrar dependencias server-side/client-side.
- Se alterar sprite, revalidar Patch/client.
- Se alterar drops, validar IDs/AegisName dos itens.
- Se alterar spawn, validar mapa e coordenadas.

Riscos:

- Remover spawn pode afetar quests/events. Preferir desativacao reversivel.
- Alterar monstro base pode impactar muitos mapas; preferir override/import quando suportado.

## Implantar mapa

Mapas sao area de alto risco. Nao tratar como copia simples de `.rsw`, `.gnd` e `.gat`.

### GRF/Patch assets

Obrigatorio:

- `.rsw`
- `.gnd`
- `.gat`
- Texturas referenciadas pelo `.gnd`/`.rsw`
- Modelos `.rsm`/`.rsm2` referenciados pelo `.rsw`
- Sons referenciados pelo `.rsw`, quando houver
- Efeitos `.str` e outros recursos referenciados, quando houver
- Arquivos auxiliares detectados por parse dos formatos, nao so por nomes.

Condicional:

- Mini mapa, loading image, arquivos de navegacao, lua/lub de warp, dataresnametable ou equivalentes, conforme client.
- Thor/GRF de saida para distribuicao.

### rAthena server-side

Obrigatorio:

- Registrar mapa em lista carregada pelo map-server. Upstream atual importa `conf/maps_athena.conf` e `conf/import/map_conf.txt`; preferir `conf/import/map_conf.txt` ou estrutura custom validada.
- Registrar no `db/map_index.txt` ou import equivalente se a versao local oferecer. Se nao houver import seguro, map_index e arquivo sensivel e exige diff/backup explicito.
- Atualizar/gerar `map_cache.dat` com ferramenta correta do rAthena local.
- Validar se o servidor usa cache ou leitura por GRF/data directory.

Condicional:

- Mapflags.
- Warps.
- NPCs iniciais.
- Spawns.
- Instance configs.

Validacoes criticas:

- Nome de mapa unico, tamanho e chars validos.
- Map index append-only; nunca reordenar indices existentes.
- Mapa precisa existir no Patch/client e no rAthena.
- Map-cache precisa ser compativel com a versao e modo Renewal/Pre-Renewal.
- Texturas/modelos/sons ausentes devem bloquear apply.
- Rename binario entre `MapName` e resource names de `.rsw/.gnd/.gat` deve bloquear apply enquanto nao houver rewriter seguro dos binarios.
- Asset plans devem manter origem do match (`TargetLoosePatch`, `LoosePatch`, `GrfExtraction` ou `Missing`) e alvo final em `Patch/data`.
- Diff precisa listar comandos pos-apply: gerar map-cache, reiniciar map-server ou reload adequado.

## Arquivos que o Dependency Resolver deve conhecer

### rAthena

- `db/re/*.yml`
- `db/pre-re/*.yml`
- `db/import/*.yml`
- `db/map_index.txt`
- `db/map_cache.dat`
- `db/re/map_cache.dat`
- `db/pre-re/map_cache.dat`
- `conf/map_athena.conf`
- `conf/maps_athena.conf`
- `conf/import/*.conf`
- `npc/**/*.txt`
- `npc/**/*.conf`
- `npc/custom/**`
- `doc/script_commands.txt` para validacao auxiliar local.

### Patch/client

- `data/**`
- `System/ItemInfo.lua`
- `System/ItemInfo.lub`
- `data/luafiles514/lua files/datainfo/**`
- `data/sprite/**`
- `data/texture/**`
- `data/model/**`
- `data/wav/**`
- `data/effect/**`
- `*.grf`
- `*.gpf`
- `*.thor`

### GRF/Thor/GPF

- Indice de entradas.
- CRC/hash/tamanho.
- Paths normalizados e paths originais.
- Codificacao de nomes.
- Arquivos duplicados em GRFs de prioridade diferente.
- Arquivos removidos por Thor.

## Gates obrigatorios antes de apply

1. Repositorios configurados.
2. Scanners executados.
3. Fonte da verdade resolvida.
4. ID allocator validado.
5. Dependency Resolver sem ausencias obrigatorias.
6. Dry-run gerado.
7. Diff por arquivo revisado.
8. Backup planejado.
9. Comandos pos-apply listados.
10. Confirmacao explicita do usuario.

## Atualizacao 2026-05-07

### Mapa

- O scanner atual ja percorre referencias de texturas, modelos, sons, efeitos e sprites quando `.rsw/.gnd` existem como arquivos soltos no Patch.
- Quando o mapa estiver apenas em GRF, o dry-run agora pode extrair `.rsw/.gnd` para uma raiz temporaria controlada, rodar o scanner profundo e apagar os arquivos temporarios ao final.
- A origem do scan fica explicita como `LoosePatch` ou `ControlledGrfExtraction`; a origem do match GRF continua exposta separadamente no lookup por `LocalIndex`, `LiveScanFallback` ou `LiveScan`.
- O relatorio de mapa agora expoe `AssetPlans` e `MapCachePlan`, permitindo apply/rollback inicial com backup, log e rebuild de `map_cache.dat` em staging.
- A leitura profunda atual usa strings internas dos arquivos; parser binario dedicado para `.rsw/.gnd` permanece como evolucao necessaria para reduzir heuristica antes de automacoes mais amplas.

### Monstro

- `mob_skill_db.txt` passou a ser dependencia efetiva do diff preview quando o dry-run recebe skill.
- O spawn custom agora precisa considerar tambem `spawn-x`, `spawn-y`, `spawn-area-x`, `spawn-area-y` e `spawn-label`.

### NPC

- Sprite NPC padrao deve ser confirmado em `jobname.lub`, `jobidentity.lub` e/ou `npcidentity.lub` quando esses arquivos estiverem legiveis como texto.
- Sprite NPC nao padrao passa a exigir verificacao client-side adicional mesmo quando o script server-side estiver valido.

### Shield visual

- Na ausencia de tabela client-side dedicada para shield visual custom, o caminho oficial passa a ser:
  - usar apenas `View` embutido para shield real; ou
  - redirecionar para pipeline `robe`/`Costume_Garment` quando o visual ja aparecer em `spriterobename.lub` ou `spriterobeid.lub`.

## Atualizacao 2026-05-11 - NPC client identity

Dependencias client-side de NPC custom:

- `jobname.lua`, `jobname.lub` ou TXT equivalente.
- `jobidentity.lua`, `jobidentity.lub` ou TXT equivalente.
- `npcidentity.lua`, `npcidentity.lub` ou TXT equivalente.
- Sprite `.spr/.act` solto no Patch ou resolvido por GRF com proveniencia registrada.

Regras:

- `TextLua`, `TextLub` e `LegacyTxt` sao formatos textuais manipulaveis em staging.
- `BinaryLub` bloqueia apply client-side.
- `Unknown` bloqueia apply client-side.
- Sprite resolvido apenas em GRF nao e copiado automaticamente nesta etapa.
- Full apply de NPC custom exige identidade client-side segura ou registro existente completo.
- Apply server-only exige flag explicita `--allow-server-only`.

## Atualizacao 2026-05-11 - itemInfo e clients hibridos

Dependencias client-side de item/equipamento agora sao planejadas por `ClientSidePlan`:

- `System/ItemInfo.lua`
- `System/ItemInfo.lub`
- `system/iteminfo.lua`
- `system/iteminfo.lub`
- `system/iteminfo_true.lua`
- `system/iteminfo_true.lub`
- `data/idnum2itemdisplaynametable.txt`
- `data/idnum2itemresnametable.txt`
- `data/idnum2itemdesctable.txt`
- `data/num2itemdisplaynametable.txt`
- `data/num2itemresnametable.txt`
- `data/num2itemdesctable.txt`
- `data/itemslotcounttable.txt`
- datainfo visual textual: `accessoryid`, `accname`, `spriterobeid`, `spriterobename`, `weapontable`.

Regras:

- `TextLua`, `TextLub` e `LegacyTxt` podem ser usados em diff/apply com staging.
- `BinaryLub` e `Unknown` bloqueiam.
- Client hibrido precisa explicitar qual alvo sera escrito; nesta rodada, o Patch atual usa TXT legado completo como alvo seguro e `itemInfo` fica read-only.
- Asset copy de icone/sprite ainda fica planejado, nao executado automaticamente.

## Atualizacao 2026-05-11 - auditoria pre-producao

Achados adicionais:

- O manifest local ainda declara `ClientDateStatus = unknown`, mas `discover` detectou `ClientDate = 2025-07-16` pelo executavel `2025-07-16_Ragexe_175220998_clientinfo_patched.exe`.
- `grf index` completo encontrou 38 containers no repositorio de GRFs.
- `data_0.grf` contem 131185 entradas e confirma cobertura de formatos essenciais para assets: `.bmp`, `.act`, `.spr`, `.tga`, `.rsm`, `.wav`, `.str`, `.pal`, `.rsm2`, `.gat`, `.rsw`, `.gnd` e `.lub`.
- Item com asset ausente ainda pode seguir como aplicavel em casos simples com warning; antes de apply por API, a ferramenta deve definir quais tipos exigem asset obrigatorio.
- Mapa real continua dependendo de resolucao unica de texturas/modelos/sons/efeitos; referencias ambiguas de GRF bloqueiam apply.
- Sprite NPC encontrado apenas em GRF continua dependendo de pipeline futuro de asset copy para Patch.
