# 2026-05-07 ITEM-APPLY-AUDIT v1

## Objetivo

Endurecer `item apply` antes de qualquer apply real nos repositorios do jogo.

## Entregue

- Confirmacao explicita continua obrigatoria.
- Preflight de conflitos antes de escrever:
  - `create.target-exists`
  - `append.exact-preview-present`
  - `append.normalized-preview-present`
  - `append.anchor-line-present`
- Log de bloqueio mesmo quando nenhum arquivo e escrito.
- Trilhas de auditoria por etapa (`start`, `preflight`, `backup`, `write`, `blocked`, `failed`, `complete`).
- Hashes SHA-256 e contagem de linhas por arquivo aplicado.
- Rollback continua preso a logs internos do workspace e aceita apenas apply com status `Applied`.

## Resultado

O apply de item agora e mais seguro para evoluir depois para NPC, monstro e mapa.
