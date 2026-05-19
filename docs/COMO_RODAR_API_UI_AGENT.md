# Como rodar API, UI e Agente Setimmo

## Agente Setimmo

```powershell
cd .\Agente_Setimmo
dotnet run --project src\RagnaForge.Agent.Cli -- status --json
dotnet run --project src\RagnaForge.Agent.Cli -- doctor --json
dotnet run --project src\RagnaForge.Agent.Cli -- health --json
dotnet run --project src\RagnaForge.Agent.Cli -- knowledge validate --json
```

Confirmacao de seguranca:

```powershell
dotnet run --project src\RagnaForge.Agent.Cli -- apply --json
dotnet run --project src\RagnaForge.Agent.Cli -- rollback --list --json
```

O primeiro comando deve ser bloqueado por politica. O segundo deve listar informacao sem executar rollback real.

## API

```powershell
dotnet build .\RagnaForge.slnx
dotnet run --project .\backend\src\RagnaForge.Api\RagnaForge.Api.csproj
```

Endpoints uteis:

- `/api/agent/health`
- `/api/knowledge/status`
- `/api/pipeline/status`

## UI

```powershell
cd .\frontend
npm.cmd ci
npm.cmd run dev
```

Abra a URL exibida no terminal.

## Testes

```powershell
dotnet run --project .\backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj

cd .\frontend
npm.cmd run build
npm.cmd run test
cd ..

cd .\Agente_Setimmo
dotnet test .\RagnaForge.Agent.slnx
cd ..
```
