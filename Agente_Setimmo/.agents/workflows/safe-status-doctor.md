# safe-status-doctor

Use only safe operational commands.

## Allowed

- `dotnet build`
- `dotnet test`
- `ragnaforge status --json`
- `ragnaforge doctor --json`
- `ragnaforge baseline --json`
- `ragnaforge health --json`
- `git status`
- `git branch --show-current`
- `git log --oneline -n 10`

## Blocked

- apply
- real rollback
- delete / rm / rmdir / del / format
- GRF editing
- `.lub` editing
- unreviewed path rewrites
