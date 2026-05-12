# 2026-05-11 - NPC client identity v1

## Resumo

Esta rodada fecha o primeiro pipeline seguro para identidade client-side de NPC custom, sem tocar bytecode `.lub`.

## Arquivos detectados

O planner procura no Patch real por variantes de:

- `jobname`
- `jobidentity`
- `npcidentity`

Formatos classificados:

- `Missing`
- `TextLua`
- `TextLub`
- `BinaryLub`
- `LegacyTxt`
- `Unknown`

## Regras de seguranca

- `TextLua`, `TextLub` e `LegacyTxt` podem entrar em dry-run, diff e apply.
- `BinaryLub` bloqueia apply client-side.
- `Unknown` bloqueia apply client-side.
- O pipeline nao decompila, nao recompila e nao sobrescreve bytecode.
- Sem `--allow-server-only`, NPC custom com identidade client-side bloqueada nao aplica.

## Resolucao de sprite

O sprite do NPC pode ser resolvido por Patch solto, indice GRF local, `live-scan-fallback` ou `live-scan`.

Se o sprite existir apenas em GRF, o pipeline registra a proveniencia, mas nao copia o asset automaticamente para o Patch nesta etapa.

## Apply e rollback

`npc apply` revalida server-side e client-side, monta arquivos completos em staging, roda validacao pos-write e aplica somente alvos seguros.

`npc rollback` restaura script, loader e arquivos textuais de `jobname`, `jobidentity` e `npcidentity`, validando SHA-256 antes de restaurar.

## Limitacoes restantes

- `BinaryLub` continua bloqueado.
- Asset copy de sprite custom vindo so de GRF continua pendente.
- Clients hibridos mais exoticos ainda precisam de mais mapeamento.

## Proximo passo

- Client-side avancado para `itemInfo` e clients hibridos.
- Estrategia geral reutilizavel para `.lua/.lub/.txt`.
