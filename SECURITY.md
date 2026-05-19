# Security Policy

Ragna_Forge trabalha com arquivos sensiveis de rAthena, Patch/client e GRFs. Por isso, o padrao de seguranca e simples: ler primeiro, validar sempre, escrever nunca sem uma politica futura formal.

## Escopo atual

- API: read-only, dry-run e diff-preview.
- UI: read-only, dry-run e diff-preview.
- Agente Setimmo: diagnostico, validacao, conhecimento, MCP read-only e relatorios seguros.
- Apply real: fora da API/UI e bloqueado no Agente Setimmo neste MVP.
- Rollback real: fora da API/UI; rollback do agente e informacional/listagem segura.

## Proibido nesta fase

- Endpoint HTTP de apply.
- Endpoint HTTP de rollback real.
- Botao Apply.
- Botao Rollback.
- Shell generico.
- Comando livre vindo do usuario.
- Escrita em rAthena por API/UI.
- Escrita no Patch/client por API/UI.
- Alteracao de GRF original.
- Edicao, decompilacao ou recompilacao automatica de `.lub`.
- Commit de secrets, GRFs, sprites, assets privados, dumps, cache real ou logs reais.

## Agent Health

`GET /api/agent/health` existe apenas para diagnostico local read-only.

Ele usa allowlist rigida para conversar com o Agente Setimmo:

- `status --json`
- `doctor --json`
- `scan --project --json`
- `index --entities --json`
- `validate --json`
- comandos read-only de knowledge quando configurados

O endpoint nao aceita comando livre, nao executa apply, nao executa rollback real e nao expoe shell.

## API Pipeline Workspace

Os endpoints `/api/pipeline/*` organizam status, plano, dry-run, diff-preview, issues e reports.

Eles:

- exigem API key quando configurado;
- retornam `correlationId`;
- nao aplicam diff;
- nao escrevem em rAthena, Patch/client ou GRFs;
- mantem `safeForApply=false` quando ha blocker ou quando a operacao e apenas de auditoria.

## Asset Preview

- Bitmaps comuns: preview visual read-only.
- `.spr`: preview visual best-effort com fallback para metadados.
- `.act`: metadata-only no v1.
- `.tga`, `.gat`, `.gnd`, `.rsw`, `.rsm`: placeholders ate existir parser/conversor seguro.

Temporarios devem ficar em area controlada e ser limpos. Nenhum asset extraido deve ser persistido como resultado automatico.

## Arquivos que nao entram no Git

- `.env`
- `repositories.local.json`
- `Agente_Setimmo/config/paths.json`
- `node_modules`
- `bin` / `obj`
- `frontend/dist`
- `tmp` real
- cache real
- logs reais
- `TestResults`
- `.trx`
- `.tsbuildinfo`
- GRF/GPF/THOR
- SPR/ACT/BMP/TGA/GAT/GND/RSW/RSM/PAL

## Como reportar falha

Se encontrar bypass de read-only, path traversal, vazamento de path sensivel, segredo exposto ou escrita externa indevida, reporte de forma privada quando possivel. Nao publique exploit funcional, token, dump, path sensivel ou arquivo privado.
