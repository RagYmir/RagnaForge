---
name: ragnaforge-scan
description: Use para executar scan read-only e cache incremental do Agente Setimmo sem aplicar alterações.
---

# RagnaForge Scan

Use para scan read-only.

## Comando Futuro

```sh
ragnaforge scan --project --json
```

## Regras

- Scan deve ser read-only.
- Não alterar arquivos escaneados.
- Não escanear fora dos caminhos configurados.
- Respeitar readOnlyRoots.
- Registrar activeProfile.
- Registrar configFingerprint.
- Invalidar cache se paths mudaram.
- Retornar resumo pequeno.
