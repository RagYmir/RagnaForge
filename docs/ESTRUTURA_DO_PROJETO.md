# Estrutura do projeto

```text
Ragna_Forge/
  backend/
  frontend/
  docs/
  data/
    manifests/
    cache/
    indexes/
    logs/
    backups/
  tmp/
  scripts/
  dist/
    api/
    agent/
  Agente_Setimmo/
    src/
    tests/
    docs/
    knowledge/
    inputs/dry-run/
    cache/
    logs/
    reports/
    dist/agente-setimmo/
```

## Pastas importantes

- `backend/`: API read-only/dry-run/diff-preview.
- `frontend/`: interface administrativa.
- `Agente_Setimmo/`: agente local.
- `scripts/`: limpeza, publish, pacote e smoke.
- `data/manifests/`: template e arquivo local de repositorios.
- `dist/`: saida publicada local, ignorada pelo Git exceto placeholders.

## Pastas que so guardam placeholder no Git

- `tmp/`
- `data/cache/`
- `data/indexes/`
- `data/logs/`
- `data/backups/`
- `Agente_Setimmo/cache/`
- `Agente_Setimmo/logs/`
- `Agente_Setimmo/reports/`

Essas pastas devem manter apenas `.gitkeep` no repositorio.
