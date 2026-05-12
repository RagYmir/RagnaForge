# 2026-05-11 - Monster apply and rollback

## Objetivo

Adicionar apply/rollback transacional para monstros, cobrindo os arquivos gerados pelo dry-run atual.

## Implementacao

- Novo `MonsterApplyService` em `Infrastructure/Monsters`.
- Novos modelos `MonsterApplyLog`, `MonsterApplyResult` e `MonsterRollbackResult`.
- CLI ampliada com:
  - `monster apply --confirm APPLY`
  - `monster rollback --confirm ROLLBACK --log <arquivo>`
- Logs em `data/logs/monsters`.
- Backups em `data/backups/monsters`.
- Escrita permitida somente dentro de:
  - `rAthena/db/import`
  - `rAthena/npc`

## Arquivos Cobertos

- `db/import/mob_db.yml`
- `db/import/mob_avail.yml`
- `db/import/mob_skill_db.txt`
- `npc/scripts_custom.conf`
- `npc/custom/ragnaforge_mob_*.txt`

## Conflitos Bloqueados

- Alvo fora das raizes permitidas.
- Existencia do alvo mudou desde o dry-run.
- `create` em arquivo existente.
- Append duplicado por match exato.
- Append duplicado por match com normalizacao de quebra de linha.
- Linha-ancora ja presente.
- Monster ID ja existente.
- `AegisName` ja existente.
- Entrada `mob_avail` ja existente.
- Entrada `mob_skill_db` ja existente para ID ou `AegisName@RagnaForge`.
- Loader de spawn ja presente em `npc/scripts_custom.conf`.

## Rollback

- Lê o log de apply.
- Aceita apenas logs com status `Applied`.
- Valida SHA-256 do estado aplicado antes de restaurar.
- Restaura backups de arquivos existentes.
- Remove arquivos criados pelo apply.
- Recusa rollback cego se o alvo mudou manualmente depois do apply.

## Limitacoes

- Drops complexos ainda nao entram no dry-run/apply.
- Client-side de sprite/nome de monstro ainda nao e escrito.
- `mob_skill_db.txt` ainda cobre um subconjunto basico de colunas.
- Spawn/evento avancado ainda precisa de mapeamento adicional.

## Validacao

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj`
- Smoke CLI sem confirmacao retornou exit code `2` e nao criou novos logs.
