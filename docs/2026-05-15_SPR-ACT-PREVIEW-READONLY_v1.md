# Registro de Funcionalidade: SPR e ACT Read-Only Preview v1 (Hardened)

**Data:** 2026-05-15
**Branch:** `feature/spr-act-preview-readonly`
**Estado:** Implementado com Hardening de Segurança

## Objetivo
Expandir o preview do RagnaForge para suportar arquivos `.spr` e `.act` de forma honesta, segura e 100% read-only.

## Realidade da Implementação v1

### 1. SPR (Best-Effort Visual)
- O sistema tenta extrair e exibir o frame selecionado do sprite.
- **Sucesso Visual:** Depende da capacidade da assembly `GRF.Core.dll` em exportar `PngData`.
- **Fallback:** Se a renderização falhar ou o ativo for gigante (>2048px), o sistema retorna apenas os metadados (contagem de frames).

### 2. ACT (Metadata-Only)
- No v1, o preview de arquivos `.act` é estritamente **Metadata-Only**.
- O sistema extrai a contagem de ações e o índice selecionado, mas **não realiza composição visual** de camadas (layers) com o `.spr` correspondente.
- A interface exibe um placeholder informativo com os metadados extraídos.

## Hardening de Segurança (Industrial Grade)

### 1. PathValidationHelper
- Centralização da lógica de validação de caminhos lógicos.
- Bloqueio de traversal (`..`), caminhos rootados (`:`, `/`) e normalização obrigatória de `\` para `/`.
- Regras estritas para `CompanionEntryPath`: o arquivo `.spr` deve obrigatoriamente estar no mesmo diretório lógico que o `.act`.

### 2. Validação de Fronteira (Boundary)
- Substituição de checagens baseadas em prefixo por `Path.GetFullPath` e `Path.GetRelativePath`.
- Rejeição imediata se o caminho resolvido escapar da raiz permitida (Patch ou GRF Repository).

### 3. Limites de Recursos
- Trava física global de **10MB** para qualquer leitura de ativo.
- Captura de exceções na renderização sem vazamento de stack traces ou caminhos absolutos.

## Validação
- **Backend:** Suite de testes expandida para cobrir ataques de traversal via companion, escape de fronteira e falhas de renderização seguras.
- **Frontend:** Normalização de caminhos no cliente antes do envio para a API.

## Próximos Passos
- Composição visual real de ACT (Layers + SPR).
- Navegação interativa de frames/ações.
