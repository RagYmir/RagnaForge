# Pipeline Real Payload Battery v1

Data: 2026-05-19

## Objetivo

Validar a API Pipeline Workspace com uma bateria de payloads reais ou proximos do real, mantendo a API e a UI em modo read-only/dry-run/diff-preview e sem qualquer fluxo de apply ou rollback.

## Fixtures

As fixtures ficam em `backend/tests/RagnaForge.Tests/Fixtures/PipelinePayloads/`:

- `item_consumable_valid.json`
- `equipment_weapon_valid.json`
- `equipment_armor_valid.json`
- `visual_asset_valid.json`
- `npc_simple_valid.json`
- `monster_simple_valid.json`
- `map_existing_valid.json`
- `map_missing_dependencies.json`
- `payload_invalid_missing_required.json`
- `payload_invalid_entity_type.json`
- `payload_invalid_path_traversal.json`
- `payload_invalid_command_injection.json`
- `payload_unicode_valid.json`
- `payload_casing_variants.json`
- `payload_external_data_warning.json`
- `payload_knowledge_hint_case.json`
- `payload_large_but_allowed.json`

As fixtures sao pequenas e sanitizadas. Elas nao versionam paths reais, tokens, secrets, dumps, GRFs, Patch real, sprites reais ou assets privados.

## Cobertura

- `GET /api/pipeline/status`
- `POST /api/pipeline/plan`
- `POST /api/pipeline/dry-run`
- `POST /api/pipeline/diff-preview`
- `GET /api/pipeline/issues`
- `GET /api/pipeline/reports`
- `GET /api/pipeline/reports/{id}`
- `GET /api/knowledge/search`
- `GET /api/knowledge/explain`
- `GET /api/knowledge/schema/{entityType}`
- ausencia de `POST /api/apply`
- ausencia de `POST /api/rollback`

## Regras de seguranca validadas

- `dry-run` retorna `NoPersistentWrites=true` e `safeForApply=false`.
- `diff-preview` retorna `NoPersistentWrites=true`.
- `diff-preview` e stateless: `operationId` desconhecido nao aplica diff, nao carrega estado destrutivo e nao escreve externamente.
- Traversal e path absoluto em reports sao bloqueados ou nao roteados.
- Payloads com traversal ou command injection nao executam shell e nao vazam stack trace, filesystem exception ou path absoluto local na resposta.
- Payload oversized e rejeitado por limite seguro.
- `429 TooManyRequests` e aceito apenas em testes de concorrencia/repeticao/rate-limit e precisa retornar resposta segura.

## Resultado validado

- Backend: `145/145` testes passando.
- Frontend: `33/33` testes passando.
- Agent: `199/199` testes passando.

## Limites

- A bateria usa fixtures sanitizadas e nao exercita arquivos reais de GRF/Patch/rAthena.
- O Agent pode reportar issues de external-data do dataset local; isso bloqueia apply futuro, mas nao bloqueia auditoria read-only nem dry-run.
- Os logs locais de teste podem conter stack trace para diagnostico interno, mas as respostas HTTP validadas nao devem expor esses detalhes.

## Proximo passo

Ampliar a bateria com novos casos reais sanitizados do servidor progressivo, sem adicionar assets privados e sem liberar apply/rollback.
