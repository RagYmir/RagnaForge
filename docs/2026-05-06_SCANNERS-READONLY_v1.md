# Scanners read-only v1

Data: 2026-05-06
Status: implementado e validado localmente

## Decisoes

- Criar estrutura .NET/C# antes de qualquer UI.
- Usar .NET `net10.0` porque o SDK/template local `10.0.203` nao oferece scaffold direto para `net8.0`.
- Implementar apenas Discovery read-only nesta etapa.
- Nao escrever em rAthena, Patch/client ou GRFs.
- Modelar servidor progressivo com `EpisodeProfile`.
- Manter `pre-renewal` como perfil atual, nao regra permanente.

## Estrutura criada

```text
backend/
  src/
    RagnaForge.Domain/
    RagnaForge.Application/
    RagnaForge.Infrastructure/
    RagnaForge.Cli/
  tests/
    RagnaForge.Tests/

data/
  cache/
  indexes/
  manifests/
  backups/
  logs/
```

## Codigo criado

- Domain:
  - `RepositoryPaths`
  - `EpisodeProfile`
  - `EpisodeMode`
  - modelos de resultados de Discovery
- Application:
  - contratos de scanners
  - `RepositoryDiscoveryService`
  - `DiscoveryOptions`
- Infrastructure:
  - `RathenaScanner`
  - `PatchScanner`
  - `GrfRepositoryScanner`
  - `GrfEditorProbe`
- CLI:
  - comando `discover`
- Tests:
  - test runner simples sem dependencias externas.

## Comando de Discovery

```powershell
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- discover `
  --rathena "<RATHENA_PATH>" `
  --patch "<PATCH_PATH>" `
  --grfs "<GRF_REPOSITORY_PATH>" `
  --grf-editor "<GRF_EDITOR_PATH>" `
  --episode-name "progressive-current" `
  --episode-mode pre-renewal `
  --client-date "2025-07-16" `
  --max-grf-containers 20
```

O comando imprime JSON no stdout e nao grava arquivos.

## Validacao executada

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- CLI Discovery contra os caminhos reais com `--max-grf-containers 20`.

## Pendencias

- Persistir manifest de configuracao somente dentro do workspace, apos decisao de formato.
- Implementar Dependency Resolver de item.
- Implementar dry-run de item sem apply.
- Investigar sintaxe real do `GrfCL.exe` com GRFs temporarios controlados.
- Adicionar scanner incremental com checkpoint/cache para GRFs grandes.

