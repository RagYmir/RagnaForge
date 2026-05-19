# Agente Setimmo - AI Agent Contract

This root-level pointer keeps integrations that expect `AI_AGENT_CONTRACT.md` at the Agent root compatible.

Authoritative contract:

- `docs/AI_AGENT_CONTRACT.md`

Safety summary:

- Use `baseline --json` or `health --json` before broad work.
- Treat apply as blocked.
- Treat real rollback as blocked.
- Do not write to rAthena, Patch/client, GRF, or `.lub`.
- Use scan, index, validate, dry-run, diff, and report for safe operation.
