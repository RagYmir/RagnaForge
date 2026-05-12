# 2026-05-07 - Theme asset lookup v1

## Resumo

O `equipment dry-run` agora usa o tema visual selecionado ou sugerido como ajuda opcional para localizar assets visuais quando o nome exato nao bate.

## Escopo

- sem alterar rAthena, Patch ou GRFs;
- sem substituir o lookup exato existente;
- apenas enriquecendo o relatorio de dry-run com candidatos provaveis.

## Comportamento

Ordem atual:

1. tenta localizar sprite visual por nome exato no Patch;
2. tenta localizar sprite visual por nome exato em GRF, se o lookup estiver ativo;
3. se falhar e houver `VisualTheme.LookupTokens`, executa busca assistida por tema:
   - Patch solto;
   - indice local em `data/indexes`, quando existir;
   - GRF com partial-name match controlado apenas como fallback.

## Regras

- lookup assistido por tema e sempre heuristico;
- ele gera dependencias `Warning`, nao `apply` automatico;
- o tema explicito usa primeiro os `MatchedPatterns` reais do item atual;
- na ausencia de tema explicito, o sistema usa os melhores temas sugeridos para montar os tokens;
- quando existir indice local do container, ele passa na frente do scan GRF ao vivo para a etapa assistida por tema;
- lookup exato de assets GRF tambem passou a preferir `data/indexes` antes do fallback ao vivo.

## Validacao

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- smoke real:
  - `dotnet run --project backend\src\RagnaForge.Cli -- equipment dry-run ... --visual-theme fofo --asset-grf-container data_0.grf`

## Resultado atual

- Patch assistido por tema encontrou candidatos ligados a `rabbit`.
- lookup assistido passou a preferir `data/indexes/*.index.json` antes de revarrer GRF ao vivo.
- GRF assistido por tema continua como fallback quando o indice nao bastar.
- o relatorio final preserva `CanApply = true` enquanto expoe os candidatos como apoio humano antes de qualquer apply.
