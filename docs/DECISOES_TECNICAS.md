# Decisoes tecnicas

Data: 2026-05-06

## D-001: MVP visual anterior fica superseded

Arquivos FastAPI/frontend criados antes desta Discovery nao representam mais a direcao arquitetural aprovada. Eles nao foram apagados porque nao houve autorizacao para remocao. Devem ser tratados como artefato temporario/superseded.

## D-002: Stack nao sera decidida como FastAPI + React por padrao

A analise local do GRF Editor mostra stack .NET/WPF e bibliotecas centrais `GRF.dll`, `ActImaging.dll`, `GrfToWpfBridge.dll`. A decisao inicial e investigar core .NET/C# antes de qualquer UI definitiva.

## D-003: GRF Editor sera integrado por adapter

Nao acoplar dominio ao GRF Editor. Criar `GrfEditorIntegration` e contratos `IGrfEngine`, `IGrfScanner`, `IAssetPreviewProvider`.

## D-004: Equipamento e especializacao de item

No dominio/backend, equipamento herda/compÃµe item base. Aba visual separada e permitida, modelo duplicado independente nao.

## D-005: Fonte da verdade externa

Banco local/cache nao e fonte da verdade. A fonte sao rAthena, Patch/client, GRFs/Thor/GPF e manifests gerados.

## D-006: Mapas tem pipeline proprio

Mapa nao e copia simples de `.rsw/.gnd/.gat`. Deve validar assets, registro rAthena, map_index, map_cache e compatibilidade.

## D-007: Nenhuma escrita em rAthena/Patch sem dry-run

Todos os applies devem exigir diff, backup, log, rollback e confirmacao.

## D-008: Caminhos reais de Discovery

Repositorios informados pelo usuario em 2026-05-06:

- rAthena: `<RATHENA_PATH>`
- Patch/client: `<PATCH_PATH>`
- GRFs/Thor/GPF: `<GRF_REPOSITORY_PATH>`
- GRF Editor: `<GRF_EDITOR_PATH>`

Esses caminhos devem entrar primeiro em configuracao read-only; nenhuma escrita automatica em rAthena/Patch/GRF fica autorizada por esta decisao.

## D-009: Primeiro dry-run deve mirar item legado/TXT

O Patch local contem tabelas TXT antigas (`idnum2item*`, `num2item*`, `itemslotcounttable`) e apenas `system/iteminfo_true.lub` pequeno. Portanto o primeiro dry-run de item deve gerar proposta para TXT legado e validar LUB/datainfo como dependencia separada.

## D-010: Mapas dependem de map_cache neste ambiente

`conf/map_athena.conf` define `use_grf: no`. Assim, implantar mapa no ambiente local exige `map_cache.dat` gerado/atualizado e registro em config/map index, nao apenas arquivos no Patch/GRF.

## D-011: Servidor progressivo por episodios

O usuario confirmou que o servidor e progressivo: hoje as flags Renewal nao aparecem ativas no scan inicial, mas todo mes sera lancado um novo episodio e, em updates futuros, o projeto chegara ao Renewal.

Decisao:

- Nao hard-codear pre-renewal.
- Tratar Renewal/Pre-Renewal como perfil de episodio detectavel/versionado.
- Criar conceito de `EpisodeProfile` ou equivalente no dominio.
- Scanners e generators devem receber o perfil ativo antes de escolher `db/pre-re`, `db/re`, `db/import` e regras client-side.
- O dry-run deve registrar qual episodio/perfil foi usado para gerar a proposta.

## D-012: Projetos iniciais em net10.0

O SDK local e `10.0.203` e os templates disponiveis para `classlib`/`console` ofereceram `net10.0` como alvo .NET moderno direto. Por isso, a estrutura inicial foi criada em `net10.0`.

Esta decisao e reversivel. Se for necessario alinhar com LTS, ajustar para `net8.0` ou `net9.0` depois de configurar templates/SDK apropriados.

## D-013: Politica de reuso do GRF Editor

O GRF Editor possui codigo-fonte publicado publicamente em `https://github.com/Tokeiburu/GRFEditor`, mas a verificacao atual nao encontrou licenca detectada no repositorio nem endpoint de licenca publicado pela API do GitHub.

Decisao operacional:

- Tratar o GRF Editor como `source-available/publico`, nao como biblioteca automaticamente reutilizavel no nosso codigo.
- Permitir uso como referencia tecnica para entender formatos, fluxos e nomes de componentes.
- Permitir integracao por processo externo com `GrfCL.exe` e uso da instalacao existente como ferramenta do ambiente.
- Nao copiar codigo-fonte, nao vendorizar assemblies embutidos e nao redistribuir DLLs do GRF Editor dentro do nosso projeto enquanto nao houver licenca explicita ou permissao clara do autor.
- Se no futuro aparecer licenca explicita ou autorizacao do mantenedor, reavaliar a implementacao de `GrfEditorAssemblyEngine`.

Isso fecha a pendencia de reuso com um criterio conservador de engenharia e compliance, sem bloquear a integracao por adapter.

## D-014: Autorizacao direta do criador do GRF Editor

Em 2026-05-07 o usuario informou que obteve autorizacao direta do criador do GRF Editor para usar a base tecnica do projeto, incluindo DLLs e componentes necessarios para a integracao.

Decisao operacional atual:

- A integracao deixa de ficar restrita a wrapper/processo externo.
- Passa a ser permitido criar uma camada `GrfEditorIntegration` com uso direto de assemblies como `GRF.dll`, `ActImaging.dll` e `GrfToWpfBridge.dll`, desde que o acoplamento fique isolado.
- Continua proibido acoplar o dominio inteiro diretamente ao GRF Editor; o uso deve permanecer encapsulado em adapter/engine dedicado.
- A autorizacao melhora muito a viabilidade de:
  - indexacao interna de GRF;
  - leitura estruturada de assets;
  - preview futuro de sprites/mapas;
  - leitura/decompilacao de formatos relacionados como `.lub`, se a biblioteca suportar isso com estabilidade.
- Ainda devemos registrar no projeto a origem dessa autorizacao e manter os creditos do GRF Editor quando a integracao virar distribuicao.

## D-015: Carregamento direto de assemblies do GRF Editor fica isolado no adapter

Com a autorizacao informada e o spike funcional concluido, a decisao tecnica passa a ser:

- usar `GrfCL.exe` como host para resolver recursos embutidos;
- carregar `GRF.dll` e dependencias por `AssemblyLoadContext` descartavel;
- salvar indices internos somente em `data/indexes/`;
- nao espalhar tipos/reflection do GRF Editor fora de `GrfEditorIntegration`.

Isso nos da acesso ao conteudo interno dos GRFs sem transformar o dominio em reflexo direto da API do editor.

## D-016: Lookup interno de GRF no dry-run de item sera opt-in nesta fase

O lookup por `GRF.dll` funciona, mas alguns containers locais sao grandes. Para preservar tempo, memoria e previsibilidade, o `item dry-run` nao varre GRFs automaticamente por padrao.

Decisao:

- usar arquivos soltos do Patch primeiro;
- usar GRF interno quando o usuario informar `--asset-grf-container` ou `--scan-grf-assets`;
- manter todas as operacoes em read-only;
- futuramente preferir indices completos em `data/indexes/` para evitar re-scan de containers grandes.

## D-017: Diff preview de item sera derivado do proprio dry-run

Nesta fase nao vamos criar um pipeline separado para diff. O proprio `ItemDryRunReport` passa a carregar um `DiffPreview` estruturado, e a CLI expÃµe isso tambem por `item diff-preview`.

Isso reduz duplicacao e garante que:

- o diff sempre reflita exatamente os `ProposedChanges`;
- o usuario possa consumir o relatorio completo ou apenas o diff;
- futuras etapas de apply/rollback partam do mesmo objeto de trabalho.

## D-018: Primeiro equipment dry-run foca nas categorias visuais de menor risco

O primeiro corte de `equipment dry-run` suporta:

- `headgear`
- `accessory`
- `robe`

Motivo:

- essas categorias ja tem tabelas client-side identificadas localmente;
- o append proposto em `.lub` pode ser feito de forma reversivel no fim do arquivo;
- evita prometer suporte total a `weapon`, `shield` e categorias com semantica de client mais variada antes da validacao correta.

Categorias nao cobertas ainda devem aparecer como dependencia ausente, nao como automacao parcial.

## D-019: Weapon liberado no dry-run com bloqueios fortes

Apos checar o Patch local, `weapon` passa a ser suportado em dry-run/diff-preview por append em `data/luafiles514/lua files/datainfo/weapontable.lub`.

Decisao:

- Exigir `--weapon-base-type` para preencher `Expansion_Weapon_IDs`.
- Inferir `WeaponHitWaveNameTable` quando `--weapon-hit-sound` nao for informado.
- Bloquear `ViewID` duplicado antes de propor diff.
- Bloquear simbolo Lua inseguro e prefixo visual incoerente.
- Bloquear `shield` visual por enquanto, porque nao ha tabela dedicada confirmada nos datainfo locais.
- Continuar sem apply: tudo fica restrito a proposta, diff e validacao.

## D-020: Shield liberado apenas em modo restrito

A verificacao do Patch local mostrou sprites de escudo e itens `Left_Hand` com `View` pequeno no rAthena, mas nao mostrou uma tabela client-side dedicada para registrar uma nova classe visual de shield.

Decisao:

- liberar `shield` apenas para `View` embutido do client local;
- aceitar `View` `1..6`, coerente com o banco local analisado;
- nao gerar append em `.lub` para shield;
- bloquear qualquer tentativa de usar `client-symbol` ou `client-sprite` como registro visual novo;
- manter futuras investigacoes abertas caso apareca um pipeline seguro de shield custom.

## D-021: Temas ficam restritos a equipamentos visuais

Depois da confirmacao do usuario, o catalogo de temas nao deve mais ser tratado como sistema generico de visuais.

Decisao:

- o escopo do manifest e `equipment-visuals`;
- o nome canonico da CLI passa a ser `visual-equipment-themes`;
- o catalogo cobre apenas `headgear`, `accessory`, `robe`, `shield` e `weapon`;
- NPC, monstro e mapa ficam fora desse manifest;
- o tema passa a ser atributo/classificacao de equipamento visual, nao entidade solta do projeto.

## D-022: Lookup assistido por tema e auxiliar, nao fonte de verdade

Com a integracao do catalogo de `Equipamentos Visuais` ao `equipment dry-run`, a decisao e:

- manter lookup exato como primeira tentativa;
- usar tema apenas como ajuda opcional de busca/ranqueamento quando o nome exato falhar;
- tratar candidatos encontrados por tema como heuristica de revisao humana;
- nao promover candidato assistido por tema a `apply` automatico sem validacao mais forte.

## D-023: Shield visual custom redireciona para robe quando o client assim indicar

Com a validacao das robe tables locais (`spriterobename.lub` / `spriterobeid.lub`), a decisao passa a ser:

- se o visual com cara de shield ja estiver nas robe tables do client, o pipeline oficial e `robe` / `Costume_Garment`;
- `Left_Hand` shield continua apenas com `View` embutido nesta fase;
- nao abrir pipeline visual novo de shield sem tabela client-side dedicada e verificacao segura.

## D-024: Apply de item precisa ser auditavel mesmo quando bloqueado

`item apply` agora deve registrar:

- conflitos detectados antes de qualquer escrita;
- hashes e contagens de linha por arquivo quando houver escrita;
- trilha de auditoria por etapa;
- log de bloqueio/falha/sucesso dentro do workspace.

Isso reduz risco operacional e prepara o mesmo padrao para NPC, monstro e mapa.

## D-025: Verificacao de sprite NPC nao padrao vira gate proprio

Sprites NPC nao padrao nao podem mais ser tratados como detalhe cosmetico. A decisao e:

- validar sprite padrao em `jobname.lub`, `jobidentity.lub` e `npcidentity.lub` quando legiveis;
- quando nao houver confirmacao nessas tabelas, marcar verificacao client-side adicional;
- permitir dry-run do script server-side, mas deixar o risco de sprite visivel no relatorio.

## D-026: Scan profundo de mapa comeca por artefatos soltos

O primeiro passo seguro para dependencias de mapa e:

- extrair referencias diretamente de `.rsw/.gnd` soltos no Patch;
- resolver texturas, modelos, sons, efeitos e sprites a partir desses caminhos;
- adiar para uma etapa seguinte a extracao temporaria controlada quando o mapa existir apenas em GRF.

## D-027: Monster apply fica restrito a import/custom

O apply inicial de monstro deve ser seguro e reversivel antes de cobrir recursos avancados.

Decisao:

- escrever apenas em `rAthena/db/import` e `rAthena/npc`;
- cobrir `mob_db.yml`, `mob_avail.yml`, `mob_skill_db.txt`, loader e script custom de spawn;
- exigir `--confirm APPLY` para aplicar e `--confirm ROLLBACK` para reverter;
- registrar log mesmo quando o apply for bloqueado;
- validar SHA-256 antes de rollback para evitar rollback cego apos edicao manual;
- deixar drops complexos, client-side de sprite/nome e spawns/eventos avancados para evolucao posterior.

## D-028: Equipment apply fica restrito a import e Patch/data

O apply inicial de equipamento deve seguir o dry-run atual sem abrir escrita ampla no Patch.

Decisao:

- escrever apenas em `rAthena/db/import` e `Patch/data`;
- cobrir `item_db.yml`, tabelas TXT legado e datainfo visual atualmente mapeado;
- exigir `--confirm APPLY` para aplicar e `--confirm ROLLBACK` para reverter;
- registrar log mesmo quando o apply for bloqueado;
- validar SHA-256 antes de rollback para evitar rollback cego apos edicao manual;
- bloquear ID/AegisName duplicado, ID legado duplicado, simbolo visual duplicado e `ViewID` duplicado antes de escrever;
- manter shield visual custom fora do apply enquanto o client local nao oferecer pipeline seguro.

## D-029: Lookup NPC via GRF deve preservar a origem do match

Quando o sprite NPC nao existir solto no Patch, o dry-run pode recorrer ao lookup GRF, mas o relatorio precisa manter a origem desse match.

Decisao:

- continuar tratando sprite NPC custom como caso de validacao adicional;
- expor no `DetectionSource` se o match veio de `local-index`, `live-scan-fallback` ou `live-scan`;
- manter `AssetLookup` no relatorio para auditoria;
- nao transformar esse match em alteracao automatica de `jobname/jobidentity/npcidentity` enquanto nao houver pipeline seguro para esses arquivos.

## D-030: Map apply usa staging binario e adaptador de map cache

Com o bloco inicial de mapa concluido, a decisao tecnica fica:

- `map dry-run` deve expor `AssetPlans` e `MapCachePlan`, nao apenas diff textual;
- rename binario entre `MapName` e resource names de `.rsw/.gnd/.gat` fica bloqueado nesta fase;
- apply de mapa escreve somente em `rAthena/db/import`, `rAthena/conf` e `Patch/data`;
- assets vindos de GRF sao extraidos de forma controlada para uma raiz temporaria e copiados em staging para o Patch;
- `map_cache.dat` deve ser reconstruido por adaptador proprio (`RathenaMapCacheBuilder`) em arquivo de staging antes da substituicao final;
- rollback de mapa valida SHA-256 do estado aplicado antes de restaurar backups ou apagar arquivos criados.

## D-031: Monstro avancado valida item base/import, skill_db e staging sintatico antes do apply

Com a cobertura avancada de monstro concluida nesta rodada, a decisao tecnica fica:

- validar drops contra os bancos reais de item do rAthena, incluindo `db/item_db.yml`, `db/import/item_db.yml`, `db/pre-re/item_db*.yml` e `db/re/item_db*.yml`;
- validar skills contra `skill_db.yml` local e manter o suporte de escrita restrito ao `mob_skill_db.txt` classico de 19 colunas;
- permitir multiplos drops, multiplas skills e multiplos spawns no dominio, mantendo compatibilidade com os argumentos simples da CLI;
- bloquear quantity por drop e campos avancados de skill/spawn ainda nao mapeados, com erro claro no dry-run;
- montar o arquivo final em staging dentro do backup da operacao e executar validacao sintatica de YAML/TXT/script antes da substituicao definitiva;
- manter rollback automatico em falha de apply e rollback manual protegido por SHA-256.

## D-032: NPC custom usa identity plan seguro e bloqueia bytecode

Com o fechamento do bloco de NPC client-side, a decisao tecnica fica:

- detectar `jobname`, `jobidentity` e `npcidentity` no Patch real, sem assumir caminho ou extensao unica;
- classificar cada arquivo como `TextLua`, `TextLub`, `BinaryLub`, `LegacyTxt`, `Missing` ou `Unknown`;
- permitir diff/apply apenas para `TextLua`, `TextLub` e `LegacyTxt`;
- bloquear `BinaryLub` e `Unknown`, com motivo claro no dry-run e no diff-preview;
- preservar a proveniencia do sprite custom quando ele for resolvido por Patch, indice GRF local, `live-scan-fallback` ou `live-scan`;
- nao copiar assets de GRF para o Patch automaticamente nesta etapa;
- permitir `--allow-server-only` apenas como escape explicito quando o server-side estiver seguro;
- validar o arquivo completo em staging antes da substituicao final e proteger rollback por SHA-256 tambem nos arquivos client-side textuais.

## D-033: Client-side de item/equipamento usa plano comum e bloqueia bytecode

Com o bloco de `itemInfo` e clients hibridos, a decisao tecnica fica:

- consolidar `ClientSidePlan` como contrato comum para item e equipamento;
- classificar arquivos client-side como `TextLua`, `TextLub`, `BinaryLub`, `LegacyTxt`, `Missing` ou `Unknown`;
- aceitar `TextLub` apenas quando o detector confirmar texto;
- bloquear `BinaryLub` e `Unknown`;
- detectar clients `ItemInfo`, `LegacyTxt`, `Hybrid` ou `Unknown`;
- no Patch atual, tratar `Hybrid` como seguro para escrita em TXT legado completo, mantendo `itemInfo_true.lub` textual como contexto read-only;
- validar arquivo final em staging antes do apply real;
- manter asset copy de sprites/icones fora desta rodada.

## D-034: API backend deve nascer read-only/dry-run/diff-preview

A auditoria pre-producao de 2026-05-11 mostrou que os pipelines estao maduros para exposicao por API em modo seguro, mas apply/rollback ainda exigem controles humanos mais fortes.

Decisao:

- liberar primeiro endpoints read-only, discovery, indexacao, dry-run e diff-preview;
- nao liberar apply/rollback na primeira passagem da API, ou deixa-los atras de feature flag, autenticacao forte e confirmacao explicita;
- manter map apply bloqueado para API ate existir um caso real de mapa sem ambiguidade/dependencia pendente;
- exigir decisao manual futura para persistir `ClientDate = 2025-07-16` no manifest ou manter deteccao dinamica por `discover`;
- exigir politica de asset obrigatorio por categoria antes de permitir item/equipment apply por API;
- manter `BinaryLub` e `Unknown` como bloqueantes absolutos.

## D-035: Primeira API nao expoe endpoints de escrita

A primeira implementacao real da API confirma a decisao de seguranca:

- criar projeto ASP.NET Core separado em `backend/src/RagnaForge.Api`;
- reaproveitar servicos existentes de discovery, GRF, dry-run e diff-preview;
- expor `/health`, `/api/status`, `/api/safety/capabilities`, `/api/config/validate`, `/api/discover`, `/api/grf/*` e endpoints de dry-run/diff-preview por categoria;
- nao mapear rotas de `apply` ou `rollback`;
- declarar writes desabilitados em `ApiSafetyPolicy`;
- resolver automaticamente o workspace root quando a API for iniciada a partir da pasta do projeto API;
- adiar autenticacao, autorizacao e feature flags de escrita para uma etapa propria antes de qualquer uso de apply via HTTP.

## D-036: API local exige chave e padroniza erros antes da interface

Antes de criar interface administrativa, a API passa a ter uma camada minima de seguranca operacional.

Decisao:

- exigir `X-RagnaForge-Api-Key` em `/api/*` por padrao;
- manter `/health` e `/openapi/v1.json` publicos;
- manter `ReadOnlyMode = true`, `EnableApplyEndpoints = false` e `EnableRollbackEndpoints = false` por default;
- usar `ApiOperationGuard` para bloquear qualquer `Apply`, `Rollback`, `FileWrite`, `ExternalRepoWrite` ou `GrfWrite`;
- aceitar `CacheWrite` somente para caches/indices dentro do workspace;
- usar `ProblemDetails` com `errorCode` e `correlationId`;
- usar `ApiResponse<T>` para sucesso;
- aplicar rate limit e concurrency guard em memoria;
- restringir CORS aos origins locais configurados;
- documentar API key no OpenAPI sem expor segredo.

## D-037: Interface administrativa nasce apenas como cliente de endpoints seguros

Com a API endurecida, a interface administrativa pode comecar sem reabrir risco de escrita.

Decisao:

- usar React + TypeScript + Vite como shell administrativo;
- consumir apenas `status`, `config`, `discover`, `grf`, `dry-run` e `diff-preview`;
- nao criar botoes, rotas ou atalhos de `apply`/`rollback`;
- nao chamar CLI pelo frontend;
- exibir `ReadOnlyMode`, capabilities e bloqueios de seguranca de forma explicita;
- persistir `API Base URL` e `API key` apenas no navegador local nesta etapa;
- tratar `ProblemDetails` e `ApiResponse<T>` como contratos obrigatorios da UI;
- deixar `Auditoria/Relatorios` como placeholder ate existir endpoint read-only dedicado.

## D-038: Primeira passada visual da UI se inspira no RagnarokSDE, sem copiar codigo

Com a analise do RagnarokSDE concluida, a primeira rodada visual do RagnaForge adota apenas referencias conceituais e de experiencia.

Decisao:

- manter React/Vite/TypeScript e a arquitetura atual da UI;
- nao copiar codigo, XAML ou fluxo WPF do RagnarokSDE;
- usar como referencia apenas organizacao de grids, agrupamento de campos e leitura operacional de dados tecnicos;
- introduzir layout de tres regioes para modulos densos: lista/navegacao, formulario agrupado e painel de resultado/validacao/diff/JSON;
- manter `apply` e `rollback` totalmente fora da interface nesta fase;
- priorizar `Dashboard`, `Itens`, `Equipamentos` e `Seguranca/API` antes de expandir a mesma linguagem visual para NPC, Monstro, Mapa e GRF.

## D-039: Passada visual 2 consolida o workspace unico para os modulos restantes

Depois da primeira rodada, a interface passa a replicar a mesma linguagem operacional em `NPCs`, `Monstros`, `Mapas`, `GRF/Assets`, `Validacao` e `Historico/Relatorios`.

Decisao:

- reaproveitar componentes existentes antes de criar variantes paralelas;
- tratar `ClientIdentityPlan`, grids de `drops/skills/spawns`, arvore de dependencias de mapa e tabelas de GRF como extensoes do mesmo workspace;
- manter `Validacao` e `Historico/Relatorios` como superficies read-only, mesmo quando alimentadas por placeholder ou estado local futuro;
- continuar expondo `warnings`, `errors`, `ProblemDetails` e `correlationId` como elementos visiveis da UX, nao como detalhe escondido em JSON bruto;
- preservar a ausencia total de `apply`/`rollback` na UI, inclusive nas abas novas.

## D-040: Produtividade da UI continua estritamente local e read-only

Com a macro-etapa de produtividade da interface concluida, a decisao tecnica fica:

- `presets` existem apenas para preencher formularios; eles nunca disparam API automaticamente;
- historico, comparacao e exportacao vivem apenas no navegador (`localStorage` e downloads locais);
- nenhum desses recursos escreve no backend, no workspace, em rAthena, no Patch ou em GRFs;
- preview de assets permanece passivo e textual nesta fase, sem extracao, copia ou alteracao de conteudo;
- qualquer futura evolucao visual de assets ou relatorios deve continuar reaproveitando endpoints seguros existentes antes de considerar novos endpoints.

## D-041: A UI nao deve mais expor rotas operacionais de apply ou rollback

Depois da auditoria anti-apply do frontend, a decisao passa a ser:

- remover rotas navegaveis de `apply` e `rollback` da interface;
- manter apenas mensagens informativas em `Seguranca/API` e banners explicando que essas operacoes nao existem nesta fase;
- tratar qualquer referencia visual a `apply`/`rollback` como documentacao de bloqueio, nunca como affordance acionavel;
- manter o cliente HTTP sem chamadas para `/apply` ou `/rollback`, mesmo que no futuro alguem adicione endpoints por engano no backend.

## D-042: Validacao de recursos continua agregada a endpoints seguros e historico local

Com a rodada de validacao de recursos e preview passivo ampliado, a decisao tecnica fica:

- preferir consolidar a validacao no frontend usando `status`, `config validate`, `discover`, `grf index/inspect`, `dry-run`, `diff-preview` e historico local antes de criar novos endpoints;
- tratar `Validation Center` como superficie read-only de triagem e risco, nao como repair center;
- exibir recursos e issues em estados claros (`resolved`, `missing`, `ambiguous`, `blocked`, `read-only`, `needs-copy-future`);
- manter preview passivo como classificacao e proveniencia textual ate existir um endpoint seguro de leitura visual;
- nao criar endpoint novo de validacao enquanto os contratos atuais ja forem suficientes para o fluxo de analise.

## D-043: Politica futura de apply/rollback continua documental e por rollout de categoria

Com a API e a UI ainda em modo seguro, a decisao tecnica fica:

- manter `apply/rollback` fora da API e da interface ate existir politica formal de auth forte, autorizacao por papel, diff review obrigatoria, checklist de risco, staging obrigatorio e logs/auditoria;
- bloquear desde ja qualquer ideia de liberar `map apply` primeiro;
- tratar `.lub` bytecode, asset critico ausente, conflito de hash, rename binario de mapa e ambiguidade critica como bloqueios absolutos mesmo no futuro;
- liberar escrita, se um dia existir, por categoria e com feature flags desligadas por padrao.
