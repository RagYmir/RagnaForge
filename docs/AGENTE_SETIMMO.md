# Agente Setimmo

Agente Setimmo e o nome publico do agente local incorporado ao Ragna_Forge.

Pasta tecnica:

```text
Agente_Setimmo/
```

## Por que ele fica dentro do projeto

Ficar dentro de `Ragna_Forge/` reduz ambiguidade de caminho e facilita:

- validar ambiente;
- rodar smokes;
- publicar executavel local;
- empacotar uma release limpa;
- integrar com a API pelo endpoint `GET /api/agent/health`.

## Compatibilidade tecnica

Namespaces e nomes de alguns assemblies continuam `RagnaForge.Agent.*` para reduzir risco de refatoracao grande. Isso e intencional nesta etapa.

O executavel publico publicado e:

```text
dist/agent/agente-setimmo.exe
```

Tambem pode existir:

```text
dist/agent/ragnaforge.exe
```

Esse segundo nome existe apenas para compatibilidade com scripts antigos.

## Comandos seguros

```powershell
dotnet run --project src\RagnaForge.Agent.Cli -- status --json
dotnet run --project src\RagnaForge.Agent.Cli -- doctor --json
dotnet run --project src\RagnaForge.Agent.Cli -- baseline --json
dotnet run --project src\RagnaForge.Agent.Cli -- health --json
dotnet run --project src\RagnaForge.Agent.Cli -- validate --json
dotnet run --project src\RagnaForge.Agent.Cli -- knowledge validate --json
```

## Comandos bloqueados

```powershell
dotnet run --project src\RagnaForge.Agent.Cli -- apply --json
```

Apply real permanece bloqueado neste MVP.
