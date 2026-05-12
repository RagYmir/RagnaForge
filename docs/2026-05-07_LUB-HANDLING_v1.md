# 2026-05-07 - LUB handling v1

## Resumo

Foi definido um caminho seguro inicial para lidar com `.lub`, separando arquivos legíveis em texto de bytecode compilado.

## Observação local

No Patch atual, arquivos como:

- `accname.lub`
- `jobname.lub`
- `spriterobename.lub`
- `weapontable.lub`

foram inspecionados por leitura textual direta e puderam ser pesquisados com `Select-String`.

## Decisão técnica

- `.lub` legível em texto pode ser tratado como arquivo textual no MVP;
- `.lub` com assinatura de bytecode Lua permanece read-only até existir estratégia dedicada e aprovada de decompilação;
- o sistema não deve assumir que todo `.lub` precisa ser descompilado.

## Implementação

- `backend/src/RagnaForge.Infrastructure/Patch/LuaScriptFormatDetector.cs`

Formatos detectados:

- `Text`
- `LuaBytecode`
- `BinaryUnknown`
- `Missing`

## Validacao

- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`

## Atualizacao 2026-05-11 - NPC client identity

- `jobname`, `jobidentity` e `npcidentity` agora passam por classificacao explicita.
- A classificacao usada e `TextLua`, `TextLub`, `BinaryLub`, `LegacyTxt`, `Missing` ou `Unknown`.
- `TextLub` continua permitido quando o arquivo real for texto legivel.
- `BinaryLub` continua bloqueado; o pipeline nao tenta decompilar, converter ou sobrescrever bytecode.
- O apply de NPC so toca arquivos client-side quando a identidade estiver em formato textual e a validacao em staging passar.

## Atualizacao 2026-05-11 - itemInfo e datainfo visual

- `itemInfo.lub` textual agora entra no `ClientSidePlan` como `TextLub`.
- `itemInfo.lub` bytecode entra como `BinaryLub` e bloqueia apply.
- Datainfo visual textual de equipamento tambem e classificado antes de gerar hunk.
- Datainfo visual bytecode nao recebe hunk editavel.

## Resultado atual

- o projeto já tem detector para separar texto de bytecode;
- no client atual, datainfo principal observado até aqui se comporta como texto legível;
- decompilação fica reservada apenas para casos realmente binários e indispensáveis.
