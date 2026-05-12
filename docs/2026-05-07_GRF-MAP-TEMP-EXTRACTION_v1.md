# 2026-05-07 - GRF map temp extraction

## Objetivo

Permitir que `map dry-run` faca scan profundo de dependencias mesmo quando `.rsw` e `.gnd` existem apenas dentro de GRF, sem alterar GRFs originais, Patch ou rAthena.

## Implementacao

- Novo contrato `IGrfFileExtractor` na camada Application.
- Novo resultado de dominio `GrfFileExtractionResult`.
- Novo adaptador `GrfAssemblyFileExtractor` em `GrfEditorIntegration`.
- O adaptador usa `GRF.dll` carregada a partir do `GrfCL.exe` instalado no GRF Editor.
- A extracao e limitada a entradas selecionadas pelo lookup GRF e a um limite de tamanho controlado.
- A saida temporaria fica sob `tmp/map-dependency-scan/<operacao>` no workspace.
- `map dry-run` apaga a pasta da operacao apos rodar `MapReferencedAssetScanner`.

## Garantias

- Nenhuma escrita em `E:\Ragnarok\Testes\rAthena_teste`.
- Nenhuma escrita em `E:\Ragnarok\Testes\Patch_teste`.
- Nenhuma alteracao nas GRFs originais.
- O relatorio separa a origem do match GRF (`LocalIndex`, `LiveScanFallback`, `LiveScan`) da origem do scan profundo (`LoosePatch`, `ControlledGrfExtraction`).

## Limitacoes

- O scanner profundo atual identifica referencias por strings internas dos arquivos.
- Parser binario dedicado para `.rsw/.gnd` continua recomendado antes de liberar apply automatico de mapas complexos.
- O fluxo ainda e dry-run/diff; apply/rollback de mapa permanece pendente.

## Validacao

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\\tests\\RagnaForge.Tests\\RagnaForge.Tests.csproj`
- Smoke real com `prontera` em `data_0.grf` retornou `DependencyScan.Source = ControlledGrfExtraction`, `DeepScan = true` e `TempEntries = 0`.
