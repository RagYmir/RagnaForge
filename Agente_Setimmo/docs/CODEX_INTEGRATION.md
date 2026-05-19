# Codex Integration

## AGENTS.md

O arquivo `AGENTS.md` na raiz do agente é o contrato operacional para o Codex. Ele contém:
- Diretórios configuráveis
- Regras obrigatórias
- Fluxo padrão
- Comandos disponíveis

## Skills

Skills compatíveis com Codex estão em `.agents/skills/`:
- `ragnaforge-status/SKILL.md`
- `ragnaforge-doctor/SKILL.md`
- `ragnaforge-scan/SKILL.md`
- `ragnaforge-validate/SKILL.md`
- `ragnaforge-diff/SKILL.md`
- `ragnaforge-report/SKILL.md`

Cada SKILL.md tem metadata compatível com o formato Codex.

## .codex

O diretório `.codex/` pode conter configuração específica do Codex:
- `config.example.toml` — template de configuração

## Uso da CLI

O Codex deve usar a CLI local e consumir JSON:

```sh
ragnaforge status --json
ragnaforge doctor --json
```

## Uso Futuro de MCP

Quando o MCP estiver implementado, o Codex poderá consumir ferramentas MCP em vez de parsear output de CLI. Ver `docs/MCP.md`.

## Comandos Recomendados

```sh
ragnaforge status --json        # Verificar estado
ragnaforge doctor --json        # Validar saúde
ragnaforge scan --project --json # Scan read-only (futuro)
ragnaforge validate --json      # Validar consistência (futuro)
ragnaforge diff --operation <id> --json # Revisar diff (futuro)
```

## Regras para o Codex

1. Não abrir arquivos grandes sem necessidade.
2. Não aplicar mudanças perigosas sem revisão humana.
3. Preferir consumir JSON do agente local.
4. Tratar AGENTS.md como contrato operacional.
5. Consultar `docs/AI_AGENT_CONTRACT.md` como contrato neutro.
6. Operar sem depender de recursos exclusivos do Antigravity.

## MCP Preview v1.2.0

Codex pode consumir o servidor MCP local por stdio. Ver `docs/MCP.md`.

```json
{
  "mcpServers": {
    "ragnaforge": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Users\\Allis\\Desktop\\Agente Setimmo\\src\\RagnaForge.Agent.Mcp"
      ]
    }
  }
}
```

Ferramentas destrutivas nao existem no MCP preview. `ragnaforge_apply` e `ragnaforge_rollback_confirm` sao bloqueadas por politica. Resources e prompts tambem sao read-only.
