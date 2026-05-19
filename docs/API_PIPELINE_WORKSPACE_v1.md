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
- `diff-preview` e stateless: um `operationId` desconhecido nao aplica nada, nao escreve externamente e deve retornar apenas preview seguro ou warning.

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

- Backend: `145/145` testes passando.
- Frontend: `33/33` testes passando.
- Agent: `199/199` testes passando.

## Bateria real de payloads

A bateria `PipelineRealPayloadBatteryTests` cobre fixtures sanitizadas para item consumable, equipment weapon, equipment armor, asset visual, NPC, monstro, mapa existente, mapa com dependencias ausentes, payload invalido sem campo obrigatorio, entity type invalido, traversal, command injection, unicode, variacoes de casing, warning de external-data e knowledge hint.

As fixtures ficam em `backend/tests/RagnaForge.Tests/Fixtures/PipelinePayloads/` e nao contem path real, segredo, token, dump, GRF, Patch real ou asset privado. Payloads maliciosos sao tratados como dados/rejeitados, sem shell, sem stack trace e sem vazamento de path absoluto em resposta.

`429 TooManyRequests` so e aceito em cenarios de concorrencia/repeticao/rate-limit; testes funcionais normais continuam exigindo `200`, `400`, `422` ou o codigo seguro esperado.

## Limites conhecidos

- `reports` ainda retorna sumarios/readback seguros e pequenos; historico persistente completo deve ser tratado em etapa propria.
- `plan` reutiliza dry-runs internos existentes quando possivel, mas nao transforma readiness em autorizacao de escrita.
- Mapas continuam sem parser binario profundo nesta tela; dependencias complexas seguem como placeholder quando nao verificadas.

## Proximo passo recomendado

Executar bateria com payloads reais do servidor progressivo via API Pipeline Workspace e registrar amostras sanitizadas de plan/dry-run/diff-preview.
