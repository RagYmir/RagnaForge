# 2026-05-07 MAP-DEPENDENCY-SCAN v1

## Objetivo

Ampliar `map dry-run` para revelar dependencias de mapa alem do trio `.rsw/.gnd/.gat`.

## Entregue

- Scanner de referencias embutidas em `.rsw` e `.gnd` acessiveis como arquivos soltos.
- Classificacao de referencias por:
  - `Texture`
  - `Model`
  - `Sound`
  - `Effect`
  - `Sprite`
- Resolucao das referencias primeiro no Patch solto e, quando habilitado, por lookup GRF.
- Exposicao estruturada em `MapDependencyScanResult` no relatorio.
- Resumo por categoria no bloco de dependencias do `map dry-run`.

## Limitacao atual

- Quando o mapa existir apenas em GRF, o scan profundo ainda nao extrai temporariamente `.rsw/.gnd`; nessa situacao o relatorio continua validando o trio base e a proveniencia dos matches GRF.
