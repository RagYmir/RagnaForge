# Admin UI UX, produtividade local e validacao de recursos

Data: 2026-05-12
Escopo: macro-etapa segura da interface administrativa, sem `apply`/`rollback`, sem escrita externa e sem alteracao de `.lub` bytecode.

## Resumo executivo

Nesta rodada a interface administrativa do RagnaForge deixou de ser apenas uma casca de consulta e passou a oferecer um conjunto mais completo de ferramentas locais de analise:

- auditoria visual e padronizacao das telas;
- presets seguros de formulario;
- historico local de `dry-run` e `diff-preview`;
- comparacao simples entre resultados;
- exportacao local em JSON e Markdown;
- melhorias de diff, validacao e dependencia;
- preview passivo de assets;
- reforco explicito de que `apply` e `rollback` nao existem na UI.

Tudo continua restrito a:

- leitura;
- `dry-run`;
- `diff-preview`;
- validacao;
- analise de dependencias;
- armazenamento local no navegador.

## Telas auditadas

- Dashboard
- Configuracao
- Discovery
- GRF / Assets
- Itens
- Equipamentos
- NPCs
- Monstros
- Mapas
- Validacao
- Historico / Relatorios
- Seguranca / API

## Melhorias implementadas

### 1. Auditoria visual e consistencia

- padronizacao do layout de tres regioes;
- warnings e errors sempre visiveis;
- `ProblemDetails` e `correlationId` preservados nas superficies de resultado;
- JSON bruto mantido como aba opcional, nao como unica forma de entender o retorno;
- correcoes de textos/encoding e descricoes de UX em paginas criticas.

### 2. Presets seguros de formulario

Presets adicionados:

- Itens: `Item Etc simples`, `Item com resource`, `Item com asset GRF`, `Item client-side hibrido`
- Equipamentos: `Headgear`, `Robe`, `Weapon`, `Shield restrito`
- NPCs: `NPC sprite padrao`, `NPC sprite custom`, `NPC com identity plan`
- Monstros: `Monstro simples`, `Monstro com drops`, `Monstro com skills`, `Monstro com spawns avancados`
- Mapas: `Mapa existente`, `Mapa novo por GRF`, `Mapa com dependencias`

Regras mantidas:

- preset nao chama API;
- preset nao executa `dry-run`;
- preset nao executa `diff-preview`;
- preset nao aplica nada.

### 3. Historico local

Historico local implementado em `localStorage` com:

- categoria;
- timestamp;
- tipo (`dry-run`, `diff-preview`, `report`);
- payload usado;
- resumo;
- contagem de warnings/errors;
- `correlationId`;
- readiness;
- `CanApply`, quando existir;
- quantidade de arquivos de diff, quando existir.

Recursos atuais:

- listar por categoria;
- limpar categoria;
- limpar tudo;
- reidratar payload no formulario sem executar automaticamente;
- exportar entradas individuais.

### 4. Comparacao simples entre dry-runs

Comparacao local implementada com:

- readiness;
- `CanApply`;
- warnings;
- errors;
- quantidade de arquivos de diff;
- `ClientSidePlan`;
- `VisualClientSidePlan`;
- `ClientIdentityPlan`;
- `AssetPlans`;
- `MapCachePlan`;
- JSON lado a lado.

Primeira versao: comparacao resumida + JSON, sem diff semantico profundo.

### 5. Exportacao local

Exportacao implementada no navegador:

- JSON local;
- Markdown simples;
- resultado individual;
- item do historico;
- comparacao entre dry-runs.

Sem escrita no backend.

### 6. DiffWorkbench

Melhorias:

- agrupamento por `server`, `client`, `assets`, `config`, `map-cache` e `other`;
- contagem de entradas por grupo;
- expandir/recolher;
- copia de texto quando seguro;
- raw diff opcional.

### 7. ValidationMatrix

Melhorias:

- filtros por severidade;
- filtros por tag (`bytecode`, `missing-asset`, `conflict`, `blocked`);
- tabela detalhada de issues;
- categorias e recomendacoes mais claras.

Sem `repair`.

### 8. DependencyTree

Estados suportados:

- `resolved`
- `missing`
- `ambiguous`
- `blocked`
- `read-only`
- `needs-copy-future`

Origens exibidas quando houver:

- `Patch`
- `LocalIndex`
- `LiveScan`
- `LiveScanFallback`
- `ControlledGrfExtraction`
- `Unknown`

### 9. Preview passivo de assets

Estrutura preparada para mostrar:

- path;
- tipo/extensao;
- origem;
- status;
- observacao textual.

Nesta fase:

- sem render visual real de sprite/bitmap;
- sem extracao;
- sem copia;
- sem alteracao de GRF/Patch.

### 10. Validacao de recursos inspirada no RagnarokSDE

A UI agora classifica melhor, em modo read-only:

- inventory BMP;
- collection BMP;
- drag act/spr;
- card illustration;
- headgear;
- weapon;
- robe;
- NPC sprite;
- monster sprite;
- texturas/modelos/sons de mapa.

Ainda nao existe `repair` nem copia automatica.

## Auditoria anti-apply

Confirmacoes desta rodada:

- sem botoes de `apply`;
- sem botoes de `rollback`;
- sem rotas operacionais de `apply`/`rollback`;
- sem chamadas `/apply` ou `/rollback` no cliente HTTP;
- sem uso de `--confirm APPLY` ou `--confirm ROLLBACK` no frontend;
- sem chamadas de CLI pelo navegador.

Referencias a `apply`/`rollback` permanecem apenas como texto informativo de bloqueio.

## Endpoints consumidos

- `GET /health`
- `GET /api/status`
- `GET /api/safety/capabilities`
- `POST /api/config/validate`
- `POST /api/discover`
- `POST /api/grf/index`
- `POST /api/grf/inspect`
- `POST /api/items/dry-run`
- `POST /api/items/diff-preview`
- `POST /api/equipment/dry-run`
- `POST /api/equipment/diff-preview`
- `POST /api/npcs/dry-run`
- `POST /api/npcs/diff-preview`
- `POST /api/monsters/dry-run`
- `POST /api/monsters/diff-preview`
- `POST /api/maps/dry-run`
- `POST /api/maps/diff-preview`

## Validacao executada

### Backend

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`

### Frontend

- `npm run build`
- `npm run test`

### Smoke seguro

Executado em portas locais dedicadas, sem escrita externa:

- API com `RAGNAFORGE_API_KEY` configurada;
- frontend em preview;
- `health` OK;
- `status` OK;
- `config validate` OK;
- `item dry-run` OK;
- `item diff-preview` OK;
- `equipment dry-run` OK;
- `npc dry-run` OK;
- `monster dry-run` OK;
- `map dry-run` OK;
- `POST /api/items/apply` continua `404`.

## Limitacoes restantes

- preview visual real de assets ainda nao existe;
- exportacao continua local ao navegador, sem index de arquivos no workspace;
- historico local ainda nao tem busca/filtros avancados;
- comparacao entre dry-runs ainda e resumida, nao semantica profunda;
- warnings de future flags do React Router continuam na suite de testes.

## Proximos passos recomendados

Opcao A:
- aprofundar preview passivo real de assets, ainda read-only.

Opcao B:
- melhorar validacao server/client inspirada no RagnarokSDE.

Opcao C:
- documentar a politica futura de `apply`/`rollback`, sem implementar nada.

Opcao D:
- rodar bateria de casos reais do servidor progressivo usando apenas `dry-run` e `diff-preview`.
