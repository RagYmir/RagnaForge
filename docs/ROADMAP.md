# Roadmap

Data: 2026-05-19

## Fase atual - Consolidacao operacional Ragna_Forge

Status: em validacao

- Renomear o workspace operacional para `Ragna_Forge`.
- Incorporar o Agente Setimmo dentro do projeto principal.
- Manter namespaces/assemblies antigos quando a troca tecnica trouxer risco desnecessario.
- Padronizar scripts de limpeza, publish, smoke e pacote limpo.
- Documentar o uso para pessoas sem familiaridade com a arquitetura.
- Garantir que API/UI continuam read-only e sem apply/rollback.

## Fase 0 - Discovery e documentacao

Status: concluida para Discovery inicial

- Inventariar projeto atual.
- Analisar GRF Editor instalado.
- Criar `DEPENDENCIAS_RATHENA_PATCH.md`.
- Criar `ANALISE_GRF_EDITOR.md`.
- Criar `ARQUITETURA.md`.
- Criar `PIPELINES.md`.
- Criar `DECISOES_TECNICAS.md`.
- Criar `STATUS_PROJETO.md`.

## Fase 1 - Configuracao de repositorios

Status: em andamento

- Cadastrar caminho do GRF Editor.
- Cadastrar rAthena repo.
- Cadastrar Patch/client repo.
- Cadastrar GRFs/Thor/GPF de origem.
- Cadastrar episodio/progressao atual do servidor.
- Validar existencia e permissao read-only.
- Gerar manifest de configuracao.

## Fase 2 - Scanners read-only

Status: iniciado

- Probe de GRF Editor.
- Detector de perfil de episodio/progressao.
- Catalogo local de temas para equipamentos visuais.
- Scanner de GRF/Patch assets.
- Scanner rAthena item DB.
- Scanner IDs usados.
- Scanner Patch itemInfo/datainfo.
- Scanner de mapas.
- Scanner de NPC scripts.

## Fase 3 - Dependency Resolver

- Resolver item.
- Resolver equipamento como item especializado.
- Resolver NPC.
- Resolver monstro.
- Resolver mapa.
- Classificar dependencias obrigatorias, condicionais, opcionais e ausentes.

## Fase 4 - Dry-run e diff

- Proposta de item novo.
- Diff de `db/import/item_db.yml`.
- Diff client-side.
- Alertas de dependencias ausentes.
- Nenhuma escrita real ainda.

## Fase 5 - Apply transacional

Status: iniciado

- Item apply/rollback inicial: concluido.
- Equipamento apply/rollback inicial: concluido.
- NPC apply/rollback inicial: concluido.
- Monstro apply/rollback inicial: concluido.
- Monstro avancado com drops/skills/spawns e validacao pos-write: concluido.
- Mapa apply/rollback inicial: concluido.
- Backup.
- Escrita segura.
- Validacao pos-write.
- Log.
- Rollback.
- Proximo bloco: identidades client-side de NPC custom, client-side avancado e somente depois API/interface.

## Fase 6 - Interface

Status: em andamento, limitada a read-only, dry-run e diff-preview

- Dashboard: concluido.
- Abas administrativas principais: concluido para `Configuracao`, `Discovery`, `Itens`, `Equipamentos`, `NPCs`, `Monstros`, `Mapas`, `GRF/Assets`, `Validacao`, `Historico/Relatorios` e `Seguranca/API`.
- Preview passivo de assets: concluido em modo placeholder/read-only.
- Historico local de dry-runs e diff-previews: concluido no navegador.
- Comparacao simples entre dry-runs: concluido localmente.
- Exportacao local de relatorios: concluido em JSON e Markdown.
- Presets seguros de formulario: concluido.
- Apply/rollback assistido: explicitamente fora de escopo nesta fase.

## Atualizacao 2026-05-11

- NPC client identity segura para `jobname`, `jobidentity` e `npcidentity`: concluida para formatos textuais.
- `.lub` bytecode permanece bloqueado por seguranca.
- Proximo bloco antes de API/interface: client-side avancado para `itemInfo` e clients hibridos.
- Client-side avancado para `itemInfo` e clients hibridos: concluido para deteccao, planejamento, diff/apply textual e bloqueio de bytecode.
- Proximo bloco antes da API/interface: auditoria pre-producao de dry-run/diff-preview ponta a ponta.
- Auditoria pre-producao de dry-run/diff-preview: concluida em modo read-only.
- API backend apenas para read-only, dry-run e diff-preview: iniciada e validada naquele checkpoint com 84/84 testes; a consolidacao final vigente elevou o backend para 141/141.
- Apply/rollback via API deve ficar bloqueado por feature flag/confirmacao forte ate nova decisao.
- API hardening com auth local, ProblemDetails, operation guards, rate/concurrency, CORS restrito e correlationId: concluido e consolidado, com total vigente de 141/141 testes no backend.
- Interface administrativa read-only/dry-run/diff-preview: iniciada.
- Proximo bloco: rodada de UX/auditoria visual da interface e ajuste fino dos fluxos de analise.

## Atualizacao 2026-05-12

- Primeira passada visual da interface concluida para `Dashboard`, `Itens`, `Equipamentos` e `Seguranca/API`.
- Novo layout operacional de tres regioes introduzido: navegacao/lista a esquerda, formulario agrupado ao centro e inspeção/validacao/diff/JSON na lateral.
- UI continua sem `apply`/`rollback`, sem chamadas CLI e sem novos endpoints perigosos.
- Proximo bloco: expandir o mesmo workspace visual para NPC, Monstro, Mapa e GRF, depois fazer uma rodada de refinamento de filtros, historico local e relatorios exportaveis.

### Complemento pass 2

- Segunda passada visual concluida para `NPCs`, `Monstros`, `Mapas`, `GRF/Assets`, `Validacao` e `Historico/Relatorios`.
- O frontend agora compartilha um padrao consistente de lista lateral, formulario agrupado e inspetor de readiness/validacao/diff/JSON nas principais superficies operacionais.
- Proximo bloco real: auditoria visual completa da interface inteira e, depois, produtividade segura com presets, historico local de dry-runs, exportacao de relatorios, comparacao entre dry-runs e preview passivo de assets.

### Complemento macro-etapa produtividade

- Auditoria visual macro concluida com correcoes seguras de consistencia, textos e estrutura operacional.
- `Presets`, `Historico local`, `Comparacao`, `Exportacao local`, `DiffWorkbench`, `ValidationMatrix` e `DependencyTree` ja estao integrados ao frontend.
- Proximo bloco recomendado: ou aprofundar preview passivo real de assets, ou endurecer ainda mais a validacao server/client inspirada no RagnarokSDE, sempre sem liberar `apply`/`rollback`.

### Complemento macro-etapa validacao de recursos

- Validation read-only de recursos consolidada no frontend sem endpoint novo de escrita.
- Preview passivo ampliado para mostrar categoria, origem, proveniencia e path esperado.
- Bateria read-only com casos reais do servidor progressivo concluida para item, equipamento, NPC, monstro, mapa e GRF.
- Politica futura de `apply/rollback` registrada em documento, sem qualquer liberacao operacional.
- Proximo bloco recomendado:
  - preview visual real read-only de icones/sprites; ou
  - parser binario read-only dedicado para `.rsw/.gnd`; ou
  - persistencia segura de `ClientDate`; ou
  - empacotamento local da aplicacao para uso diario.

### Atualizacao 2026-05-12 - Preview Visual Real

- [x] Preview visual real read-only de ícones/assets bitmap (BMP/PNG): concluído.
- [x] Suporte a metadados (SPR/ACT) e preview visual best-effort (SPR) via GrfCL/Reflection: concluído.

- Endpoint `POST /api/assets/preview` endurecido com bloqueio de path traversal e limpeza de temporários.
- Frontend integrado com `PassiveAssetPreviewPanel` visual.
- Proximo bloco recomendado:
  - parser binario read-only dedicado para `.rsw/.gnd` (scan profundo sem string search); ou
  - persistencia segura de `ClientDate` no manifest; ou
  - renderização básica de mapas (.rsw/.gnd) via BabylonJS/ThreeJS (Read-Only); ou
  - suporte a paletas customizadas (.pal) para SPR preview.

### Atualizacao 2026-05-18 - Agent Health e fechamento da branch

- Integracao Agent Health concluida no backend e no frontend, sempre em modo read-only.
- Agent auxiliar agora entra no fluxo oficial de validacao da branch via `status`, `doctor`, `scan`, `index`, `validate`, `baseline` e `health`.
- Apply e rollback continuam fora da API e da interface.
- Totais consolidados desta branch:
  - Backend: 141/141 testes
  - Frontend: 32/32 testes
  - Agent: 183/183 testes

### Atualizacao 2026-05-18 - API Pipeline Workspace v1

- Workspace operacional read-only da API concluido para status, plan, dry-run, diff-preview, issues e reports.
- Frontend ganhou a tela `Pipeline API`, sem botao de apply/rollback e sem input de comando livre.
- Dependency summary agora evita presenca inventada: arquivos nao verificados aparecem como `NotChecked`/`Placeholder`.
- Apply e rollback continuam fora da API/UI; `safeForApply` permanece `false`.
- Totais atuais:
  - Backend: 145/145 testes
  - Frontend: 33/33 testes
  - Agent: 199/199 testes

### Atualizacao 2026-05-19 - Pipeline Real Payload Battery v1

- Bateria real de payloads da Pipeline API concluida para payloads validos, invalidos, traversal, command injection, oversized, concorrencia, repeticao, reports e knowledge.
- `diff-preview` documentado como stateless e sem escrita persistente.
- `429 TooManyRequests` fica restrito a cenarios de stress/rate-limit, sem mascarar testes funcionais normais.
- Proximo passo: rodar a mesma bateria contra amostras sanitizadas do servidor progressivo quando houver novos casos reais.
