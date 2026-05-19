# Triagem de Dados Externos (External-Data Triage) — Agente Setimmo

Este documento detalha o funcionamento do comando `triage` do Agente Setimmo e analisa a integridade dos dados e as 1084 issues externas detectadas durante a validação.

## 1. O Problema das 1084 Issues
Durante a execução do comando `validate`, o Agente Setimmo detecta aproximadamente **1084 issues** no banco de dados do rAthena e arquivos do cliente Ragnarok Online.

Essas issues se dividem em:
- **1 Erro Real**: `ITEM_DUPLICATE_ID_SERVER` no arquivo `item_db_usable.yml` (ID duplicado no rAthena).
- **1083 Warnings (Ruído Esperado)**: Principalmente `MAP_NO_CLIENT_FILES` (1078) e `MAP_INCOMPLETE_CLIENT` (5), localizados no arquivo de índice de mapas (`map_index.txt`).

## 2. Por que as 1084 Issues não Bloqueiam o Trabalho Read-Only?
A governança do Agente Setimmo classifica rigorosamente as falhas com base no seu impacto operacional em três categorias:

- **`safeForReadOnlyWork` (Audit) = `true`**: O trabalho de auditoria, consulta e visualização das tabelas de itens, monstros, NPCs e preview de frames SPR/ACT é 100% seguro. Erros e warnings em dados legados do rAthena não corrompem nem interferem com a capacidade das IAs de lerem o estado atual dos repositórios locais.
- **`safeForDryRun` (Simulação) = `true`**: Simular alterações (dry-run) para ver o diff proposto de um novo item ou NPC é permitido. O cálculo de diff ocorre em memória e gera manifests informacionais sem tocar nos arquivos reais.
- **`safeForApply` (Escrita Direta) = `false`**: Qualquer tentativa de aplicar mudanças reais nos arquivos do rAthena ou Patch do cliente é terminantemente bloqueada enquanto houver erros estruturais ativos, como IDs duplicados de itens.

## 3. Classificação Operacional das Issues (Triage v1)

A triagem agrupa e separa os problemas em categorias acionáveis:

| Categoria | Descrição | Bloqueia Read-Only? | Bloqueia Dry-Run? | Bloqueia Apply? |
|-----------|-----------|----------------------|--------------------|-----------------|
| **Erro Real (1)** | `ITEM_DUPLICATE_ID_SERVER` in `item_db_usable.yml` | Não | Não | **Sim** ❌ |
| **Ruído Esperado (1083)** | `MAP_NO_CLIENT_FILES` no índice do servidor | Não | Não | **Sim** ❌ |

### O que é o "Ruído Esperado"?
São warnings de consistência onde mapas cadastrados no rAthena não possuem arquivos visuais equivalentes (.rsw/.gnd/.gat) mapeados na pasta do Patch/cliente. Em servidores customizados ou em desenvolvimento, isso é comum quando se usam bancos de dados padrão do rAthena, mas nem todos os mapas estão ativados ou extraídos no cliente de teste.

## 4. Como Priorizar e Corrigir os Problemas

Para habilitar a automação completa (`safeForApply = true`), siga os passos abaixo em ordem de prioridade:

### Passo 1: Resolver o Erro Real de ID Duplicado (ITEM_DUPLICATE_ID_SERVER)
1. Localize a issue indicada no relatório (`item_db_usable.yml`).
2. Abra o arquivo e altere o ID duplicado do item em conflito para um ID livre ou remova a duplicata legada.
3. Isso removerá o único blocker crítico estrutural de apply do banco de dados do rAthena.

### Passo 2: Criar um Baseline de Ignore para o Ruído de Mapas
Como a ausência de arquivos visuais de mapas padrão (ex: `yuno_fild07`) não é um bug do seu projeto customizado e sim dados legados inativos, você pode:
1. Criar um baseline de validação informando que esses mapas não devem gerar warnings de consistência.
2. Mover arquivos visuais (.rsw/.gnd/.gat) correspondentes dos mapas ativados para a pasta do cliente `data/` ou GRF correspondente.

## 5. Como Executar a Triagem Localmente

Você pode rodar a triagem de forma segura e rápida a qualquer momento:

### CLI do Agent:
```powershell
dotnet run --project src\RagnaForge.Agent.Cli -- triage --external-data --json
```

### MCP Server (AI/Operator integration):
Chame a tool `ragnaforge_triage` passando `externalDataOnly: true`.

A triagem gerará um relatório detalhado em Markdown em:
`logs/reports/external-data-triage-v1.report.md`
