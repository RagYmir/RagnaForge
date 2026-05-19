---
name: ragnaforge-doctor
description: Use para diagnosticar configuração, PathGuard, logs, cache, profiles e proteções do Agente Setimmo via ragnaforge doctor --json.
---

# RagnaForge Doctor

Use quando precisar validar segurança e saúde do agente.

## Comando Principal

```sh
ragnaforge doctor --json
# ou
dotnet run --project src/RagnaForge.Agent.Cli -- doctor --json
```

## Regras

- Não alterar arquivos externos.
- Não modificar GRFs.
- Não editar .lub.
- Validar activeProfile.
- Validar configFingerprint.
- Validar cache obsoleto.
- Separar problemas por severidade.
- Reportar próximos passos seguros.
