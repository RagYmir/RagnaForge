# 2026-05-11_MAP-APPLY-ROLLBACK_v1

## Objetivo

Fechar o bloco inicial de mapa com:

- `map dry-run` endurecido;
- `AssetPlans` e `MapCachePlan` no relatorio;
- `map apply/rollback` transacional;
- rebuild de `map_cache.dat` encapsulado em adaptador;
- testes e smoke de seguranca.

## Entregas

- `MapDryRunReport` agora carrega:
  - `AssetPlans`
  - `MapCachePlan`
- `MapApplyService` implementado.
- `RathenaMapCacheBuilder` implementado.
- CLI com:
  - `map apply --confirm APPLY`
  - `map rollback --confirm ROLLBACK --log <arquivo>`

## Regras aplicadas

- Escrita limitada a:
  - `rAthena/db/import`
  - `rAthena/conf`
  - `Patch/data`
- Assets GRF sao extraidos para raiz temporaria controlada.
- `map_cache.dat` e reconstruido em staging antes da substituicao final.
- Rollback valida SHA-256 antes de restaurar.
- Rename binario entre `MapName` e resource names fica bloqueado nesta fase.

## Validacao

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- Smoke real resumido:
  - `map dry-run --map-name prontera --asset-grf-container data_0.grf`
  - resultado: `CanApply = false`, porque `prontera` ja existe e porque `18` dependencias GRF ficaram ambiguas; `AssetPlans = 28`; `NeedsCopy = 10`; `MapCacheTool = true`
- Smoke de seguranca:
  - `map apply` sem `--confirm APPLY` retorna `2`

## Limites atuais

- Parser binario dedicado de `.rsw/.gnd` ainda nao existe.
- Dependencias referenciadas com match GRF ambiguo ficam bloqueadas ate existir lookup mais preciso por caminho.
- O bloco nao cria automaticamente warps, mapflags, NPCs ou spawns vinculados ao mapa.
- O apply real em repositorios externos continua dependente de confirmacao explicita do usuario.
