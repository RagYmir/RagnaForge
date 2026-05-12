# 2026-05-07 - Entity diff previews v1

## Resumo

Foram adicionados diff previews iniciais para NPC, monstro e mapa, mantendo tudo em modo proposta.

## Comandos

- `npc dry-run`
- `npc diff-preview`
- `monster dry-run`
- `monster diff-preview`
- `map dry-run`
- `map diff-preview`

## Cobertura atual

### NPC

- cria proposta de arquivo em `npc/custom`;
- cria proposta de carga em `npc/scripts_custom.conf`;
- valida mapa no rAthena;
- acusa ausência de mapa loose no Patch como warning.

### Monstro

- cria proposta em `db/import/mob_db.yml`;
- cria proposta opcional em `db/import/mob_avail.yml`;
- cria proposta de spawn em `npc/custom`;
- cria proposta de carga em `npc/scripts_custom.conf`;
- aloca ID livre quando necessário.

### Mapa

- cria proposta em `db/import/map_index.txt`;
- cria proposta em `conf/maps_athena.conf`;
- valida `.rsw`, `.gnd` e `.gat`;
- usa lookup GRF index-first quando disponível;
- expõe proveniência do match de mapa em `RswLookup`, `GndLookup` e `GatLookup`;
- registra warning sobre rebuild de `map_cache.dat`.

## Validacao

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- smoke real:
  - `npc diff-preview --config data\manifests\repositories.local.json --name "RagnaForge Guide" --map prontera --x 150 --y 180 --dir 2 --sprite 4_M_JOB_BLACKSMITH`
  - `monster diff-preview --config data\manifests\repositories.local.json --aegis RF_TEST_MOB --name "RagnaForge Test Mob" --map prontera --level 10 --hp 1000 --amount 5 --respawn 60000 --sprite PORING`
  - `map dry-run --config data\manifests\repositories.local.json --map-name prontera --asset-grf-container data_0.grf`

## Resultado atual

- NPC e monstro já retornam diff estruturado real contra arquivos atuais;
- mapa já resolve assets em GRF para `.rsw`, `.gnd` e `.gat`;
- tudo continua sem escrita real em rAthena, Patch ou GRFs.
