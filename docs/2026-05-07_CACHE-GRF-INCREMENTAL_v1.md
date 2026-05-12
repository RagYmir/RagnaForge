# Cache incremental de GRFs v1

Data: 2026-05-07
Status: implementado e validado localmente

## Decisoes

- O indice fica em `data/cache/grf-repository.index.json`.
- O arquivo de cache e recriavel e nao e fonte da verdade.
- O scanner indexa containers `.grf`, `.gpf`, `.thor`, `.rgz` e `.zip`.
- A indexacao compara caminho, tamanho e `LastWriteTimeUtc` para classificar entradas como adicionadas, alteradas, inalteradas ou removidas.
- Escrita permitida apenas dentro de `data/cache`.
- Cancelamento e suportado por `CancellationToken` e Ctrl+C na CLI.
- Nesta etapa o indice guarda metadados dos containers; conteudo interno de GRF fica para a etapa de integracao com GRF Editor/GrfCL.

## Estrutura criada

```text
backend/src/RagnaForge.Domain/Discovery/
  GrfRepositoryIndexDocument.cs

backend/src/RagnaForge.Application/
  Abstractions/IGrfRepositoryIndexStore.cs
  Grf/GrfRepositoryIndexOptions.cs

backend/src/RagnaForge.Infrastructure/Grf/
  CachedGrfRepositoryIndexer.cs
  JsonGrfRepositoryIndexStore.cs

data/cache/
  grf-repository.index.json
```

## CLI

```powershell
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- grf index `
  --config data\manifests\repositories.local.json `
  --cache data\cache\grf-repository.index.json `
  --max-containers 20
```

## Validacao executada

- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- `dotnet build RagnaForge.slnx`
- `grf index --config ... --max-containers 20`
- Reexecucao de `grf index` carregou cache e marcou 20 containers como inalterados.
- Smoke test de seguranca recusou `--cache outside-index.json`.

## Pendencias

- Implementar Dependency Resolver de item.
- Implementar dry-run de item.
- Investigar sintaxe real do `GrfCL.exe`.
- Expandir o indice para arquivos internos de GRF/Thor quando a camada `GrfEditorIntegration` estiver tecnicamente fechada.
