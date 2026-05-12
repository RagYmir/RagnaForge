# 2026-05-07 - Visual equipment themes v1

## Resumo

O catalogo de temas foi alinhado para tratar explicitamente de `equipamentos visuais` do jogo, e nao de assets genericos.

## Decisao

- O escopo do manifest agora e `equipment-visuals`.
- O nome canonicamente exposto pela CLI passa a ser `visual-equipment-themes`.
- O arquivo padrao passa a ser `data/manifests/visual-equipment-themes.local.json`.
- `visual-themes` permanece como alias de compatibilidade na CLI.

## Modelo

Cada tema agora descreve:

- `Categories`: categorias de equipamento visual como `headgear`, `accessory`, `robe`, `shield`, `weapon`.
- `EquipLocations`: slots/equip locations do rAthena que ajudam o catalogo a orientar filtros e futuras validacoes.
- `Tags` e `ResourceNamePatterns`: pistas para classificacao e busca.

## Integracao com dry-run

O `equipment dry-run` agora consegue consumir esse catalogo de duas formas:

- com `--visual-theme <key>`, validando o tema escolhido e anexando o resultado ao relatorio;
- sem tema explicito, sugerindo temas compativeis quando o manifest local existir.

Nesta fase, o catalogo ainda nao altera proposta de arquivo nem bloqueia apply. Ele funciona como classificacao orientativa, validacao auxiliar de `equipamentos visuais` e ajuda opcional no lookup de assets quando o nome exato falha.

## Fora de escopo

Este catalogo nao cobre:

- NPCs
- monstros
- mapas
- temas de interface

Se no futuro quisermos taxonomias para esses dominios, elas devem nascer como catalogos separados.

## Validacao

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- `dotnet run --project backend\src\RagnaForge.Cli -- visual-equipment-themes init --out data\manifests\visual-equipment-themes.local.json --force`
- `dotnet run --project backend\src\RagnaForge.Cli -- visual-equipment-themes validate --config data\manifests\visual-equipment-themes.local.json`
- `dotnet run --project backend\src\RagnaForge.Cli -- visual-equipment-themes list --config data\manifests\visual-equipment-themes.local.json`
- `dotnet run --project backend\src\RagnaForge.Cli -- equipment dry-run ... --visual-theme fofo`
