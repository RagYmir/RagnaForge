# Manifest de configuracao progressiva v1

Data: 2026-05-07
Status: implementado e validado localmente

## Decisoes

- A copia ativa do projeto e `C:\Users\Allis\Desktop\New project`.
- A copia antiga em `C:\Users\Allis\Documents\New project` ficou intocada.
- O manifest local fica em `data/manifests/repositories.local.json`.
- O servidor e tratado como progressivo por episodio.
- O episodio atual fica em `PreRenewal`, sem transformar isso em regra permanente.
- `ClientDate` permanece `null` e `ClientDateStatus` fica `unknown` ate deteccao ou confirmacao.
- rAthena, Patch/client e GRFs continuam sendo a fonte da verdade.

## Estrutura criada

```text
backend/src/RagnaForge.Domain/Configuration/
  ConfigurationManifest.cs
  ManifestValidationIssue.cs
  ManifestValidationResult.cs

backend/src/RagnaForge.Application/
  Abstractions/IConfigurationManifestStore.cs
  Configuration/ConfigurationManifestValidator.cs

backend/src/RagnaForge.Infrastructure/Configuration/
  JsonConfigurationManifestStore.cs

data/manifests/
  repositories.local.json
```

## CLI

```powershell
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- config init `
  --rathena "E:\Ragnarok\Testes\rAthena_teste" `
  --patch "E:\Ragnarok\Testes\Patch_teste" `
  --grfs "E:\Ragnarok\Conteudo Ragnarok\GRF'S" `
  --grf-editor "C:\Program Files (x86)\GRF Editor" `
  --episode-name "progressive-current" `
  --episode-mode pre-renewal `
  --out data\manifests\repositories.local.json
```

```powershell
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- config validate `
  --config data\manifests\repositories.local.json
```

```powershell
dotnet run --project backend\src\RagnaForge.Cli\RagnaForge.Cli.csproj -- discover `
  --config data\manifests\repositories.local.json `
  --max-grf-containers 20
```

## Validacao executada

- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- `dotnet build RagnaForge.slnx`
- `config init` criou o manifest dentro de `data/manifests`.
- `config validate` validou os caminhos reais e retornou apenas warning de `client_date.unknown`.
- `discover --config` reutilizou o manifest e leu rAthena/Patch/GRFs em modo read-only.
- Smoke test de seguranca recusou `--out outside.json` e confirmou que nenhum arquivo externo foi criado.

## Pendencias

- Criar cache incremental/cancelavel para GRFs grandes.
- Criar Dependency Resolver de item.
- Criar dry-run de item.
- Investigar sintaxe real do `GrfCL.exe` com arquivos temporarios controlados.
- Detectar client date com base nos executaveis/client antes de escrever dependencias client-side.
