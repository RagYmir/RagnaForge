# 2026-05-07_EQUIPMENT-DRYRUN_v1

## Resumo

Foi implementado o primeiro `dry-run` de equipamento como especializacao de item.

## Escopo desta fase

Cobertura com proposta real:

- `headgear`
- `accessory`
- `robe`

Cobertura ainda pendente:

- `weapon`
- `shield`
- outras categorias com tabelas client-side especificas

## Mudancas

- criado `EquipmentDefinitionInput`
- criado `EquipmentDryRunReport`
- criado `LegacyEquipmentDryRunService`
- CLI ganhou:
  - `equipment dry-run`
  - `equipment diff-preview`

## Arquivos client-side cobertos

Para `headgear` e `accessory`:

- `accessoryid.lub`
- `accname.lub`

Para `robe`:

- `spriterobeid.lub`
- `spriterobename.lub`

## Smoke real

Comando validado:

```powershell
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- equipment dry-run ^
  --config data\manifests\repositories.local.json ^
  --aegis RF_C_Loli_Ruri_Moon ^
  --name "RagnaForge Loli Ruri Moon" ^
  --resource c_loli_ruri_moon ^
  --type Armor ^
  --buy 10 ^
  --sell 5 ^
  --weight 100 ^
  --slots 1 ^
  --identified-desc "Teste robe GRF" ^
  --locations Garment ^
  --visual-category robe ^
  --view 5064 ^
  --client-symbol ROBE_RF_C_Loli_Ruri_Moon ^
  --client-sprite C_Loli_Ruri_Moon ^
  --defense 3 ^
  --equip-level-min 10 ^
  --refineable true ^
  --asset-grf-container data_0.grf
```

Resultado:

- `CanApply = true`
- `10` propostas de alteracao
- asset encontrado dentro de `data_0.grf`
- propostas para `spriterobeid.lub` e `spriterobename.lub`

## Garantias mantidas

- nenhuma escrita em rAthena
- nenhuma escrita em Patch/client
- nenhum GRF alterado
- tudo continua em dry-run/diff

## Atualizacao 2026-05-11 - client-side avancado

- `equipment dry-run` agora reutiliza `ClientSidePlan` para o item base e expoe `VisualClientSidePlan` para datainfo visual.
- O plano visual classifica `accessoryid`, `accname`, `spriterobeid`, `spriterobename` e `weapontable` como `TextLua`, `TextLub`, `BinaryLub`, `LegacyTxt`, `Missing` ou `Unknown`.
- `BinaryLub` e `Unknown` bloqueiam diff/apply; nenhum hunk editavel e criado para bytecode.
- `equipment apply` valida staging completo com `ApplyPostWriteValidator` antes de substituir arquivos reais.
- `equipment rollback` permanece protegido por `--confirm ROLLBACK` e SHA-256 do estado aplicado.
- Shield custom continua bloqueado fora do caminho `robe`/`Costume_Garment` quando o client indicar essa limitacao.
