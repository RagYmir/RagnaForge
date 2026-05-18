# Operational Readiness v1

Data: 2026-05-18

## Escopo

Fechamento operacional da branch de SPR/ACT preview read-only com integracao Agent Health e uso do RagnaForge Agent como ferramenta auxiliar segura.

## Estado validado

- Backend: 126/126 testes passando.
- Frontend: 30/30 testes passando.
- Agent auxiliar: 183/183 testes passando.
- API/UI seguem read-only para esta entrega.
- `GET /api/agent/health` expõe somente diagnostico estruturado.
- AgentIntegration usa allowlist rigida.
- MCP do Agent expoe tools, resources e prompts read-only.

## Garantias

- Sem endpoint de apply na API.
- Sem endpoint de rollback real na API.
- Sem botao de apply na UI.
- Sem botao de rollback real na UI.
- Sem tool MCP de apply.
- Sem tool MCP de rollback real.
- Sem shell generico.
- Sem comando livre vindo do usuario.
- Sem escrita em rAthena, Patch/client, GRF ou `.lub`.

## Asset preview

- SPR: preview visual best-effort com fallback para metadados.
- ACT: metadata-only no v1.
- TGA/GAT/GND/RSW/RSM: placeholders ate parser/conversor seguro.

## Agent Health

O backend pode chamar somente:

- `status --json`
- `doctor --json`
- `scan --project --json`
- `index --entities --json`
- `validate --json`

Cache stale deve gerar warning claro e nao deve ser mostrado como contagem confiavel.

## External-data triage

Issues de dataset externo podem deixar `safeForApply=false`, sem bloquear auditoria read-only ou dry-run quando `safeForReadOnlyWork=true` e `safeForDryRun=true`.

## Proximo passo recomendado

Manter a proxima fase em modo seguro: preview visual real read-only de assets ou bateria de casos reais com report, sem liberar apply/rollback na API/UI.
