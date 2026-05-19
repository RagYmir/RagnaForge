# MCP Security

Agente Setimmo MCP v1 is a read-only orchestration surface.

## Guarantees

- Real apply is blocked.
- Real rollback is blocked.
- No shell tool is registered.
- No arbitrary command runner is registered.
- Path traversal is blocked for MCP arguments and resources.
- Absolute paths are blocked in MCP arguments and resources.
- Responses are size-limited.
- Resources stay inside `agentRoot`.

## Allowed side effects

The only planned MCP write side effect is local dry-run input/report material under `agentRoot`, used to produce read-only manifests, diffs, and reports. It must never write to rAthena, Patch/client, GRF, or `.lub`.

## Blockers

Any future MCP change that exposes apply, real rollback, shell, arbitrary command execution, or external file writes must be rejected until a separate human-approved policy exists.
