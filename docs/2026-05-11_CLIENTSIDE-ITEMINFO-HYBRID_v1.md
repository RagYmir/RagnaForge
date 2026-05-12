# 2026-05-11 - Client-side itemInfo e clients hibridos v1

## Resumo

Esta rodada consolidou um plano comum para client-side de item/equipamento, cobrindo `itemInfo`, TXT legado, `.lua`, `.lub` textual, `.lub` bytecode, datainfo visual e clients hibridos.

Nenhum `.lub` bytecode e editado, convertido ou decompilado. Nenhum asset e copiado de GRF para Patch nesta etapa.

## ClientSidePlan

O plano comum fica no dominio e e usado por item e equipamento.

Campos principais:

- `Required`
- `CanApply`
- `BlockReasons`
- `ClientProfile`
- `ClientSideMode`
- `DetectedFiles`
- `FileFormats`
- `ItemInfoDetected`
- `LegacyTablesDetected`
- `HybridClientDetected`
- `SupportedTargets`
- `UnsupportedTargets`
- `BytecodeBlockedFiles`
- `ProposedRegistrations`
- `ExistingRegistrations`
- `ProposedChanges`
- `DiffHunks`
- `ApplyTargets`
- `RollbackTargets`
- `PostWriteValidationPlan`
- `ValidationWarnings`
- `ValidationErrors`
- `ApplyReadiness`

## ClientSideMode

- `ItemInfo`: ha `itemInfo.lua` ou `itemInfo.lub` textual seguro e nao ha conjunto legado completo preferido.
- `LegacyTxt`: tabelas TXT legadas estao completas e sao o alvo seguro.
- `Hybrid`: `itemInfo` e tabelas TXT legadas coexistem.
- `Unknown`: nao ha alvo seguro suficiente para aplicar client-side.

## Arquivos detectados

ItemInfo:

- `System/ItemInfo.lua`
- `System/ItemInfo.lub`
- `system/iteminfo.lua`
- `system/iteminfo.lub`
- `system/iteminfo_true.lua`
- `system/iteminfo_true.lub`

TXT legado:

- `data/idnum2itemdisplaynametable.txt`
- `data/idnum2itemresnametable.txt`
- `data/idnum2itemdesctable.txt`
- `data/num2itemdisplaynametable.txt`
- `data/num2itemresnametable.txt`
- `data/num2itemdesctable.txt`
- `data/itemslotcounttable.txt`

Datainfo visual de equipamento:

- `accessoryid.lua/lub`
- `accname.lua/lub`
- `spriterobeid.lua/lub`
- `spriterobename.lua/lub`
- `weapontable.lua/lub`

## Formatos

- `TextLua`: texto Lua manipulavel em staging.
- `TextLub`: `.lub` textual manipulavel em staging.
- `LegacyTxt`: tabela TXT legada manipulavel em staging.
- `BinaryLub`: bloqueado.
- `Unknown`: bloqueado.
- `Missing`: arquivo ausente, usado no plano para evidenciar dependencia.

## Patch atual

O Patch atual foi detectado como `Hybrid` para itens:

- `E:\Ragnarok\Testes\Patch_teste\system\iteminfo_true.lub`: `TextLub`.
- Tabelas TXT em `E:\Ragnarok\Testes\Patch_teste\data`: `LegacyTxt`.

E para equipamento visual no smoke de headgear:

- `accessoryid.lub`: `TextLub`.
- `accname.lub`: `TextLub`.

Em smokes complementares de equipamento:

- `spriterobeid.lub`: `TextLub`.
- `spriterobename.lub`: `TextLub`.
- `weapontable.lub`: `TextLub`.

Estrategia segura desta rodada:

- Escrever em TXT legado completo quando o client for hibrido e o `itemInfo` for textual.
- Manter `itemInfo` textual visivel no relatorio e validado como contexto read-only no Patch atual.
- Bloquear se `itemInfo` ou datainfo visual essencial for bytecode/unknown.

## Item

`item dry-run` agora expoe:

- `ClientSidePlan`
- `ClientSideMode`
- `BytecodeBlocks`
- `ExistingClientRegistration`
- `ProposedClientRegistration`
- `PostWriteValidationPlan`

`item diff-preview`:

- gera hunks apenas para alvos textuais seguros;
- nao gera hunk falso para bytecode;
- mostra registros existentes, bloqueios e proposta client-side junto com o diff server-side.

`item apply`:

- revalida server-side e client-side;
- monta arquivos finais em staging;
- executa `ApplyPostWriteValidator`;
- so substitui destino final se o staging passar;
- grava backup/log/manifest;
- bloqueia `.lub` bytecode e client-side inseguro.

`item rollback`:

- exige `--confirm ROLLBACK`;
- valida SHA-256 do estado aplicado;
- restaura arquivos client-side textuais;
- bloqueia rollback se o arquivo teve drift manual depois do apply.

## Equipamento

`equipment dry-run` agora expoe:

- `ClientSidePlan` para o item base;
- `VisualClientSidePlan` para datainfo visual;
- `BytecodeBlocks`;
- `ApplyReadiness`.

`equipment diff-preview/apply`:

- reutilizam a estrategia geral de client-side;
- bloqueiam bytecode em datainfo visual;
- mantem bloqueios de simbolo inseguro, `ViewID` duplicado e shield custom sem tabela dedicada;
- validam o staging completo antes da escrita final.

`equipment rollback`:

- continua protegido por `--confirm ROLLBACK` e SHA-256;
- restaura tambem TXT legado e datainfo textual alterados.

## Validacao pos-write

`ApplyPostWriteValidator` foi ampliado para validar o arquivo final em staging:

- YAML de item/equipamento;
- TXT legado de item;
- Lua/LUB textual;
- datainfo visual textual.

A validacao verifica legibilidade, estrutura basica, existencia do registro proposto e duplicidades obvias antes da substituicao final.

## Testes adicionados

- Deteccao de `itemInfo.lua` textual.
- Deteccao de `itemInfo.lub` textual.
- Bloqueio de `itemInfo.lub` bytecode.
- Deteccao de tabelas TXT legadas.
- Deteccao de client hibrido.
- Bloqueio de hibrido ambiguo com bytecode.
- Registro de validacao pos-write no item apply.
- Rollback de item bloqueando drift client-side.
- Exposicao de `VisualClientSidePlan` no equipamento.
- Bloqueio de datainfo visual bytecode.

## Validacao executada

- `dotnet build RagnaForge.slnx`: 0 erros, 0 avisos.
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`: 81/81 testes OK.

## Smokes reais seguros

- `item dry-run` com `RF_ClientSide_Smoke`: `ClientSideMode = Hybrid`, `itemInfo_true.lub = TextLub`, tabelas TXT = `LegacyTxt`, `CanApply = true`.
- `equipment dry-run` com `RF_ClientSide_Visual`: `ClientSideMode = Hybrid`, `accessoryid.lub = TextLub`, `accname.lub = TextLub`, `CanApply = true`.
- Smokes complementares de equipamento detectaram `spriterobeid.lub`, `spriterobename.lub` e `weapontable.lub` como `TextLub`.
- `item apply` sem `--confirm APPLY`: recusado antes de qualquer escrita real.

## Limitacoes

- Asset copy de icones/sprites ainda nao foi liberado nesta etapa.
- Clients hibridos exoticos continuam bloqueados quando o alvo seguro nao for evidente.
- `.lub` bytecode continua bloqueado.
- `itemInfo` textual recebe plano seguro para clients ItemInfo-only, mas no Patch atual o alvo preferido continua sendo TXT legado por coexistencia hibrida.
- Datainfo visual equivalente fora dos arquivos ja mapeados ainda precisa ser adicionado quando aparecer em outro client.

## Proximo passo

Auditoria pre-producao de dry-run/diff-preview para item, equipamento, NPC, monstro e mapa antes de iniciar API/interface.
