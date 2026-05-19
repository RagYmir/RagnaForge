---
name: ragnaforge-status
description: Use para verificar o estado seguro do Agente Setimmo via ragnaforge status --json, sem alterar arquivos.
---

# RagnaForge Status

Use quando precisar entender o estado atual do agente.

## Comando Principal

```sh
ragnaforge status --json
# ou
dotnet run --project src/RagnaForge.Agent.Cli -- status --json
```

## Regras

- Não alterar arquivos.
- Não executar apply.
- Não executar rollback real.
- Reportar activeProfile.
- Reportar configFingerprint.
- Reportar warnings e errors.
- Resumir o JSON para o usuário.
