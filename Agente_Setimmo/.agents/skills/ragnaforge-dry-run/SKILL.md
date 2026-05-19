# Skill: RagnaForge Dry-Run

Plan entity changes (items, NPCs, monsters, maps) without applying them.

## Usage
`ragnaforge dry-run <entityType> --input <file.json> --json`

## Safety
- Blocks path traversal in entity names.
- Blocks absolute paths from input.
- Validates all planned `affectedFiles` with `PlannedPathValidator`.
- Strictly blocks `.lub` modification plans.
- Output restricted to `agentRoot` (logs/operations).
