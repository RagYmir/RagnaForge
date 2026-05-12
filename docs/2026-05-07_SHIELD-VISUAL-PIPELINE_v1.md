# 2026-05-07 - Shield visual pipeline v1

## Resumo

O client local mostra que parte dos visuais com aparência de shield não usa uma tabela dedicada de shield. Em vez disso, alguns deles aparecem no pipeline de robe/costume garment.

## Evidência local

Foram encontrados no Patch local exemplos como:

- `ROBE_Ten_G_Shield_TW`
- `ROBE_C_Lord_Of_Death_Shield`
- `ROBE_C_Defense_Shield`

em:

- `data\luafiles514\lua files\datainfo\spriterobeid.lub`
- `data\luafiles514\lua files\datainfo\spriterobename.lub`

## Decisão técnica

- `shield` real de `Left_Hand` continua restrito a `View` embutido `1..6`;
- registro novo com `client-symbol` ou `client-sprite` continua bloqueado para `shield`;
- quando o sprite informado já aparece nas robe tables, o dry-run avisa que pode ser caso de `visual-category robe` com `Costume_Garment`.

## Implicação prática

Nem todo conteúdo que visualmente parece escudo deve seguir o pipeline de shield do equipamento normal. Alguns pertencem ao pipeline de costume/robe do client.

## Validacao

- smoke real:
  - `dotnet run --project backend\src\RagnaForge.Cli -- equipment dry-run --config data\manifests\repositories.local.json --aegis RF_SHIELD_HINT --name "RagnaForge Shield Hint" --resource rf_shield_hint --type Armor --locations Left_Hand --visual-category shield --view 3 --client-symbol SHIELD_RF_HINT --client-sprite C_Lord_Of_Death_Shield`

## Resultado atual

- o relatório agora emite warning apontando `spriterobename.lub` quando detecta esse padrão;
- o bloqueio de shield custom continua ativo por segurança.
