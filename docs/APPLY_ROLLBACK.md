# Apply / Rollback

Data: 2026-05-11
Status: Item, equipamento, NPC, monstro e mapa possuem apply/rollback com confirmacao explicita. Monstro agora inclui cobertura avancada de drops/skills/spawns e validacao sintatica pos-write em staging.

## Regras Globais

- Nenhum apply sem dry-run aplicavel.
- Nenhum apply sem `--confirm APPLY`.
- Nenhum rollback sem `--confirm ROLLBACK`.
- Backups e logs ficam sempre dentro do workspace.
- rAthena, Patch e GRFs originais continuam como fontes da verdade.
- GRFs originais nao sao alteradas diretamente.

## Item

- Logs: `data/logs/items`.
- Backups: `data/backups/items`.
- Raizes de escrita: rAthena e Patch conforme propostas do dry-run.
- Rollback por log de apply.

## Equipamento

- Logs: `data/logs/equipment`.
- Backups: `data/backups/equipment`.
- Raizes de escrita:
  - `rAthena/db/import`
  - `Patch/data`
- Cobre:
  - `item_db.yml`
  - tabelas TXT legado de item
  - `accessoryid.lub`
  - `accname.lub`
  - `spriterobeid.lub`
  - `spriterobename.lub`
  - `weapontable.lub`
- Rollback valida SHA-256 do estado aplicado antes de restaurar.

## NPC

- Logs: `data/logs/npcs`.
- Backups: `data/backups/npcs`.
- Raiz de escrita: `rAthena/npc`.
- Cobre script custom e loader em `npc/scripts_custom.conf`.

## Monstro

- Logs: `data/logs/monsters`.
- Backups: `data/backups/monsters`.
- Raizes de escrita:
  - `rAthena/db/import`
  - `rAthena/npc`
- Cobre:
  - `mob_db.yml`
  - `mob_avail.yml`
  - `mob_skill_db.txt`
  - script custom de spawn
  - loader em `npc/scripts_custom.conf`
  - drops normais e MVP drops com validacao por `AegisName`/ID
  - skills multiplas no layout TXT classico de 19 colunas
  - spawns multiplos com label visivel, label de evento, area e respawn
- Antes da substituicao final:
  - monta o arquivo completo em staging dentro do backup da operacao
  - valida YAML/TXT/script antes de tocar o destino real
  - bloqueia o apply se a validacao do staging falhar
- Rollback valida SHA-256 do estado aplicado antes de restaurar.

## Mapa

- Logs: `data/logs/maps`.
- Backups: `data/backups/maps`.
- Raizes de escrita:
  - `rAthena/db/import`
  - `rAthena/conf`
  - `Patch/data`
- Cobre:
  - append em `db/import/map_index.txt`
  - append em `conf/maps_athena.conf`
  - copia segura dos assets planejados para `Patch/data`
  - rebuild de `db/import/map_cache.dat` por adaptador de `mapcache.exe`
- Rollback valida SHA-256 do estado aplicado antes de restaurar.

## Comandos

```powershell
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- item apply --config data\manifests\repositories.local.json --aegis RF_Test_Item --name "RagnaForge Test Item" --resource RF_Test_Item --confirm APPLY
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- item rollback --log data\logs\items\<apply>.apply.json --confirm ROLLBACK

dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- equipment apply --config data\manifests\repositories.local.json --aegis RF_Costume_Rabbit --name "RagnaForge Costume Rabbit" --resource c_rabbit_winged_robe --type Armor --locations Head_Top --visual-category headgear --view 5000 --client-symbol ACCESSORY_RF_COSTUME_RABBIT --client-sprite _rf_costume_rabbit --confirm APPLY
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- equipment rollback --log data\logs\equipment\<apply>.apply.json --confirm ROLLBACK

dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- npc apply --config data\manifests\repositories.local.json --name "RagnaForge Guide" --map prontera --x 150 --y 180 --sprite 4_M_JOB_BLACKSMITH --confirm APPLY
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- npc rollback --log data\logs\npcs\<apply>.apply.json --confirm ROLLBACK

dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- monster apply --config data\manifests\repositories.local.json --aegis RF_TEST_MOB --name "RagnaForge Test Mob" --map prontera --level 10 --hp 1000 --amount 5 --respawn 60000 --confirm APPLY
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- monster rollback --log data\logs\monsters\<apply>.apply.json --confirm ROLLBACK
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- monster dry-run --config data\manifests\repositories.local.json --aegis RF_ADV_MOB --name "RagnaForge Advanced Mob" --map prontera --drops "item=Apple,chance=5000;item=Jellopy,chance=1000,mvp=true" --skills "id=175,lv=1,state=any,rate=9000,target=self,condition=always;id=176,lv=2,state=attack,rate=2000,target=target,condition=myhpinrate,condvalue=20,val1=60" --spawns "map=prontera,x=120,y=140,areax=8,areay=6,label=RF Advanced Spawn,event=TreeHandler::OnKill"

dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- map apply --config data\manifests\repositories.local.json --map-name sample --asset-grf-container data_0.grf --confirm APPLY
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- map rollback --log data\logs\maps\<apply>.apply.json --confirm ROLLBACK
```

## Pendencias

- Rollback com resolucao assistida quando o arquivo foi alterado manualmente depois do apply.
- Parser binario dedicado de `.rsw/.gnd` para reduzir heuristicas de string no pipeline de mapa.
- Asset copy seguro de icones/sprites de GRF para Patch ainda aguarda pipeline dedicado.
- Auditoria pre-producao ponta a ponta deve rodar dry-run/diff-preview reais antes de API/interface.

## Atualizacao 2026-05-11 - NPC client identity

- `npc apply` agora pode incluir arquivos textuais de identidade client-side no mesmo log/manifest.
- Raizes client-side permitidas para NPC: `Patch/data` e `Patch/system`.
- Arquivos textuais suportados: `jobname`, `jobidentity` e `npcidentity` em `TextLua`, `TextLub` ou `LegacyTxt`.
- `BinaryLub` e `Unknown` bloqueiam o apply completo por padrao.
- `--allow-server-only true` permite aplicar apenas `rAthena/npc` quando o lado client estiver bloqueado.
- O rollback valida SHA-256 do estado aplicado antes de restaurar scripts, loader e arquivos client-side textuais.
- Pendencia remanescente: asset copy seguro para sprite NPC encontrado apenas em GRF.

## Atualizacao 2026-05-11 - item/equipment client-side avancado

- `item apply` valida staging completo com `ApplyPostWriteValidator` antes da escrita final.
- `equipment apply` tambem valida staging completo, incluindo TXT legado e datainfo visual textual.
- `item rollback` agora valida SHA-256 do estado aplicado antes de restaurar, bloqueando drift manual.
- `BinaryLub` em `itemInfo` ou datainfo visual bloqueia apply.
- Logs continuam em `data/logs/items` e `data/logs/equipment`; backups continuam em `data/backups/items` e `data/backups/equipment`.
