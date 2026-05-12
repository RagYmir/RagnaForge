# Monster Advanced Drops Skills Spawns v1

Data: 2026-05-11

## Objetivo

Fortalecer o pipeline de monstro antes de API/interface, cobrindo:

- drops normais e MVP drops;
- multiplas skills;
- multiplos spawns com label, evento e area;
- validacao sintatica pos-write em staging;
- apply/rollback mais seguro para uso real.

## O que foi implementado

- `MonsterDryRunReport` agora expoe:
  - `Drops`
  - `Skills`
  - `Spawns`
  - `UnsupportedFields`
  - `ValidationWarnings`
  - `ValidationErrors`
  - `ApplyReadiness`
  - `PostWriteValidationPlan`
- `monster diff-preview` agora devolve esses metadados junto do diff.
- `monster dry-run` agora aceita:
  - `--drops`
  - `--skills`
  - `--spawns`
- `MonsterApplyService` agora:
  - monta o arquivo final em staging dentro de `data/backups/monsters/<op>/staging`
  - executa validacao sintatica antes da substituicao final
  - grava o resumo da validacao no log de apply
  - bloqueia o apply quando o staging falha

## Drops suportados

- Item por `AegisName`
- Item por ID
- Multiplos drops
- MVP drops
- Chance
- `kind=stealprotected`

## Drops ainda nao suportados

- Quantity real no `mob_db.yml` atual
- Campos extras nao mapeados do drop

Quando quantity diferente de `1` aparece, o dry-run bloqueia e explica a limitacao.

## Skills suportadas

Formato atual suportado:

- `mob_skill_db.txt` classico de 19 colunas
- multiplas skills por monstro
- `state`
- `target`
- `condition`
- `condition value`
- `val1..val5`
- `emotion`
- `chat` textual seguro
- `anchor`

## Skills ainda nao suportadas

- formatos alternativos fora do TXT classico local
- campos desconhecidos nao mapeados
- qualquer fluxo que exija editar `.lub` bytecode

Campos desconhecidos entram em `UnsupportedFields` e bloqueiam o apply.

## Spawns suportados

- mapa
- quantidade
- coordenada fixa
- area
- respawn
- label visivel
- label de evento textual
- multiplos spawns
- spawn randomico por coordenadas zeradas / area zerada

## Spawns ainda nao suportados

- geracao automatica do handler de evento
- variancia separada de respawn
- validacao geometrica profunda contra `.gat`

## Validacao pos-write

Validadores adicionados:

- `YamlSyntaxValidator`
- `RathenaTxtValidator`
- `RathenaScriptValidator`
- `LuaTextValidator`
- `ApplyPostWriteValidator`

Cobertura usada nesta rodada:

- `mob_db.yml`
- `mob_avail.yml`
- `mob_skill_db.txt`
- script custom de spawn
- `npc/scripts_custom.conf`

## Comandos de teste

```powershell
dotnet build RagnaForge.slnx
dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj

dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- monster dry-run --config data\manifests\repositories.local.json --aegis RF_ADV_MOB --name "RagnaForge Advanced Mob" --map prontera --drops "item=Apple,chance=5000;item=Jellopy,chance=1000,mvp=true" --skills "id=175,lv=1,state=any,rate=9000,target=self,condition=always;id=176,lv=2,state=attack,rate=2000,target=target,condition=myhpinrate,condvalue=20,val1=60" --spawns "map=prontera,x=120,y=140,areax=8,areay=6,label=RF Advanced Spawn,event=TreeHandler::OnKill"

dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- monster apply --config data\manifests\repositories.local.json --aegis RF_ADV_MOB --name "RagnaForge Advanced Mob" --map prontera --confirm APPLY

dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- monster rollback --log data\logs\monsters\<apply>.apply.json --confirm ROLLBACK
```

## Riscos restantes

- identidade client-side de NPC custom ainda esta fora deste bloco;
- `.lub` bytecode continua bloqueado sem estrategia segura;
- mapa ainda depende de parser binario mais forte para hardening completo;
- monster ainda nao gera automaticamente handlers de evento.

## Proximo passo recomendado

Fechar estrategia segura para `jobname`, `jobidentity` e `npcidentity` antes de avancar para client-side mais amplo, API ou interface.
