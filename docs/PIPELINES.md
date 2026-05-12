# Pipelines de implementacao

Data: 2026-05-11
Status: Pipelines principais com dry-run, diff-preview, apply/rollback protegido e client-side avancado de item/equipamento consolidado.

## Pipeline global

1. Discovery de repositorios.
2. Probe do GRF Editor.
3. Scan de GRFs/Patch.
4. Scan rAthena.
5. Detectar perfil de episodio/progressao.
6. Detectar Renewal/Pre-Renewal dentro do perfil ativo.
7. Detectar client date/sistema client-side.
8. Resolver dependencias.
9. Alocar IDs.
10. Gerar dry-run.
11. Gerar diff preview.
12. Revisar riscos.
13. Confirmar apply.
14. Backup.
15. Escrita transacional.
16. Validacao pos-write.
17. Log e rollback.

## Gate pre-API

Antes de expor os pipelines em API/backend:

1. Executar auditoria read-only de configuracao, GRF, item, equipamento, NPC, monstro, mapa e client-side.
2. Confirmar que dry-run e diff-preview retornam relatorios completos.
3. Confirmar que apply/rollback sem confirmacao retornam exit code `2`.
4. Confirmar que nenhum repositorio externo foi alterado.
5. Liberar primeiro apenas endpoints read-only/dry-run/diff-preview.
6. Manter apply/rollback bloqueados por confirmacao forte, feature flag ou decisao manual ate nova revisao.

## Criar item

1. Receber dados.
2. Detectar arquivo server-side alvo.
3. Verificar ID e AegisName.
4. Resolver assets client-side.
5. Resolver `ItemInfo` ou tabelas antigas.
6. Gerar entrada YAML em import/custom.
7. Gerar entrada client-side.
8. Validar scripts e paths.
9. Dry-run.
10. Diff preview especializado por arquivo alvo.
11. Apply confirmado.

## Editar item

1. Localizar item.
2. Mostrar origem dos dados.
3. Separar server/client/assets.
4. Gerar patch incremental.
5. Validar dependencias alteradas.
6. Dry-run, diff, backup, apply.

## Criar equipamento

1. Executar pipeline de criar item.
2. Resolver propriedades extras de equipamento.
3. Resolver ViewID/ViewSprite.
4. Validar location/jobs/gender/level/slots.
5. Validar sprites visuais e tabelas client-side.
6. Gerar dry-run completo.
7. Gerar diff preview especializado.
8. Apply exige `--confirm APPLY`, grava backup/log e fica restrito a `rAthena/db/import` e `Patch/data` nesta fase.
9. Rollback exige `--confirm ROLLBACK`, valida hashes aplicados e usa o log de apply.

### Gates atuais de equipamento

- `headgear/accessory`: validar `accessoryid.lub`, `accname.lub`, prefixo `ACCESSORY_`, `ViewID` e sprite visual.
- `robe`: validar `spriterobeid.lub`, `spriterobename.lub`, prefixo `ROBE_`, `ViewID` e sprite visual.
- `weapon`: validar `weapontable.lub`, prefixo `WEAPONTYPE_`/`WPCLASS_`, `--weapon-base-type`, location `Right_Hand`/`Both_Hand`, `ViewID` e sprite visual.
- `shield`: aceitar apenas `View` embutido `1..6`, exigir `Left_Hand` e nao gerar datainfo `.lub`.
- `shield` com `client-symbol` ou `client-sprite` deve bloquear, porque isso indicaria tentativa de visual custom ainda nao mapeado.
- Qualquer simbolo Lua inseguro, string insegura, location desconhecida ou `ViewID` duplicado bloqueia `CanApply`.

## Editar equipamento

1. Localizar item base.
2. Localizar dependencias de equipamento.
3. Validar alteracoes visuais.
4. Gerar diff server/client.
5. Apply somente com rollback.

## Criar NPC

1. Escolher sprite.
2. Validar sprite no Patch/client.
3. Validar mapa e coordenadas.
4. Gerar script em pasta custom.
5. Registrar script em `.conf` carregado.
6. Validar sintaxe basica.
7. Dry-run/diff/apply.
8. Apply exige `--confirm APPLY`, grava backup/log e fica restrito a `rAthena/npc` nesta fase.
9. Rollback exige `--confirm ROLLBACK` e usa o log de apply.

## Editar NPC

1. Localizar NPC por mapa/nome/script/sprite.
2. Mostrar arquivo origem.
3. Editar dialogo/logica/posicao/sprite.
4. Revalidar mapa/sprite.
5. Diff e rollback.

## Criar monstro

1. Receber stats, drops, skills e spawn.
2. Verificar ID e AegisName.
3. Validar sprite e client-side.
4. Gerar `mob_db`.
5. Gerar `mob_avail` se necessario.
6. Gerar `mob_skill_db` se necessario.
7. Gerar spawn custom.
8. Validar mapa/spawn/drops.
9. Dry-run/diff/apply.
10. Apply exige `--confirm APPLY`, grava backup/log e fica restrito a `rAthena/db/import` e `rAthena/npc` nesta fase.
11. Rollback exige `--confirm ROLLBACK`, valida hashes aplicados e usa o log de apply.

## Editar monstro

1. Localizar mob, skills, availability e spawns.
2. Mostrar origem.
3. Alterar stats/sprite/drops/skills/spawn.
4. Revalidar dependencias.
5. Diff e rollback.

## Implantar mapa

1. Selecionar mapa em GRF/Patch.
2. Parsear `.rsw`, `.gnd`, `.gat`.
3. Resolver texturas/modelos/sons/efeitos.
4. Expor `AssetPlans` com origem do asset, alvo no Patch e necessidade de copia.
5. Bloquear rename binario quando `MapName` divergir dos resource names de `.rsw/.gnd/.gat`.
6. Validar assets ausentes.
7. Registrar mapa no rAthena.
8. Atualizar map_index de forma append-only.
9. Gerar/atualizar map_cache com ferramenta local em staging antes de substituir o alvo final.
10. Dry-run com diff textual, plano de assets e plano de map cache.
11. Apply exige `--confirm APPLY`, grava backup/log e fica restrito a `rAthena/db/import`, `rAthena/conf` e `Patch/data`.
12. Rollback exige `--confirm ROLLBACK`, valida hashes aplicados e usa o log de apply.

## Pipeline de seguranca

- Sem dry-run, nao ha apply.
- Sem backup, nao ha escrita.
- Sem dependency graph, nao ha diff.
- Sem confirmacao, nao ha alteracao em rAthena/Patch.
- Sem rollback plan, nao ha apply.
- Sem perfil de episodio registrado, nao ha apply.

## Atualizacao 2026-05-07

- `item apply` agora possui preflight de conflitos, auditoria por etapa e log mesmo quando o apply e bloqueado.
- `equipment apply/rollback` agora existe para `item_db.yml`, tabelas TXT legado e datainfo visual, com escrita limitada a `rAthena/db/import` e `Patch/data`.
- `monster dry-run` agora expoe `Drops`, `Skills`, `Spawns`, `UnsupportedFields`, `ValidationWarnings`, `ValidationErrors`, `ApplyReadiness` e `PostWriteValidationPlan`.
- `monster diff-preview` agora pode incluir drops normais/MVP, multiplas linhas de `mob_skill_db.txt` e spawn com area/coordenadas/evento mais completos.
- `monster apply/rollback` agora existe para `mob_db.yml`, `mob_avail.yml`, `mob_skill_db.txt`, loader e spawn custom, com escrita limitada a `db/import` e `npc`, staging validado e rollback automatico em falha.
- `npc dry-run` agora contem gate proprio de validacao de sprite client-side para separar sprite padrao de sprite nao padrao.
- `npc dry-run` agora preserva no `DetectionSource` quando um sprite custom foi resolvido via GRF por `local-index`, `live-scan-fallback` ou `live-scan`.
- `npc apply/rollback` agora existe para scripts e loader custom, com escrita limitada a `rAthena/npc`.
- `map dry-run` agora faz scan profundo de dependencias quando o mapa estiver acessivel como arquivo solto no Patch.
- `map dry-run` tambem faz scan profundo quando `.rsw/.gnd` estiverem apenas em GRF, usando extracao temporaria controlada e read-only via GRF Editor.
- `map dry-run` agora expoe `AssetPlans` e `MapCachePlan`.
- `map apply/rollback` agora existe com extracao controlada de assets GRF, rebuild de `map_cache.dat` em staging e rollback validado por hash.
- Shield visual custom nao segue pipeline proprio nesta fase: quando o client apontar robe tables, o fluxo oficial e `robe`/`Costume_Garment`.

## Atualizacao 2026-05-11 - NPC client identity

- `npc dry-run` agora detecta `jobname`, `jobidentity` e `npcidentity` no Patch real.
- Cada arquivo e classificado como `Missing`, `TextLua`, `TextLub`, `BinaryLub`, `LegacyTxt` ou `Unknown`.
- `TextLua`, `TextLub` e `LegacyTxt` podem receber diff/apply quando a identidade client-side for necessaria.
- `BinaryLub` e `Unknown` bloqueiam apply client-side; nao ha descompilacao, conversao ou sobrescrita de bytecode.
- `NpcClientIdentityPlan` expoe sprite resolvido, proveniencia, arquivos detectados, formatos, registros existentes, registros propostos, bloqueios, warnings, errors e readiness.
- `npc diff-preview` gera hunks client-side apenas para arquivos textuais.
- `npc apply` aplica server-side e client-side textual em staging validado, mantendo `--confirm APPLY`.
- `--allow-server-only` permite aplicar apenas script/loader quando o client-side estiver bloqueado, registrando que o NPC pode nao ficar funcional no client.
- `npc rollback` restaura tambem `jobname`, `jobidentity` e `npcidentity` textuais alterados, validando SHA-256.

## Atualizacao 2026-05-11 - Client-side itemInfo/hibrido

- `item dry-run` agora cria `ClientSidePlan` e detecta `ItemInfo`, TXT legado e modo hibrido.
- `ClientSideMode` pode ser `ItemInfo`, `LegacyTxt`, `Hybrid` ou `Unknown`.
- `itemInfo.lua`, `itemInfo.lub`, tabelas TXT e datainfo visual sao classificados como `TextLua`, `TextLub`, `BinaryLub`, `LegacyTxt`, `Missing` ou `Unknown`.
- `item diff-preview` so gera hunk para arquivos textuais seguros.
- `item apply` valida staging de YAML/TXT/Lua antes de escrever.
- `equipment dry-run` reutiliza `ClientSidePlan` para item base e `VisualClientSidePlan` para datainfo visual.
- `equipment diff-preview/apply` bloqueiam `.lub` bytecode em datainfo visual.
- No client hibrido atual, TXT legado completo e usado como alvo de escrita; `itemInfo` textual fica visivel e validado como contexto read-only.

## Atualizacao 2026-05-11 - API segura inicial

- A API inicial expoe somente leitura, validacao, discovery, GRF index/inspect, dry-run e diff-preview.
- `item`, `equipment`, `npc`, `monster` e `map` podem gerar relatorios e diffs por HTTP.
- `apply` e `rollback` continuam exclusivos dos servicos/CLI protegidos por confirmacao explicita; nao ha rotas HTTP para escrita nesta fase.
- `/api/safety/capabilities` expõe que todos os pipelines de conteudo estao com `Apply = false` e `Rollback = false`.
- Qualquer futura rota de escrita deve nascer atras de autenticacao, autorizacao, confirmacao forte, politica de asset obrigatorio e validacao de client date.

## Atualizacao 2026-05-11 - API hardening

- `/api/*` exige chave local por `X-RagnaForge-Api-Key`.
- Todos os endpoints seguros retornam `ApiResponse<T>` com `correlationId`, `operationKind`, `readOnlyMode` e duracao.
- Erros retornam `ProblemDetails` com `errorCode`, `correlationId`, `path`, `timestamp` e validacoes quando houver.
- `ApiOperationGuard` bloqueia operacoes perigosas independentemente de rota existir ou nao.
- Rate/concurrency limits reduzem risco de scan pesado acidental, especialmente GRF e mapa.
- CORS permanece restrito a origins locais configurados.
- OpenAPI documenta que apply/rollback continuam ausentes.

## Atualizacao 2026-05-11 - Interface administrativa read-only

- A interface administrativa agora consome apenas endpoints seguros da API.
- Dashboard e Seguranca/API deixam explicito que `apply` e `rollback` continuam indisponiveis.
- Formularios de item, equipamento, NPC, monstro e mapa geram apenas `dry-run` e `diff-preview`.
- Cliente HTTP central envia `X-RagnaForge-Api-Key` e `X-Correlation-Id`, e propaga `ProblemDetails`.
- Nao ha fallback para CLI nem rotas de escrita ocultas.
