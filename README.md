# RagnaForge

RagnaForge é uma ferramenta administrativa avançada para gerenciar configurações e assets de servidores baseados em rAthena. Criada com foco rigoroso em integridade, ela permite um fluxo de planejamento (`dry-run`) e análise de diferenças (`diff-preview`) de modo unificado e seguro.

## Estado Atual
O RagnaForge está atualmente em fase **Read-Only / Dry-Run**. Isto significa que todas as funcionalidades que alteram arquivos do servidor (`apply`/`rollback`) estão **propositadamente bloqueadas** na API e na interface gráfica (Admin UI). O projeto existe no momento como uma infraestrutura de visualização passiva e auditoria. 

## Arquitetura e Segurança
A API e o Frontend operam sob o modo `safe/read-only`.
*   **Apenas Leitura**: A interface expõe dashboards, validação de itens, NPCs, monstros, mapas e visualizações de assets.
*   **Sem Alteração Acidental**: Não existem botões ou fluxos habilitados para Apply ou Rollback na UI.
*   **API Segura**: Os endpoints destrutivos não existem na implementação da rede local, as regras são impostas via `ApiSafetyPolicy`.

## Como Configurar e Rodar

### 1. Criar o arquivo de repositórios locais
Para proteger os caminhos do seu disco local de entrarem no GitHub, o projeto requer que você defina o seu manifesto local na pasta `data/manifests/`.

1. Copie o template:
   ```sh
   cp data/manifests/repositories.example.json data/manifests/repositories.local.json
   ```
2. Edite `data/manifests/repositories.local.json` colocando os seus caminhos reais absolutos para as pastas do rAthena, Patch/Client, GRFs e GRF Editor.
**(ATENÇÃO: Nunca adicione `repositories.local.json` nos seus commits! Ele já está no `.gitignore`)**

### 2. Rodar o Backend
O backend foi construído em .NET 10.0 e possui 98 testes garantindo a segurança de todos os scanners e bloqueios.
```sh
# Compilar a solucao
dotnet build RagnaForge.slnx

# (Recomendado) Executar os testes de garantia antes de subir
dotnet run --project backend/tests/RagnaForge.Tests/RagnaForge.Tests.csproj

# Rodar a API na porta http://127.0.0.1:5099
dotnet run --project backend/src/RagnaForge.Api/RagnaForge.Api.csproj
```

### 3. Rodar o Frontend (Admin UI)
A interface foi escrita em React + Vite.
```sh
cd frontend
npm install

# (Recomendado) Executar os testes do frontend
npm run test

# Rodar em modo dev na porta http://localhost:5173
npm run dev
```

## Avisos de Segurança
*   Nunca commite segredos, senhas ou a sua `X-RagnaForge-Api-Key`.
*   Nunca adicione caminhos locais diretos que contenham seu nome de usuário.
*   Os diretórios de `tmp`, caches e GRFs são estritamente para uso local e já se encontram no `.gitignore`.
*   As modificações de código sempre devem passar por uma rotina completa de testes unitários.

## Status de Testes
*   **Backend:** 98/98 Testes Unitários/Integração PASS (100% cobrindo a política read-only e diffs).
*   **Frontend:** 22/22 Testes de Componentes PASS (100% cobrindo matrizes de UI e UX Safety).