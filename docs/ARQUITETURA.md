# Arquitetura recomendada

Data: 2026-05-06
Status: Proposta pos-Discovery inicial

## Direcao tecnica

O projeto deve ser uma esteira de implementacao, nao um CRUD generico.

Recomendacao inicial:

- Core em C#/.NET.
- API em ASP.NET Core somente depois do core/dry-run.
- UI posterior: WPF, Avalonia, WinUI, WebView ou frontend web, decidida apos validar preview e integracao GRF.
- GRF Editor integrado por camada adaptadora versionavel.
- Banco local apenas como cache/index/history, nunca fonte da verdade.

O ambiente local possui .NET SDK `10.0.203` e runtimes Desktop/Core/AspNet ate `10.0.7`, entao a trilha .NET e viavel tecnicamente neste computador.

## Principios

- rAthena, Patch/client e GRFs sao fonte da verdade.
- Toda aplicacao deve iniciar por scan.
- Toda proposta deve sair como dry-run.
- Toda escrita deve gerar diff, backup, log e rollback.
- Nunca editar GRF original; gerar Patch/Thor/GRF de saida.
- Preferir `import/custom` sempre que a versao local suportar.
- Equipamento e especializacao de item no dominio.
- Mapas exigem resolver de dependencias proprio.
- GRF Editor nunca deve ficar acoplado ao dominio inteiro.
- Renewal/Pre-Renewal deve ser tratado como perfil progressivo de episodio, nao constante fixa.

## Modulos

```text
backend/
  src/
    Api/
    Application/
    Domain/
    Infrastructure/
    GrfEditorIntegration/
    Grf/
    VisualCatalog/
    AssetIndexing/
    AssetPreview/
    Rathena/
    Patch/
    Validation/
    Diff/
    DryRun/
    Apply/
    Rollback/
    Jobs/
  tests/

frontend/
  src/
    pages/
    components/
    services/
    features/
      items/
      equipments/
      npcs/
      monsters/
      maps/
      assets/
      repositories/
      validation/
      history/

data/
  cache/
  indexes/
  manifests/
  backups/
  logs/

docs/
  STATUS_PROJETO.md
  ARQUITETURA.md
  ANALISE_GRF_EDITOR.md
  PIPELINES.md
  DEPENDENCIAS_RATHENA_PATCH.md
  DECISOES_TECNICAS.md
  ROADMAP.md
```

Esta estrutura deve ser criada somente apos aprovacao explicita, pois o projeto ja contem artefatos de um MVP anterior que precisam ser tratados sem sobrescrever trabalho.

## Camadas

### Domain

Entidades e regras puras:

- `ContentItem`
- `EquipmentItem : ContentItem`
- `NpcScript`
- `Monster`
- `MapDeployment`
- `AssetReference`
- `RepositoryProfile`
- `EpisodeProfile`
- `DependencyGraph`
- `DryRunPlan`
- `ApplyTransaction`
- `RollbackPlan`

### Application

Casos de uso:

- configurar repositorios.
- detectar episodio/perfil ativo.
- catalogar equipamentos visuais e seus temas.
- escanear assets.
- escanear rAthena.
- escanear Patch/client.
- resolver dependencias.
- alocar IDs.
- gerar dry-run.
- gerar diff.
- aplicar com transacao.
- rollback.

### GrfEditorIntegration

Isola uso do GRF Editor:

- `GrfEditorInstallationProbe`
- `GrfEditorCapabilityScanner`
- `GrfCliAdapter`
- `GrfAssemblyProbe`
- `GrfEditorAssemblyLoadContext`
- `GrfAssemblyContainerInspector`
- `GrfEditorLicenseNotice`

### Grf

Contrato independente:

- `IGrfEngine`
- `IGrfScanner`
- `IGrfExtractor`
- `IAssetPreviewProvider`
- `IContainerWriter`

Implementacoes possiveis:

- `GrfCliEngine` via `GrfCL.exe`
- `GrfEditorAssemblyEngine` permitido pela autorizacao direta informada pelo usuario
- `FallbackGrfEngine` futuro

Estado atual do spike:

- `grf inspect` ja usa `GRF.dll` embutida no `GrfCL.exe` para indexacao interna por container.
- o cache desta etapa fica em `data/indexes/`.
- o restante do projeto continua desacoplado dessa dependencia por meio da camada `GrfEditorIntegration`.
- `item dry-run` pode usar `GrfAssemblyAssetLookupService` em modo opt-in para validar assets dentro de GRF.

### Rathena

Scanners/parsers:

- `RathenaRepositoryScanner`
- `EpisodeProfileDetector`
- `RenewalModeDetector`
- `ItemDbScanner`
- `MobDbScanner`
- `NpcScriptScanner`
- `MapRegistryScanner`
- `MapCacheScanner`
- `IdAllocator`

### VisualCatalog

Catalogo local de equipamentos visuais:

- `VisualEquipmentThemeCatalog`
- `VisualEquipmentThemeMatcher`
- `VisualEquipmentThemeManifest`
- filtros por categoria visual e equip location

Objetivo:

- classificar visuais de equipamento por tema estetico;
- orientar busca e preview de assets;
- preparar futuros filtros da interface por `headgear`, `robe`, `shield`, `weapon` e `accessory`;
- manter esse catalogo desacoplado de NPC, monstro e mapa.

### Patch

Scanners/parsers:

- `PatchRepositoryScanner`
- `ClientDateDetector`
- `ItemInfoScanner`
- `DatainfoScanner`
- `SpritePathScanner`
- `MapAssetScanner`
- `ThorBuildScanner`

### Dependency Resolver

Nucleo do sistema:

- recebe alvo de conteudo.
- recebe perfil de episodio/progressao ativo.
- consulta indices de rAthena/Patch/GRF.
- produz lista obrigatoria/condicional/opcional/ausente.
- produz riscos.
- bloqueia apply se faltarem obrigatorias.

### Episode/Profile

O servidor local e progressivo por episodios. O projeto deve modelar isso explicitamente:

- episodio atual.
- modo atual: pre-renewal, renewal ou hibrido.
- data/build do client alvo.
- bancos rAthena ativos para aquele episodio.
- regras client-side ativas para aquele episodio.
- manifest historico de mudancas por episodio.

Essa camada evita que o sistema fique preso ao estado atual do `renewal.hpp`.

### Writer/Generator

Gera mudancas em memoria:

- YAML rAthena.
- scripts NPC.
- conf/import.
- client Lua/txt/lub quando seguro.
- manifest de Patch/Thor/GRF de saida.

### Diff/DryRun/Apply

Fluxo:

1. gerar plano em memoria.
2. gerar diff textual e manifest.
3. confirmar.
4. criar backup.
5. escrever arquivos temporarios.
6. substituir de forma segura.
7. registrar log.
8. validar pos-write.
9. oferecer rollback.

## Decisao sobre UI

Nao decidir UI agora.

Opcoes:

- WPF: melhor reaproveitamento visual do GRF Editor, Windows-only.
- Avalonia: desktop cross-platform, menor compatibilidade direta com WPF do GRF Editor.
- WinUI: Windows moderno, mas maior custo de setup.
- ASP.NET Core + WebView/Web frontend: bom para API e interface administrativa, preview visual exigira adapters.
- React: pode ser usado depois, mas nao e decisao inicial.

Decisao preliminar: primeiro core .NET + scanners + dry-run. A UI entra depois.
