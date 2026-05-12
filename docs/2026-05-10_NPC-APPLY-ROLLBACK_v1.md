# 2026-05-10 - NPC apply and rollback

## Objetivo

Adicionar apply/rollback transacional para NPC, mantendo dry-run e confirmacao explicita como gates obrigatorios.

## Implementacao

- Novo `NpcApplyService` em `Infrastructure/Npcs`.
- Novos modelos `NpcApplyLog`, `NpcApplyResult` e `NpcRollbackResult`.
- CLI ampliada com:
  - `npc apply --confirm APPLY`
  - `npc rollback --confirm ROLLBACK --log <arquivo>`
- Logs em `data/logs/npcs`.
- Backups em `data/backups/npcs`.
- Escrita permitida somente dentro de `rAthena/npc`.

## Garantias

- `npc apply` recusa executar sem `--confirm APPLY`.
- `npc rollback` recusa executar sem `--confirm ROLLBACK`.
- Alvos fora de `rAthena/npc` sao bloqueados no preflight.
- Conflitos de create/append sao registrados antes de qualquer escrita.
- Rollback restaura arquivos existentes e remove arquivos criados pelo apply.

## Validacao

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj`
- Smoke CLI sem confirmacao retornou exit code `2` e nao criou novos logs.
