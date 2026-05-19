# Relatório de Hardening — Agente Setimmo v1.0.2-mvp-clean-final

> Data: 2026-05-16
> Versão Final: **1.0.2-mvp-clean-final**
> Status: **Estável / Auditado**
> Operador: **Google Antigravity**

---

## 1. Métricas Reais de Operação (Snapshot Final)

| Métrica | Valor |
|---|---|
| **Arquivos Scaneados** | **440.970** |
| **Arquivos Parseados** | **810** |
| **Arquivos Ignorados** | **440.160** |
| **Items Indexados** | **76.679** (modo `renewal` + client support) |
| **Monsters Indexados** | **2.677** |
| **NPCs Indexados** | **13.860** |
| **Mapas Indexados** | **1.100** |
| **Duração Index** | **~18-36s local** |
| **Testes Totais** | **148/148 PASS** |

## 2. Implementações de Hardening (v1.0.2)

### Segurança de Paths e IDs
- **OperationIdValidator**: IDs agora são validados por regex `^[a-f0-9]{12}$`. Tentativas de *path traversal* (ex: `../../`) via `operationId` ou `rollbackId` são bloqueadas e retornam JSON de erro.
- **Dry-Run Input Protection**: O path do arquivo de entrada `--input` é validado pelo `PathGuard.EnsureCanRead`.
- **PlannedPathValidator**: Todos os caminhos planejados em um dry-run são sanitizados antes de gerar o manifesto. Bloqueia traversal, caminhos absolutos arbitrários e edições de `.lub`.
- **Index Protection**: O comando `index` agora valida `rathenaPath` e `patchPath` usando o `PathGuard` do perfil ativo antes de qualquer leitura.
- **dbMode**: O perfil ativo define `renewal`, `pre-renewal` ou `hybrid`, evitando falsos erros entre `db/re` e `db/pre-re`.

### Indexação e Parsers
- **Client-Side Support**: Suporte inicial para indexação de itens no lado do cliente (TXT tables). Detecção de arquivos `.lub` (bytecode) tratada como *Read-Only*.
- **Stats Correction**: `filesScanned`, `filesParsed` e `filesSkipped` agora refletem a realidade do sistema de arquivos.
- **Find Fallback**: O comando `find` agora tenta carregar índices específicos (ex: `item_index.json`) caso o índice unificado esteja ausente ou obsoleto.
- **Validate Server/Client**: Mesmo ID entre server e client não é duplicata fatal; `.lub` com `Id = -1` é ignorado; client sem `AegisName` não gera warning indevido.

### Pacote e Limpeza
- **Cleanup**: Todos os artefatos de build (`bin/`, `obj/`) e logs/caches gerados durante o desenvolvimento foram removidos.
- **GitIgnore**: Reforçado para evitar vazamento de logs JSON/MD e caches no repositório.
- **Dry-run Inputs**: Inputs recomendados em `inputs/dry-run/`, com bloqueio de input fora do `agentRoot`.

## 3. Segurança Confirmada (Auditada)

| Regra Absoluta | Status | Confirmação |
|---|---|---|
| rAthena não modificado | ✅ | Agent é estritamente Read-Only em rAthena. |
| Patch/Client não modificado | ✅ | Agent é estritamente Read-Only em Patch. |
| GRF original não modificado | ✅ | Nenhuma ferramenta de escrita em GRF habilitada. |
| .lub não editado | ✅ | Bloqueio explícito em `PathGuard` e `PlannedPathValidator`. |
| Apply real bloqueado | ✅ | Comandos retornam `blocked_by_safety_policy`. |
| Rollback real bloqueado | ✅ | Comandos retornam `blocked_by_safety_policy`. |
| Escrita fora de agentRoot | ✅ | Restrita a `logs/` e `cache/` (dentro do Agent). |

## 4. Testes Hardening (Novos)

- `OperationIdValidator_BlocksTraversal`: ✅
- `DiffCommand_BlocksInvalidId`: ✅
- `DryRunCommand_BlocksNpcNameTraversal`: ✅
- `PlannedPathValidator_BlocksLubEditing`: ✅
- `IndexCommand_ValidatesRathenaPathAccess`: ✅

## 5. Próxima Recomendação

**Integração MCP**: Agora que o Core e a CLI estão endurecidos e auditados, o próximo passo lógico é a criação do `RagnaForge.Agent.Mcp` para expor essas capacidades como ferramentas nativas para IAs via Model Context Protocol.

---
*Relatório gerado automaticamente após hardening de segurança.*
