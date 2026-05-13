# 2026-05-11 - Pre-production audit v1

## Resumo executivo

Auditoria pre-producao executada em modo seguro/read-only para os pipelines de item, equipamento, NPC, monstro, mapa, GRF/indexacao, client-side e guards de apply/rollback.

Resultado geral:

- API read-only, dry-run e diff-preview estao tecnicamente liberaveis como proximo passo.
- API apply/rollback ainda deve nascer bloqueada por confirmacao forte, permissao explicita e revisao humana.
- Nenhum `--confirm APPLY` ou `--confirm ROLLBACK` foi usado.
- Nenhum apply real foi executado.
- Nenhum rollback real foi executado.
- Nenhum `.lub` bytecode foi editado.
- Nenhuma GRF original foi alterada.
- Amostra de 25 arquivos externos criticos em rAthena/Patch/GRF manteve `Length` e `LastWriteTimeUtc` inalterados.

## Escopo

Categorias auditadas:

1. Configuracao geral.
2. GRF/indexacao.
3. Item.
4. Equipamento.
5. NPC.
6. Monstro.
7. Mapa.
8. Client-side.
9. Seguranca de apply/rollback.
10. Prontidao para API.

## O que foi testado

- `config validate`
- `discover`
- `grf index`
- `grf inspect`
- `item dry-run`
- `item diff-preview`
- `equipment dry-run`
- `equipment diff-preview`
- `npc dry-run`
- `npc diff-preview`
- `monster dry-run`
- `monster diff-preview`
- `map dry-run`
- `map diff-preview`
- guards de apply sem `--confirm APPLY`
- guards de rollback sem `--confirm ROLLBACK`
- `dotnet build`
- suite automatizada de testes

## O que nao foi testado

- Apply real em rAthena/Patch.
- Rollback real em rAthena/Patch.
- Edicao, decompilacao ou recompilacao de `.lub` bytecode.
- Copia de assets de GRF para Patch.
- API backend.
- Interface administrativa.
- Mapa novo 100% aplicavel em ambiente real; os mapas reais auditados ficaram bloqueados por existencia, ambiguidades ou dependencias ausentes.

## Comandos executados

```powershell
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- config validate --config data\manifests\repositories.local.json
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- discover --config data\manifests\repositories.local.json --max-grf-containers 20
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- grf index --config data\manifests\repositories.local.json --cache data\cache\preprod-grf-repository.index.json --max-containers 20
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- grf index --config data\manifests\repositories.local.json --cache data\cache\preprod-grf-repository-full.index.json --max-containers 200
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- grf inspect --config data\manifests\repositories.local.json --container data_0.grf --cache data\indexes\preprod-data_0.index.json --limit 50 --force
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- item dry-run ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- item diff-preview ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- equipment dry-run ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- equipment diff-preview ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- npc dry-run ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- npc diff-preview ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- monster dry-run ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- monster diff-preview ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- map dry-run ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- map diff-preview ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- item apply ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- equipment apply ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- npc apply ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- monster apply ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- map apply ...
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- item rollback --log data\logs\items\fake.apply.json
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- equipment rollback --log data\logs\equipment\fake.apply.json
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- npc rollback --log data\logs\npcs\fake.apply.json
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- monster rollback --log data\logs\monsters\fake.apply.json
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- map rollback --log data\logs\maps\fake.apply.json
dotnet build RagnaForge.slnx
dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj
```

Os comandos `apply` e `rollback` foram executados sem confirmacao de proposito, apenas para validar guards. Todos retornaram exit code `2` antes de qualquer escrita.

## Resultado por categoria

### 1. Configuracao geral

- `config validate`: valido, com warning `client_date.unknown`.
- `discover`: caminhos reais resolvidos.
- rAthena: `<RATHENA_PATH>`.
- Patch: `<PATCH_PATH>`.
- GRFs: `<GRF_REPOSITORY_PATH>`.
- GRF Editor: `<GRF_EDITOR_PATH>`.
- `discover` detectou `ClientDate = 2025-07-16` a partir de `2025-07-16_Ragexe_175220998_clientinfo_patched.exe`.

Conclusao: configuracao esta operacional, mas o manifest ainda nao persiste o client date detectado.

### 2. GRF/indexacao

- `grf index --max-containers 20`: 20 containers, limite atingido.
- `grf index --max-containers 200`: 38 containers, limite nao atingido.
- `grf inspect data_0.grf`: 131185 entradas, captura truncada em 50 para auditoria.
- Extensoes relevantes em `data_0.grf`: `.bmp`, `.act`, `.spr`, `.tga`, `.rsm`, `.wav`, `.str`, `.pal`, `.rsm2`, `.gat`, `.rsw`, `.gnd`, `.lub`.

Conclusao: indexacao esta pronta para API read-only/dry-run. Para producao, usar cache completo de 38 containers como base e evitar scan ao vivo em fluxo interativo quando houver cache valido.

### 3. Item

Casos auditados:

| Caso | Resultado |
| --- | --- |
| Item simples Etc | `CanApply = true`, client `Hybrid`, 7 propostas |
| Item com asset solto no Patch | `CanApply = true`, client `Hybrid`, 7 propostas |
| Item com asset em GRF | `CanApply = true`, match GRF por `LiveScanFallback`, 4 matches |
| Client hibrido | `CanApply = true`, `ClientSideMode = Hybrid` |
| ID/AegisName conflitante com Apple/512 | bloqueado, exit code `1`, `CanApply = false` |
| Asset ausente | nao bloqueou item Etc; gerou warnings |

Conclusao: item esta pronto para API read-only, dry-run e diff-preview. API apply deve exigir politica explicita sobre asset obrigatorio por tipo de item, porque item Etc com asset ausente ainda fica aplicavel com warning.

### 4. Equipamento

Casos auditados:

| Caso | Resultado |
| --- | --- |
| Headgear | `CanApply = true`, `VisualClientSidePlan` com `accessoryid:TextLub`, `accname:TextLub` |
| Robe | `CanApply = true`, `spriterobeid:TextLub`, `spriterobename:TextLub` |
| Weapon | `CanApply = true`, `weapontable:TextLub` |
| Shield restrito | `CanApply = true`, sem datainfo visual novo |
| Shield custom | bloqueado, exit code `1` |
| ViewID duplicado | bloqueado, exit code `1` |
| Simbolo inseguro | bloqueado, exit code `1` |

Conclusao: equipamento esta pronto para API read-only, dry-run e diff-preview. API apply pode existir depois, mas deve manter `--confirm APPLY`, bloqueios de `ViewID`, simbolo e shield custom.

### 5. NPC

Casos auditados:

| Caso | Resultado |
| --- | --- |
| Sprite padrao `4_M_JOB_BLACKSMITH` | `CanApply = true`, `ApplyReadiness = Ready`, 2 hunks server-side |
| Sprite existente e ja registrado `4_AIRA` | `CanApply = true`, identidade existente |
| Sprite custom solto com identidade nova `4_CHUNITHM` | `CanApply = true`, 3 registros client-side propostos em TextLub, 5 hunks |
| Sprite resolvido apenas por GRF | bloqueado para full apply, `ReadyServerOnly`, asset copy pendente |
| Ambiguidade/bytecode | coberto por testes automatizados; nao foi forjado no Patch real |

Conclusao: NPC esta pronto para API read-only, dry-run e diff-preview. API apply deve manter bloqueio default quando identidade client-side estiver insegura e expor `allow-server-only` como decisao explicita.

### 6. Monstro

Casos auditados:

| Caso | Resultado |
| --- | --- |
| Monstro simples | `CanApply = true`, `ApplyReadiness = Ready`, 3 propostas |
| Monstro com `mob_avail` | `CanApply = true`, 4 propostas |
| Multiplos drops + MVP drop | `CanApply = true`, 2 drops |
| Multiplas skills + spawns em multiplos mapas | `CanApply = true`, 2 skills, 3 spawns |
| Drop inexistente | bloqueado, `Drop item 'RF_NO_SUCH_ITEM' was not found` |
| Skill invalida | bloqueado, `Skill ID 999999 was not found` |
| Mapa inexistente | bloqueado, `Spawn map 'no_such_map_zz' is not registered` |

Conclusao: monstro esta pronto para API read-only, dry-run, diff-preview e, apos autenticacao/confirmacao forte, apply controlado.

### 7. Mapa

Casos auditados:

| Caso | Resultado |
| --- | --- |
| `prontera` existente | bloqueado por mapa ja registrado e dependencias ambiguas |
| `1@colo` real solto | bloqueado por modelos/texturas ausentes ou ambiguos |
| mapa inexistente | bloqueado por trio `.rsw/.gnd/.gat` ausente |
| rename binario `rf_audit_rename` -> `prontera.*` | bloqueado por rename binario |

Detalhes:

- `prontera`: `AssetPlans = 28`, `NeedsCopy = 10`, map cache tool detectado.
- `1@colo`: `AssetPlans = 54`, `NeedsCopy = 4`, bloqueios por referencias de modelo/textura.
- `no_such_map_zz`: trio core ausente.
- `rf_audit_rename`: bloqueio de rename binario confirmado.

Conclusao: mapa esta pronto para API read-only, dry-run e diff-preview. API apply de mapa deve ficar bloqueada por padrao ate haver um caso real positivo sem ambiguidade ou uma tela de decisao manual de assets.

### 8. Client-side

Modo atual do Patch:

- `ClientSideMode = Hybrid`.
- `system/iteminfo_true.lub`: `TextLub`.
- TXT legado completo: `LegacyTxt`.
- `accessoryid.lub`: `TextLub`.
- `accname.lub`: `TextLub`.
- `spriterobeid.lub`: `TextLub`.
- `spriterobename.lub`: `TextLub`.
- `weapontable.lub`: `TextLub`.
- `jobname.lub`: `TextLub`.
- `jobidentity.lub`: `TextLub`.
- `npcidentity.lub`: `TextLub`.

Estrategia segura atual:

- Item/equipamento em client hibrido: escrever TXT legado completo; tratar `itemInfo` textual como contexto/read-only.
- Datainfo visual textual: permitir staging/diff/apply futuro com confirmacao.
- NPC identity textual: permitir staging/diff/apply futuro com confirmacao.
- `BinaryLub` e `Unknown`: bloqueio.
- Asset copy de GRF para Patch: bloqueio ate pipeline dedicado.

### 9. Seguranca de apply/rollback

Todos os guards foram testados sem confirmacao:

| Comando | Exit code | Resultado |
| --- | ---: | --- |
| item apply | 2 | recusado por falta de `--confirm APPLY` |
| equipment apply | 2 | recusado por falta de `--confirm APPLY` |
| npc apply | 2 | recusado por falta de `--confirm APPLY` |
| monster apply | 2 | recusado por falta de `--confirm APPLY` |
| map apply | 2 | recusado por falta de `--confirm APPLY` |
| item rollback | 2 | recusado por falta de `--confirm ROLLBACK` |
| equipment rollback | 2 | recusado por falta de `--confirm ROLLBACK` |
| npc rollback | 2 | recusado por falta de `--confirm ROLLBACK` |
| monster rollback | 2 | recusado por falta de `--confirm ROLLBACK` |
| map rollback | 2 | recusado por falta de `--confirm ROLLBACK` |

Contagem de logs/backups antes e depois dos guards:

- Logs: 1 -> 1.
- Backups: 1 -> 1.

Conclusao: guards estao funcionando e nao criaram log/backup indevido.

## Matriz de prontidao

| Categoria | ReadyForApiReadOnly | ReadyForApiDryRun | ReadyForApiDiffPreview | ReadyForApiApply | ReadyForApiRollback | NeedsManualDecision | BlockedReason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| item | sim | sim | sim | parcial | parcial | sim | politica de asset obrigatorio e confirmacao forte |
| equipment | sim | sim | sim | parcial | parcial | sim | visual assets e shield custom continuam com gates |
| npc | sim | sim | sim | parcial | parcial | sim | GRF-only sprite exige asset copy; server-only exige decisao explicita |
| monster | sim | sim | sim | parcial | parcial | sim | apply deve ficar atras de confirmacao/autorizacao forte |
| map | sim | sim | sim | nao | parcial | sim | mapas reais auditados ficaram bloqueados por ambiguidade/dependencias/rename |
| grf | sim | sim | nao aplicavel | nao | nao | sim | full index deve ser preferido; extracao/copia ainda precisa pipeline |
| client-side | sim | sim | sim | parcial | parcial | sim | bytecode e unknown bloqueados; hibrido precisa estrategia explicita |

## Tabela de riscos

| Severidade | Risco | Mitigacao |
| --- | --- | --- |
| Baixo | Manifest ainda marca `ClientDate` como desconhecido | API deve chamar `discover` ou atualizar manifest apos confirmacao |
| Baixo | GRF inspect de auditoria foi truncado em 50 entradas | Cache completo de repositorio ja encontrou 38 containers; usar indices por demanda |
| Medio | Item Etc com asset ausente fica aplicavel com warning | Definir politica por tipo: asset obrigatorio para item/equip visual, opcional para outros |
| Medio | Client hibrido pode variar por client date | Exigir estrategia explicita no endpoint antes de apply |
| Medio | NPC sprite em GRF nao vira funcional sem copy para Patch | Criar pipeline de asset copy antes de liberar full apply nesse caso |
| Alto | Mapas reais possuem dependencias ambiguas ou ausentes | Manter map apply bloqueado em API ate resolver ambiguidade de assets |
| Alto | Rename binario de mapa nao e suportado | Manter bloqueio e exigir resource names alinhados |
| Bloqueante | `.lub` bytecode | Continuar bloqueado ate existir estrategia segura aprovada |

## Bloqueios encontrados

- `client_date.unknown` no manifest local, apesar do `discover` detectar `2025-07-16`.
- Mapas reais auditados nao ficaram prontos para apply por dependencia/ambiguidade/existencia/rename.
- Item com asset ausente nao bloqueia em todos os casos, apenas gera warning; isso precisa politica antes de apply via API.
- Sprite NPC resolvido apenas em GRF exige pipeline futuro de asset copy.
- `.lub` bytecode segue bloqueado por regra de seguranca.

## Dependencias pendentes

- Pipeline de asset copy GRF -> Patch com diff, backup e rollback.
- Politica de asset obrigatorio por categoria de item/equipamento.
- Persistencia ou confirmacao do `ClientDate` detectado.
- Resolucao assistida de ambiguidades de assets de mapa.
- Parser binario dedicado de `.rsw/.gnd` para reduzir heuristica de string.
- Estrategia futura para bytecode `.lub`, somente se indispensavel.

## Confirmacoes de seguranca

- Nenhum apply real foi executado.
- Nenhum rollback real foi executado.
- Nenhum comando com `--confirm APPLY` foi executado.
- Nenhum comando com `--confirm ROLLBACK` foi executado.
- Nenhum repositorio externo foi alterado nos 25 alvos criticos amostrados.
- Nenhuma GRF original foi alterada.
- Arquivos gerados ficaram no workspace: caches de auditoria em `data/cache`, `data/indexes`, baseline em `tmp` e este relatorio em `docs`.

## Recomendações antes da API

1. API deve iniciar apenas com endpoints read-only, dry-run e diff-preview.
2. Endpoints de apply/rollback devem ser criados bloqueados ou atras de confirmacao forte, auditoria, autenticacao e permissao explicita.
3. Map apply nao deve ser liberado na primeira versao da API de producao; deixar como dry-run/diff ate resolver ambiguidades reais.
4. Persistir ou confirmar `ClientDate = 2025-07-16` no manifest antes de automatizar client-side.
5. Criar politica de assets obrigatorios antes de permitir item/equipment apply por API.

## Criterios para liberar API backend

- Expor primeiro endpoints:
  - `GET /status`
  - `GET /config`
  - `POST /discover`
  - `POST /grf/index`
  - `POST /item/dry-run`
  - `POST /item/diff-preview`
  - `POST /equipment/dry-run`
  - `POST /equipment/diff-preview`
  - `POST /npc/dry-run`
  - `POST /npc/diff-preview`
  - `POST /monster/dry-run`
  - `POST /monster/diff-preview`
  - `POST /map/dry-run`
  - `POST /map/diff-preview`
- Apply/rollback endpoints so devem ser liberados quando:
  - houver autenticacao;
  - houver confirmacao explicita;
  - houver diff revisado;
  - houver log/backup/rollback manifest;
  - houver bloqueio de bytecode/unknown;
  - houver protecao contra escrita fora das raizes permitidas.

## Proximo passo recomendado

Iniciar API backend em modo seguro, read-only/dry-run/diff-preview primeiro, mantendo apply/rollback fora da primeira liberacao ou bloqueados por feature flag ate nova decisao.
