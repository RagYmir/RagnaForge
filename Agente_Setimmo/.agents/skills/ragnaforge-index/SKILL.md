# Skill: RagnaForge Index

Index rAthena entities (items, NPCs, monsters, maps) into a local cache.

## Usage
`ragnaforge index --entities --json`

## Safety
- Read-only: does not modify rAthena files.
- Validates paths with PathGuard.
- Populates `filesScanned`, `filesParsed`, `filesSkipped`.
- Supports degraded mode if `patchPath` is missing.
- Detects `.lub` files as read-only bytecode.
