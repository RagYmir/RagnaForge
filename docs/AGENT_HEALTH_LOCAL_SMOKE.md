# RagnaForge Agent Health - Local Smoke Test

This document describes the local smoke flow for the read-only integration between the RagnaForge API/UI and the local RagnaForge Agent.

## Scope

- Endpoint: `GET /api/agent/health`
- UI page: `Agent Health`
- Agent version validated in this cycle: `1.2.0-operational-ux`
- Safety mode: read-only diagnostics only

The integration must never expose apply, rollback, repair, shell, or free-form command execution.

## Local Configuration

Configure the Agent path outside production defaults. Prefer environment variables, `appsettings.Development.json`, or an ignored local settings file.

Example shape:

```json
{
  "RagnaForge": {
    "Agent": {
      "AgentExePath": "<AGENT_ROOT>\\dist\\ragnaforge\\ragnaforge.exe",
      "AgentCacheDir": "<AGENT_ROOT>\\cache\\agent",
      "AgentTimeoutSeconds": 30
    }
  }
}
```

Do not turn workstation-specific absolute paths into universal project defaults.

## Backend Smoke

Start the API from the RagnaForge project root:

```powershell
dotnet run --project backend\src\RagnaForge.Api\RagnaForge.Api.csproj
```

Call the endpoint with the configured API key:

```powershell
Invoke-RestMethod `
  -Uri "http://127.0.0.1:5099/api/agent/health" `
  -Method Get `
  -Headers @{ "X-RagnaForge-Api-Key" = "<LOCAL_API_KEY>" }
```

Expected result:

- `success: true`
- `readOnlyMode: true`
- `operationKind: "ReadOnly"`
- `data.agentReachable: true`
- `data.statusOk: true`
- `data.doctorOk: true`
- `data.safety.applyBlocked: true`
- `data.safety.rollbackRealBlocked: true`
- `correlationId` present

If the Agent executable is unavailable or times out, the endpoint should return a safe structured response or ProblemDetails without exposing raw shell state or secrets.

## UI Smoke

Start the frontend:

```powershell
cd frontend
npm run dev
```

Open the UI, configure the API base URL and key, then open `Agent Health`.

Expected UI behavior:

- Shows Agent online/offline state.
- Shows status, doctor, scan/index, and validate summaries when available.
- Shows stale-cache warnings if backend reports cache warnings.
- Shows `safeForReadOnlyWork`, `safeForDryRun`, and `safeForApply`.
- Shows apply blocked and rollback real blocked.
- Does not render apply buttons.
- Does not render rollback buttons.
- Does not render a command input.

## Health Interpretation

- `safeForReadOnlyWork`: true when the Agent considers read-only audit work safe.
- `safeForDryRun`: true when dry-run and diff-preview work are allowed.
- `safeForApply`: false while validation, policy, or environment state blocks future writes. The API/UI still do not expose apply even if this value changes in a future policy.
- `applyBlocked`: apply is unavailable through this integration.
- `rollbackRealBlocked`: real rollback is unavailable through this integration.
- `cacheMatchesFingerprint`: false means the cached scan/index should not be trusted for counts until the Agent scan/index is refreshed.

Dataset issues may make `safeForApply=false` while still allowing read-only audit and dry-run work.

## Common Failure Modes

### Agent executable unavailable

The backend cannot find or execute `AgentExePath`.

Action: fix local configuration, then rerun the smoke.

### Timeout

The Agent command did not finish inside `AgentTimeoutSeconds`.

Action: rerun `ragnaforge status --json`, `ragnaforge doctor --json`, and refresh scan/index directly in the Agent if needed. Increase the local timeout only when the machine is known to need it.

### Stale cache

The cached `project_index.json` or `entities_index.json` does not match the active profile/fingerprint.

Action: run the matching Agent scan/index command and retry. The UI must not present stale counts as trusted.

## Security Guarantees

- Backend command allowlist only permits:
  - `status --json`
  - `doctor --json`
  - `scan --project --json`
  - `index --entities --json`
  - `validate --json`
- `UseShellExecute=false`.
- No user-supplied command string is accepted.
- No apply command is allowlisted.
- No rollback command is allowlisted.
- No external rAthena, Patch/client, GRF, or `.lub` write is performed.
