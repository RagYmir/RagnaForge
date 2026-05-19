# API Pipeline Workspace v1

Data: 2026-05-18

## Objetivo

Consolidar um workspace operacional read-only para a API/UI do RagnaForge, reunindo status, planejamento, dry-run seguro, diff-preview, issues e reports sem abrir apply, rollback real ou escrita externa.

## Endpoints

- `GET /api/pipeline/status`
- `POST /api/pipeline/plan`
- `POST /api/pipeline/dry-run`
- `POST /api/pipeline/diff-preview`
- `GET /api/pipeline/issues`
- `GET /api/pipeline/reports`
- `GET /api/pipeline/reports/{id}`

Todos exigem `X-RagnaForge-Api-Key` como os demais endpoints `/api/*`.

## Contrato de seguranca

- API/UI continuam read-only, dry-run e diff-preview.
- Nao existem endpoints de apply ou rollback real.
- Nao ha chamada de shell nem comando livre.
- O frontend nao chama CLI.
- `safeForApply` permanece `false`.
- rAthena, Patch/client, GRFs e `.lub` nao sao modificados.
- Reports bloqueiam traversal e path absoluto no identificador.
- Dependency summary usa `NotChecked` ou `Placeholder` quando a API nao verificou o arquivo real.

## UI

A tela `Pipeline API` oferece:

- badges de read-only, dry-run seguro, diff-preview seguro, apply bloqueado e rollback real bloqueado;
- seletor de entidade;
- editor JSON para payload;
- botoes seguros para `Gerar plano`, `Executar dry-run seguro` e `Gerar diff-preview`;
- painel de dependencies/readiness;
- dashboard de issues;
- lista read-only de reports;
- warnings/errors e `correlationId` visiveis.

Nao ha botao de apply, botao de rollback, input de comando livre ou persistencia de segredo no browser.

## Validacao

- Backend: `141/141` testes passando.
- Frontend: `32/32` testes passando.
- Agent: `183/183` testes passando.

## Limites conhecidos

- `reports` ainda retorna sumarios/readback seguros e pequenos; historico persistente completo deve ser tratado em etapa propria.
- `plan` reutiliza dry-runs internos existentes quando possivel, mas nao transforma readiness em autorizacao de escrita.
- Mapas continuam sem parser binario profundo nesta tela; dependencias complexas seguem como placeholder quando nao verificadas.

## Proximo passo recomendado

Executar bateria com payloads reais do servidor progressivo via API Pipeline Workspace e registrar amostras sanitizadas de plan/dry-run/diff-preview.
