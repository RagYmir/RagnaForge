# Skill: RagnaForge Find

Search for items, NPCs, monsters, or maps in the cached entity index.

## Usage
agnaforge find <type> --id <id> --json
agnaforge find <type> --name <name> --json

## Safety
- Read-only search.
- Supports fallback to specific indices (item_index.json, etc.) if unified index is missing.
