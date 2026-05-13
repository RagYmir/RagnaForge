# 2026-05-07 - GRF lookup provenance v1

## Resumo

Os relatórios de `item dry-run` e `equipment dry-run` agora informam claramente se o match GRF veio de indice local ou de fallback ao vivo.

## Escopo

- sem alterar rAthena, Patch ou GRFs;
- sem mudar regras de apply;
- apenas enriquecendo o JSON de saída e as mensagens de dependencia.

## O que entrou

- `GrfAssetLookupSource` no domínio;
- `LocalIndexesLoaded` e `LiveContainersScanned` no resultado de lookup;
- `AssetLookup` no `ItemDryRunReport`;
- `ItemAssetLookup` e `VisualAssetLookup` no `EquipmentDryRunReport`;
- mensagens de dependencia agora citam a origem do match quando o lookup GRF resolve o asset.

## Valores atuais de proveniencia

- `LocalIndex`
- `LiveScan`
- `LiveScanFallback`

## Validacao

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- smoke real:
  - `dotnet run --project backend\src\RagnaForge.Cli -- item dry-run --config data\manifests\repositories.local.json --aegis RF_Index_Source --name "RagnaForge Index Source" --resource sample --type Etc --buy 10 --sell 5 --weight 10 --identified-desc "Linha 1|Linha 2" --asset-grf-container "<WORKSPACE_ROOT>\tmp\grf-smoke\sample.grf"`
  - `dotnet run --project backend\src\RagnaForge.Cli -- equipment dry-run --config data\manifests\repositories.local.json --aegis RF_Index_Source_Weapon --name "RagnaForge Index Source Weapon" --resource sample --type Weapon --identified-desc "Linha 1|Linha 2" --locations Right_Hand --visual-category weapon --view 77778 --client-symbol WEAPONTYPE_SAMPLE_SOURCE --client-sprite sample --weapon-base-type SWORD --weapon-level 1 --buy 10 --sell 5 --weight 10 --asset-grf-container "<WORKSPACE_ROOT>\tmp\grf-smoke\sample.grf"`

## Resultado atual

- item dry-run mostra `AssetLookup.Source = LocalIndex` quando resolve pelo indice local;
- equipment dry-run mostra `ItemAssetLookup.Source` e `VisualAssetLookup.Source` separadamente;
- o fluxo continua 100% em dry-run e sem escrita fora do workspace.
