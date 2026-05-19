# Antigravity Integration

## Configuração Recomendada

| Setting | Valor |
|---|---|
| Mode | Planning/Plan para tarefas complexas |
| Development | Review-driven development |
| Terminal Execution Policy | Request Review |
| Artifact Review Policy | Pedir revisão para planos, implementação e diffs |
| Terminal Sandbox | Habilitado quando disponível |
| JavaScript Execution Policy | Request Review ou Disabled |
| Non-Workspace File Access | Desativado por padrão |

## Rules

Regras para o Antigravity estão em `.agents/rules/`:
- `ragnaforge-safety.md` — regras de segurança
- `ragnaforge-code-style.md` — regras de estilo de código
- `ragnaforge-ai-operator.md` — regras de operação

## Workflows

Workflows salvos estão em `.agents/workflows/`:
- `audit-agent.md` — auditoria segura
- `safe-status-doctor.md` — diagnóstico seguro
- `scan-project-readonly.md` — scan read-only
- `review-diff.md` — revisão de diff
- `final-report.md` — relatório final

## Skills

Skills compartilhadas estão em `.agents/skills/`:
- `ragnaforge-status/SKILL.md`
- `ragnaforge-doctor/SKILL.md`
- `ragnaforge-scan/SKILL.md`
- `ragnaforge-validate/SKILL.md`
- `ragnaforge-diff/SKILL.md`
- `ragnaforge-report/SKILL.md`

## Uso da CLI

```sh
dotnet run --project src/RagnaForge.Agent.Cli -- status --json
dotnet run --project src/RagnaForge.Agent.Cli -- doctor --json
```

## Acesso a Caminhos Externos

### Estratégia A (recomendada)
- Abrir o workspace em `C:\Users\Allis\Desktop\Ragna_Forge\Agente_Setimmo`
- Usar a CLI do agente para consultar caminhos externos via config
- Não dar acesso amplo ao Desktop inteiro

### Estratégia B (somente se necessário)
- Habilitar acesso fora do workspace temporariamente
- Permitir apenas leituras específicas
- Revisar cada ação
- Desabilitar novamente após a auditoria

## Artifacts Esperados

O Antigravity deve produzir:
- Task list
- Implementation plan
- Safety report
- Test report
- Diff summary
- Final report

## Regras Importantes

- Nunca usar Always Proceed neste projeto.
- Nunca executar comandos destrutivos sem aprovação.
- Operar sem depender de recursos exclusivos do Codex.
- Seguir `docs/AI_AGENT_CONTRACT.md` como contrato principal.

## MCP Preview v1.2.0

Hosts compativeis com MCP podem usar:

```sh
dotnet run --project src/RagnaForge.Agent.Mcp
```

O MCP preview expoe ferramentas, resources e prompts seguros, sem apply real e sem rollback real. Use `.agents/workflows/mcp-smoke-test.md` para validacao rapida.
