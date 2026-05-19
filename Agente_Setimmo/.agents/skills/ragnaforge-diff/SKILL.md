---
name: ragnaforge-diff
description: Use para revisar diffs gerados pelo Agente Setimmo antes de qualquer confirmação humana.
---

# RagnaForge Diff

Use para revisar diff.

## Comando Futuro

```sh
ragnaforge diff --operation <operation_id> --json
```

## Regras

- Nunca aplicar diff automaticamente.
- Destacar arquivos afetados.
- Destacar riscos.
- Confirmar activeProfile/configFingerprint da operação.
- Pedir revisão humana.
