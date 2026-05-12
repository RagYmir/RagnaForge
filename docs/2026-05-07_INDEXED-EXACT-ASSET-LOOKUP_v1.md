# 2026-05-07 - Indexed exact asset lookup v1

## Resumo

O lookup exato de assets GRF para `item dry-run` e `equipment dry-run` agora consulta `data/indexes/*.index.json` antes de tentar scan direto do container.

## Escopo

- sem alterar rAthena, Patch ou GRFs;
- sem remover o fallback ao vivo;
- apenas reduzindo custo e dependencia de re-scan quando o indice local ja existir.

## Comportamento

Ordem atual do lookup exato:

1. tenta carregar o indice local do container em `data/indexes`;
2. procura match exato por nome/extensao dentro do indice;
3. se o indice estiver ausente, truncado ou inconclusivo, cai para o lookup ao vivo;
4. se o indice encontrar match suficiente, retorna sem depender do scan ao vivo.

## Regras

- indice local e apenas cache auxiliar, nunca fonte da verdade;
- resultado negativo so e confiavel quando o indice existe e nao esta truncado;
- fallback ao vivo continua ativo para preservar cobertura quando o indice for insuficiente;
- o comportamento vale para lookup exato e convive com o lookup assistido por tema.

## Validacao

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- smoke real:
  - `dotnet run --project backend\src\RagnaForge.Cli -- grf inspect --config data\manifests\repositories.local.json --container "C:\Users\Allis\Desktop\New project\tmp\grf-smoke\sample.grf" --limit 10 --force`
  - `dotnet run --project backend\src\RagnaForge.Cli -- item dry-run --config data\manifests\repositories.local.json --aegis RF_Index_Smoke --name "RagnaForge Index Smoke" --resource sample --type Etc --buy 10 --sell 5 --weight 10 --identified-desc "Linha 1|Linha 2" --asset-grf-container "C:\Users\Allis\Desktop\New project\tmp\grf-smoke\sample.grf"`
  - `dotnet run --project backend\src\RagnaForge.Cli -- equipment dry-run --config data\manifests\repositories.local.json --aegis RF_Index_Weapon --name "RagnaForge Index Weapon" --resource sample --type Weapon --identified-desc "Linha 1|Linha 2" --locations Right_Hand --visual-category weapon --view 77777 --client-symbol WEAPONTYPE_SAMPLE_INDEX --client-sprite sample --weapon-base-type SWORD --weapon-level 1 --buy 10 --sell 5 --weight 10 --asset-grf-container "C:\Users\Allis\Desktop\New project\tmp\grf-smoke\sample.grf"`

## Resultado atual

- `IndexedGrfAssetLookupService` virou a porta padrao para lookup GRF na CLI.
- `item dry-run` e `equipment dry-run` passaram a reaproveitar indices locais ja existentes.
- o resultado de lookup agora preserva proveniencia explicita com `Source`, `LocalIndexesLoaded` e `LiveContainersScanned`.
- o smoke controlado com `sample.grf` confirmou o fluxo completo com proposta/diff e sem escrita em repositórios externos.
