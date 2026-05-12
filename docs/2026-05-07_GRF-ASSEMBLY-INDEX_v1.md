# 2026-05-07_GRF-ASSEMBLY-INDEX_v1

## Resumo

Foi implementado o primeiro spike funcional de integracao direta com os assemblies embutidos do GRF Editor.

O projeto agora consegue:

- carregar `GRF.dll` a partir do `GrfCL.exe`;
- listar conteudo interno de um container `.grf`;
- contar extensoes e diretorios;
- gerar amostra controlada de entradas;
- salvar o resultado em `data/indexes/`.

## Arquivos principais

- `backend/src/RagnaForge.Infrastructure/GrfEditorIntegration/GrfEditorAssemblyLoadContext.cs`
- `backend/src/RagnaForge.Infrastructure/GrfEditorIntegration/GrfAssemblyContainerInspector.cs`
- `backend/src/RagnaForge.Infrastructure/Grf/JsonGrfContainerIndexStore.cs`
- `backend/src/RagnaForge.Cli/Program.cs`
- `backend/src/RagnaForge.Domain/Discovery/GrfContainerContentIndexDocument.cs`

## Comando novo

```powershell
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- grf inspect ^
  --config data\manifests\repositories.local.json ^
  --container data_0.grf ^
  --cache data\indexes\data_0.index.json ^
  --limit 25
```

## Resultado do smoke real

Container testado:

- `<GRF_REPOSITORY_PATH>\data_0.grf`

Resumo observado:

- `EntryCount = 131185`
- `DirectoryCount = 2802`
- extensoes mais frequentes:
  - `.bmp`
  - `.act`
  - `.spr`
  - `.tga`
  - `.rsm`
  - `.wav`
  - `.str`
  - `.gat`
  - `.gnd`
  - `.rsw`
  - `.lub`

Saida local gerada:

- `data/indexes/data_0.index.json`

## Regras de seguranca mantidas

- nenhuma escrita fora do workspace;
- nenhum GRF original foi alterado;
- o caminho de cache e validado e fica preso a `data/indexes/`;
- `--cache outside-index.json` foi recusado corretamente.

## Proximo passo sugerido

Conectar o lookup interno em GRF ao `LegacyItemDryRunService`, para que a dependencia de asset deixe de depender apenas de arquivos soltos no Patch.
