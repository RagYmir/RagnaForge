# RagnaForge Agent Health — Smoke Test Local

Este documento descreve como configurar, testar e interpretar os testes de fumaça (smoke tests) para a integração entre o RagnaForge (projeto principal) e o RagnaForge Agent (agente local auxiliar).

## 1. Pré-requisitos
- .NET 10 instalado e configurado na máquina de desenvolvimento.
- RagnaForge Agent compilado no caminho correto:
  - Diretório do Agente: `C:\Users\Allis\Desktop\Agente Ragnarok`
  - Binário: `<AGENT_ROOT>\dist\ragnaforge\ragnaforge.exe` (ou executado localmente via `dotnet run` para fins de desenvolvimento).
- Chave de API configurada para comunicação segura.

## 2. Como Configurar `AgentExePath`
O caminho do executável do agente deve ser configurado no arquivo `appsettings.json` ou `appsettings.Development.json` do backend (`RagnaForge.Api`).

Exemplo de configuração segura:
```json
{
  "RagnaForge": {
    "Agent": {
      "AgentExePath": "C:\\Users\\Allis\\Desktop\\Agente Ragnarok\\dist\\ragnaforge\\ragnaforge.exe",
      "AgentCacheDir": "C:\\Users\\Allis\\Desktop\\Agente Ragnarok\\cache\\agent",
      "AgentTimeoutSeconds": 30
    }
  }
}
```
*Nota: Para homologação do commit, caminhos locais absolutos não devem ser incluídos em arquivos de produção. Use variáveis de ambiente ou placeholders como `<AGENT_ROOT>\dist\ragnaforge\ragnaforge.exe`.*

## 3. Como Subir a API do Backend
No diretório `C:\Users\Allis\Desktop\New project`:
1. Abra um terminal PowerShell.
2. Execute o seguinte comando para iniciar a API em ambiente de desenvolvimento:
   ```powershell
   dotnet run --project backend\src\RagnaForge.Api\RagnaForge.Api.csproj
   ```
3. A API estará escutando no endereço padrão (ex: `http://localhost:5099` ou conforme definido na sua configuração de porta).

## 4. Como Chamar `GET /api/agent/health`
Com a API ativa, envie uma requisição HTTP do tipo `GET` com o header da API Key exigido.

Usando curl no PowerShell:
```powershell
Invoke-RestMethod -Uri "http://localhost:5099/api/agent/health" -Method Get -Headers @{ "X-RagnaForge-Api-Key" = "local-key" }
```

## 5. Como Abrir a Página Agent Health na UI
1. Suba o servidor de desenvolvimento do frontend na pasta `frontend`:
   ```powershell
   npm run dev
   ```
2. Abra seu navegador em `http://localhost:5173`.
3. Certifique-se de que a conexão com a API e a chave estão configuradas.
4. Clique no link **"Agent Health"** na barra lateral de navegação (ou acesse a rota `/agente` diretamente).

## 6. Resposta Esperada (Exemplo de Sucesso)
Uma requisição bem-sucedida ao endpoint `GET /api/agent/health` retorna um JSON formatado com o seguinte formato:

```json
{
  "success": true,
  "data": {
    "agentReachable": true,
    "statusOk": true,
    "doctorOk": true,
    "activeProfile": "teste",
    "agentVersion": "1.1.0-mcp-preview",
    "configFingerprint": "11896c4101e0...",
    "dbMode": "renewal",
    "grfProtected": true,
    "lubEditingBlocked": true,
    "cacheExists": true,
    "cacheMatchesFingerprint": true,
    "safety": {
      "requireDryRunBeforeApply": true,
      "requireDiffBeforeApply": true,
      "requireExplicitConfirmation": true,
      "backupBeforeApply": true,
      "blockOriginalGrfWrite": true,
      "blockLubEditing": true,
      "invalidateCacheOnPathChange": true,
      "cacheMustMatchActiveProfile": true
    },
    "doctor": {
      "totalChecks": 31,
      "passed": 31,
      "warnings": 0,
      "errors": 0,
      "failedChecks": []
    },
    "index": {
      "itemsFound": 82848,
      "monstersFound": 3681,
      "npcsFound": 13860,
      "mapsFound": 1100,
      "filesScanned": 440970,
      "filesParsed": 814,
      "filesSkipped": 440156,
      "durationMs": 2580,
      "generatedAtUtc": "2026-05-18T16:00:00Z"
    },
    "validation": {
      "totalIssues": 1084,
      "errorCount": 1,
      "warningCount": 1083,
      "topCategories": [
        { "code": "MAP_NO_CLIENT_FILES", "count": 960 },
        { "code": "NPC_NO_MAP", "count": 123 }
      ]
    },
    "scan": {
      "filesVisited": 281,
      "filesIndexed": 281,
      "filesSkipped": 0,
      "directoriesVisited": 57,
      "durationMs": 2267
    },
    "warnings": [],
    "errors": []
  },
  "warnings": [],
  "errors": [],
  "generatedAt": "2026-05-18T19:20:00Z",
  "correlationId": "0275a4edf681",
  "operationKind": "ReadOnly",
  "readOnlyMode": true,
  "durationMs": 150
}
```

## 7. Como Interpretar as Flags de Health
- **`agentReachable`**: Indica se o processo do RagnaForge Agent CLI pôde ser invocado com sucesso na máquina local.
- **`statusOk`**: Confirma se o comando `status` respondeu com `ok: true`.
- **`doctorOk`**: Confirma se todas as 31 validações de integridade do ambiente e diretórios locais passaram com sucesso.
- **`safeForReadOnlyWork`**: Sinaliza se o ambiente está estável o suficiente para que o Codex ou outra IA faça análises do rAthena ou Patch. Sempre `true` se `doctorOk` for verdadeiro.
- **`safeForDryRun`**: Sinaliza que o agente pode simular modificações e calcular Diffs de visualização sem alterar arquivos.
- **`safeForApply`**: **Sempre false**. Indica que o ambiente não permite a gravação ou execução de escritas diretas.
- **`applyBlocked`** & **`rollbackRealBlocked`**: Atestam de forma redundante e visível no JSON de saúde que operações destrutivas e alterações reais no GRF principal e rAthena estão banidas.
- **Cache (Trusted / Stale)**:
  - Se `cacheMatchesFingerprint` for `true`, o cache de entidades está em sincronia estrita com a assinatura atual das configurações locais.
  - Se for `false`, o cache é considerado `stale` (desatualizado) e deve ser recriado executando `ragnaforge index --entities`.

## 8. Diagnóstico de Problemas Comuns

### Cenário A: Agent Ausente (`agentReachable = false`)
- **Sintoma**: Painel UI mostra "Agent Offline" e erros de processo no log do backend.
- **Causa**: O executável não está no caminho apontado por `AgentExePath`.
- **Solução**: Verifique o arquivo `appsettings.json` e confirme se o caminho aponta exatamente para o executável `ragnaforge.exe` compilado.

### Cenário B: Timeout de Execução do Agent
- **Sintoma**: Requisição demora até expirar e retorna status 504 ou JSON de erro indicando timeout.
- **Causa**: A execução do comando durou mais que o configurado em `AgentTimeoutSeconds` (ex: varredura pesada de diretórios na primeira execução).
- **Solução**: Aumente o `AgentTimeoutSeconds` para 60 segundos ou execute `ragnaforge index --entities` manualmente para aquecer o cache do banco de dados antes da API consumir.

### Cenário C: Erro Interno do Agent (`statusOk = false` ou `doctorOk = false`)
- **Sintoma**: O executável é chamado, mas retorna `ok: false`.
- **Causa**: Erro de segurança (ex: PathGuard detectou contenção hierárquica violada, ou falta de permissão em logs/caches).
- **Solução**: Execute `ragnaforge doctor` na CLI do agente local para visualizar a lista detalhada de checks com falha e corrigi-las (ex: caminhos em `paths.json` apontando para pastas inexistentes).

## 9. Garantia de Read-Only Absoluta
Toda a integração é blindada a nível de arquitetura:
1. **Allowlist Rígida de Comandos CLI**: Apenas comandos puramente de leitura (`status`, `doctor`, `scan`, `index`, `validate`) são aceitos pelo executor backend.
2. **Proibição de Shell**: O processo é invocado por meio de `ProcessStartInfo` sem passar por interpretadores de shell (`UseShellExecute = false`), anulando qualquer possibilidade de injeção de comandos arbitrários.
3. **Ausência de Operações de Escrita na API/UI**: A aplicação RagnaForge principal não contém endpoints, botões ou componentes que iniciem ações de alteração de dados no rAthena, Patch ou GRF.
