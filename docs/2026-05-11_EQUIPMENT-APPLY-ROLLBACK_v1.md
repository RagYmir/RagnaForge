# Equipment Apply / Rollback

Data: 2026-05-11
Status: concluido para o primeiro corte transacional de equipamento

## Objetivo

Transformar o `equipment dry-run` em apply/rollback seguro, sem tocar GRFs originais e sem escrever fora do workspace, rAthena import e Patch/data.

## Cobertura desta entrega

- `item_db.yml` em `rAthena/db/import`
- tabelas TXT legado de item no Patch
- datainfo visual:
  - `accessoryid.lub`
  - `accname.lub`
  - `spriterobeid.lub`
  - `spriterobename.lub`
  - `weapontable.lub`
- backup em `data/backups/equipment`
- log em `data/logs/equipment`
- rollback por manifest de apply

## Regras de seguranca

- apply exige `--confirm APPLY`
- rollback exige `--confirm ROLLBACK`
- escrita limitada a:
  - `rAthena/db/import`
  - `Patch/data`
- validacao de hash pos-write
- rollback bloqueia restauracao cega se o arquivo mudou depois do apply

## Conflitos bloqueados

- alvo fora das raizes permitidas
- mudanca de existencia desde o dry-run
- `create` em arquivo ja existente
- append exato/normalizado/linha ancora ja presente
- ID ja usado no `item_db`
- `AegisName` ja usado no `item_db`
- ID ja presente nas tabelas TXT legado
- simbolo visual ja presente em datainfo
- `ViewID` ja presente em datainfo

## Limitacoes mantidas

- shield visual custom continua bloqueado; apenas `View` embutido
- nao copia sprites/ACT para o Patch nesta fase
- nao manipula `.lub` bytecode nao legivel
- ainda depende do dry-run para decidir as propostas exatas

## Validacao

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- cobertura adicionada:
  - apply/rollback de headgear
  - colisao de `ViewID` introduzida depois do dry-run
  - alvo fora das raizes permitidas
