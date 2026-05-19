# Install - Agente Setimmo

## Modes

Agente Setimmo can run in three safe modes:

- Development mode: run the CLI through `dotnet run` from the source tree.
- Installed mode: run the portable executable `agente-setimmo.exe` from `dist/agente-setimmo`.
- MCP mode: run `src/RagnaForge.Agent.Mcp` as an MCP stdio server.

Apply real and destructive rollback remain blocked in all modes.

## Install

From the project root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

The installer:

- runs `dotnet publish` for the CLI in Release mode;
- targets `win-x64`;
- prefers a self-contained single-file executable;
- writes `dist/agente-setimmo/agente-setimmo.exe`;
- writes `dist/agente-setimmo/ragnaforge.exe` as a compatibility alias;
- writes `dist/agente-setimmo/ragnaforge.agentroot` with the current agent root;
- adds `dist/agente-setimmo` to the user PATH if needed;
- preserves `config`, `cache`, `logs`, and `inputs`.

Open a new terminal after install so Windows reloads the user PATH.

## Configure Agent Root

The installed executable resolves `agentRoot` in this order:

1. `RAGNAFORGE_AGENT_ROOT`
2. `dist/agente-setimmo/ragnaforge.agentroot`
3. upward search from the executable directory
4. upward search from the current directory

Recommended explicit setup:

```powershell
[Environment]::SetEnvironmentVariable(
  "RAGNAFORGE_AGENT_ROOT",
  "C:\Users\Allis\Desktop\Ragna_Forge\Agente_Setimmo",
  "User"
)
```

For the current terminal only:

```powershell
$env:RAGNAFORGE_AGENT_ROOT = "C:\Users\Allis\Desktop\Ragna_Forge\Agente_Setimmo"
```

If `config/paths.json` cannot be found, the CLI returns JSON with `nextRequiredAction = "configure_agent_root"`.

## Test

```powershell
ragnaforge --version
ragnaforge status --json
ragnaforge doctor --json
ragnaforge apply --json
```

`apply --json` must return `blocked_by_safety_policy`.

Full release check:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1
```

## Update

Pull or copy the new source package, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

The installer overwrites portable binaries but does not delete `config`, `cache`, `logs`, or `inputs`.

## Uninstall

Remove the PATH entry only:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall.ps1
```

Optionally remove the portable binaries:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall.ps1 -CleanDist
```

Uninstall does not delete `config`, `cache`, `logs`, or `inputs`.

## Codex

Use either development mode:

```powershell
dotnet run --project src/RagnaForge.Agent.Cli -- status --json
```

or installed mode:

```powershell
ragnaforge status --json
```

Set `RAGNAFORGE_AGENT_ROOT` when Codex starts outside the project directory.

## Antigravity

Configure Antigravity workflows to call:

```powershell
ragnaforge doctor --json
ragnaforge validate --json
```

Keep `RAGNAFORGE_AGENT_ROOT` set for portable execution.

## MCP

Development MCP:

```powershell
dotnet run --project src/RagnaForge.Agent.Mcp
```

Installed CLI and MCP can coexist. MCP tools remain read-only/planning-only and do not expose real apply or destructive rollback.
