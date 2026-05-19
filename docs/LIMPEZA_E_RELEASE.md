# Limpeza e release

## Limpeza segura

```powershell
.\scripts\clean-workspace.ps1
```

Para remover tambem `node_modules`:

```powershell
.\scripts\clean-workspace.ps1 -IncludeNodeModules
```

O script remove apenas artefatos allowlisted, como `bin`, `obj`, `TestResults`, `.trx`, cache/log real e `frontend/dist`.

## Publicacao local

```powershell
.\scripts\publish-all.ps1
```

Saidas:

- `dist/api/`
- `dist/agent/agente-setimmo.exe`

## Pacote limpo

```powershell
.\scripts\package-clean.ps1
```

Saida padrao:

```text
C:\Users\Allis\Desktop\Ragna_Forge_release.zip
```

O pacote nao deve conter:

- `.git`
- `.env`
- `repositories.local.json`
- `Agente_Setimmo/config/paths.json`
- `node_modules`
- `bin`
- `obj`
- cache/log real
- assets privados
- GRF/GPF/THOR
- SPR/ACT/BMP/TGA/GAT/GND/RSW/RSM/PAL
