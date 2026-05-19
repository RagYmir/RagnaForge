# Dry-Run Inputs Guide

All dry-run operations require a JSON input file describing the entity to be changed.

## Recommended Location
Inputs should be stored in:
`C:\Users\Allis\Desktop\Ragna_Forge\Agente_Setimmo\inputs\dry-run\`

## Security Restrictions
- Input files **must** be located within the `agentRoot` directory.
- Attempts to read input files from external or sensitive directories are blocked.
- The input path is validated by `PathGuard` to ensure it is within a writable root.
- Prefer `inputs/dry-run/` for all operator-provided input files.
- The operation manifest stores the parsed JSON input for auditability. Do not place secrets, API keys, credentials, tokens or private data in dry-run input files.
- Error messages should identify the blocked input rule without exposing more external path detail than necessary.

## JSON Format

### Item
```json
{
  "id": 501,
  "aegisName": "Red_Potion",
  "name": "Red Potion",
  "type": "Heal",
  "buy": 50,
  "sell": 25,
  "weight": 70
}
```

### NPC
```json
{
  "name": "Healer",
  "map": "prontera",
  "x": 150,
  "y": 150,
  "facing": 4,
  "type": "script",
  "script": "heal 100, 100;"
}
```
