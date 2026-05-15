# Registro de Funcionalidade: Asset Preview Visual Read-Only v1

**Data:** 12 de Maio de 2026
**Branch:** feature/asset-preview-readonly
**Objetivo:** Implementar preview visual seguro (read-only) de assets BMP, PNG, JPG, JPEG e WEBP sem riscos de vazamento ou mutação.

## Decisões Técnicas

1. **Endpoint de Preview Isolado (`/api/assets/preview`)**
   - Construído com `ApiOperationGuard` no modo `ReadOnly`.
   - Limita rigorosamente as requisições, validando limites de tamanho, caminhos permitidos (bloqueando ".." path traversal) e API Key.

2. **Memória e Conversão Segura**
   - Utiliza `GrfAssemblyFileExtractor` via `AssetPreviewService`.
   - Arquivos extraídos temporariamente são restritos ao diretório `tmp/` e são limpos logo após a conversão em um bloco `finally`.
   - Apenas arquivos conhecidamente suportados como imagens recebem a conversão para DataURL em Base64. Formatos complexos (.spr, .act, .rsw, etc.) recebem o payload de "Unsupported", sendo tratados com placeholders no frontend.

3. **Governança do Frontend**
   - O `PassiveAssetPreviewPanel` foi expandido.
   - O subcomponente `AssetVisualPreview` chama a API utilizando `useApiConfig`.
   - As imagens geradas ganharam estilizacao em `index.css` via `.visual-preview-container` para limitar o tamanho e manter consistência visual (máx 64x64px com contenção controlada).
   - Não foram introduzidos botões de `apply` nem `rollback`. O foco total continuou sobre a governança `read-only`.

4. **Testes**
   - **Backend:** 104 testes executando com sucesso (adicionados 6 testes específicos para `AssetPreviewService`). Foram validadas defesas contra extensões inválidas, path traversal, excesso de bytes, fallbacks para extensões não mapeadas e comportamento correto.
   - **Frontend:** Atualização do teste mockando e encapsulando o contexto de configuração do frontend, atestando comportamento com base de fetch real. O pipeline contínuo tem todos os 22 testes de renderização e fluxos integrados sem warnings/errors.

## Bloqueios / Pendências Resolvidas
- Tivemos que alinhar a inicialização do C# 10 Top-level statements para mover os testes criados ao escopo do custom runner em `RagnaForge.Tests`.
- Resolução de `ApiConfigProvider` nas chamadas aos contexts do React no Frontend para isolamento global de requisições.

A funcionalidade encontra-se completa dentro dos parâmetros Read-Only. Está pronta para o Pull Request para a `main`.
