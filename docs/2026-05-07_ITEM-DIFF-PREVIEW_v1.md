# 2026-05-07_ITEM-DIFF-PREVIEW_v1

## Resumo

O pipeline de item agora gera diff preview estruturado por arquivo, a partir do proprio dry-run.

## Mudancas

- `ItemDryRunReport` ganhou `DiffPreview`.
- Criado `ItemDiffPreviewBuilder`.
- CLI ganhou `item diff-preview`.
- O diff usa contexto real do final do arquivo e mostra apenas o hunk da adicao proposta.

## Exemplo validado

```powershell
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- item diff-preview ^
  --config data\manifests\repositories.local.json ^
  --aegis RF_Test_Item ^
  --name "RagnaForge Test Item" ^
  --resource RF_Test_Item ^
  --type Etc ^
  --buy 10 ^
  --sell 5 ^
  --weight 10 ^
  --identified-desc "Linha 1|Linha 2"
```

Resultado observado:

- `FileCount = 7`
- `CreatedCount = 0`
- `UpdatedCount = 7`
- hunks reais para:
  - `db/import/item_db.yml`
  - `idnum2itemdisplaynametable.txt`
  - `idnum2itemresnametable.txt`
  - `idnum2itemdesctable.txt`
  - `num2itemdisplaynametable.txt`
  - `num2itemresnametable.txt`
  - `num2itemdesctable.txt`

## Garantias mantidas

- nenhuma escrita em rAthena;
- nenhuma escrita em Patch/client;
- nenhum GRF alterado;
- o diff e apenas leitura + proposta.
