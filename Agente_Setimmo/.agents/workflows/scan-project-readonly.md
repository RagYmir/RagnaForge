# scan-project-readonly

Execute scan read-only do projeto sem aplicar alterações.

## Passos

1. Verifique que `ragnaforge status --json` reporta OK.
2. Execute `ragnaforge scan --project --json` (quando disponível).
3. Verifique que o scan não alterou arquivos do projeto.
4. Revise o relatório JSON.
5. Produza Artifact com resumo do scan.

## Proibido

- Alterar arquivos escaneados.
- Escanear fora dos caminhos configurados.
- Ignorar readOnlyRoots.
