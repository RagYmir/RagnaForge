# review-diff

Revise diffs gerados pelo Agente Setimmo antes de qualquer confirmação.

## Passos

1. Execute `ragnaforge diff --operation <id> --json` (quando disponível).
2. Revise cada arquivo afetado.
3. Destaque riscos.
4. Confirme activeProfile/configFingerprint da operação.
5. Produza Artifact com análise do diff.
6. Peça revisão humana.

## Proibido

- Aplicar diff automaticamente.
- Confirmar operação sem revisão humana.
