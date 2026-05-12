# 2026-05-07 - Equipment hardening v1

## Resumo

O dry-run de equipamento foi endurecido para bloquear propostas visualmente inseguras antes de qualquer apply.

## Entregas

- Suporte inicial a `weapon` usando `data/luafiles514/lua files/datainfo/weapontable.lub`.
- Suporte restrito a `shield` usando apenas `View` embutido do client local.
- Validacao de `Locations` contra a lista documentada do rAthena local.
- Validacao de `View` maior que zero.
- Validacao de simbolo Lua seguro e prefixo esperado por categoria:
  - `ACCESSORY_` para headgear/accessory.
  - `ROBE_` para robe.
  - `WEAPONTYPE_` ou `WPCLASS_` para weapon.
- Bloqueio de strings inseguras em nomes de sprite/hit sound.
- Deteccao de simbolo visual duplicado em datainfo.
- Deteccao de `ViewID` visual ja usado em `accessoryid.lub`, `spriterobeid.lub` e `weapontable.lub`.
- Lookup de sprite visual solto no Patch e, quando opt-in, dentro de GRF via adapter do GRF Editor.
- `shield` nao usa tabela client-side dedicada equivalente nos datainfo locais; por isso o suporte ficou restrito a `View` embutido.

## Weapon

Para arma, o dry-run agora propoe append em `weapontable.lub`:

- `Weapon_IDs.<symbol> = <view>`
- `WeaponNameTable[Weapon_IDs.<symbol>] = "<sprite>"`
- `Expansion_Weapon_IDs[Weapon_IDs.<symbol>] = Weapon_IDs.<base>`
- `WeaponHitWaveNameTable[Weapon_IDs.<symbol>] = "<hit_sound>"`
- `table.insert(BowTypeList, Weapon_IDs.<symbol>)` quando a base for `WEAPONTYPE_BOW`

O parametro CLI novo e:

- `--weapon-base-type`
- `--weapon-hit-sound`

Se `--weapon-hit-sound` nao for informado, o dry-run infere um som basico pelo tipo base.

## Shield

Para shield, a leitura do Patch local mostrou:

- sprites de item existem em `data/sprite/item` para varios escudos;
- o rAthena local usa `View` pequeno e recorrente em itens `Left_Hand`;
- nao existe `shieldtable.lub` ou equivalente para registrar uma nova classe visual.

Decisao aplicada:

- `shield` aceita apenas `View` embutido: `1, 2, 3, 4, 5, 6`;
- nao gera append em `.lub`;
- qualquer tentativa de informar `client-symbol` ou `client-sprite` para visual custom bloqueia o dry-run.

## Validacao

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- Smoke real `equipment dry-run` para weapon usando `repositories.local.json`.
- Smoke real `equipment diff-preview` para weapon usando `repositories.local.json`.

Resultado: `19/19` testes passaram.

Depois da extensao de shield restrito:

Resultado: `20/20` testes passaram.

## Limites

- Ainda nao existe apply.
- Ainda nao ha parse semantico completo de LUB; o append e propositalmente simples e reversivel.
- Shield visual segue bloqueado ate mapear o pipeline client-side correto.
