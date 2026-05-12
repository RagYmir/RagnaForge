# 2026-05-07_ITEM-GRF-ASSET-LOOKUP_v1

## Resumo

O dry-run de item agora consegue validar assets dentro de containers GRF usando a integracao direta com `GRF.dll`.

## Mudancas

- Criado contrato `IGrfAssetLookupService`.
- Criados modelos `GrfAssetLookupOptions`, `GrfAssetLookupResult` e `GrfAssetLookupMatch`.
- Implementado `GrfAssemblyAssetLookupService`.
- `LegacyItemDryRunService` agora consulta GRF quando nao encontra asset solto no Patch.
- CLI `item dry-run` aceita:
  - `--asset-grf-container`
  - `--scan-grf-assets`
  - `--max-grf-asset-containers`
  - `--max-grf-asset-matches`
  - `--grf-cache`

## Smoke real

Comando validado:

```powershell
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- item dry-run ^
  --config data\manifests\repositories.local.json ^
  --aegis RF_C_Rabbit_Robe ^
  --name "RagnaForge Rabbit Robe" ^
  --resource c_rabbit_winged_robe ^
  --type Etc ^
  --buy 10 ^
  --sell 5 ^
  --weight 10 ^
  --identified-desc "Teste asset GRF" ^
  --asset-grf-container data_0.grf
```

Resultado:

- `CanApply = true`
- `ResolvedId = 50000`
- asset solto no Patch nao foi necessario;
- o lookup encontrou `4` candidatos dentro de `data_0.grf`.

## Regras mantidas

- nenhuma escrita em rAthena;
- nenhuma escrita no Patch/client;
- nenhum GRF original alterado;
- tudo continua como dry-run.

## Proximo passo

Implementar diff preview especializado de item antes de qualquer apply.
