# Item dry-run e client date v1

Data: 2026-05-07
Status: implementado e validado localmente

## Decisoes

- O primeiro resolver de item foca no ambiente local legado baseado em tabelas TXT.
- O dry-run nao escreve em rAthena nem Patch/client; ele apenas produz proposta e dependencias.
- Se `Id` nao for informado, o sistema sugere/aloca o primeiro ID livre a partir de `50000`.
- Se `ClientDate` nao vier do manifesto, o sistema usa a deteccao do Patch local.
- `ResourceName` cai para `AegisName` quando nao informado.
- Assets visuais de item continuam como validacao por aviso quando so puderem estar dentro de GRF ainda nao indexada internamente.

## Estrutura criada

```text
backend/src/RagnaForge.Domain/
  Discovery/PatchDiscoveryResult.cs
  Items/ItemDefinitionInput.cs
  Items/ItemDryRunReport.cs

backend/src/RagnaForge.Infrastructure/
  Patch/PatchScanner.cs
  Items/LegacyItemDryRunService.cs
```

## CLI

```powershell
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- item dry-run `
  --config data\manifests\repositories.local.json `
  --aegis RF_Test_Item `
  --name "RagnaForge Test Item" `
  --resource RF_Test_Item `
  --type Etc `
  --buy 10 `
  --sell 5 `
  --weight 10 `
  --identified-desc "Linha 1|Linha 2"
```

## Validacao executada

- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- `dotnet build RagnaForge.slnx`
- `discover --config ...` confirmou:
  - `ClientDate = 2025-07-16`
  - fonte `2025-07-16_Ragexe_175220998_clientinfo_patched.exe`
  - modo client legado e iteminfo detectados ao mesmo tempo
- `item dry-run ...` retornou:
  - `ResolvedId = 50000`
  - `CanApply = true`
  - `ProposedChanges = 7`

## GrfCL

- codigo-fonte publico de referencia consultado no commit `dfa26ab`
- `-version`, `-open`, `-grfInfo`, `-makeGrf` e `-extractGrf` foram confirmados
- em laboratorio temporario, `-makeGrf` e `-extractGrf` funcionaram, mas com ruido textual de excecao em PowerShell

## Pendencias

- Expandir a verificacao de assets para conteudo interno de GRF por container e de forma opt-in.
- Asset copy de icones/sprites ainda deve permanecer planejado e bloqueado quando obrigatorio.
- Auditoria pre-producao deve exercitar dry-run/diff-preview reais antes de API/interface.

## Atualizacao 2026-05-11 - client-side avancado

- `item dry-run` agora expoe `ClientSidePlan` com `ClientSideMode`, arquivos detectados, formatos, registros existentes/propostos, bloqueios por bytecode e plano de validacao pos-write.
- O Patch atual foi classificado como `Hybrid`: `system/iteminfo_true.lub` textual e tabelas TXT legadas completas coexistem.
- Para o Patch atual, a estrategia segura escreve nas tabelas TXT legadas e trata `itemInfo` textual como contexto read-only.
- `item diff-preview` nao gera hunk editavel para `.lub` bytecode.
- `item apply` valida o arquivo final montado em staging antes de substituir o destino real.
- `item rollback` valida SHA-256 do estado aplicado e bloqueia drift manual em arquivos client-side.
