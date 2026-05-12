# Contribuindo para o RagnaForge

Obrigado pelo seu interesse em melhorar o RagnaForge! Como nossa prioridade máxima é não corromper servidores ou assets rAthena, toda contribuição deve seguir regras rigorosas.

## Configuração do Ambiente
1. Clone o repositório.
2. Siga as instruções do `README.md` para criar seu arquivo local `data/manifests/repositories.local.json` a partir do `.example`.
3. Garanta que você está usando as versões corretas:
   - .NET 10.0 SDK
   - Node.js + NPM

## Rodando os Testes (Obrigatório)
Todas as Pull Requests exigem passagem em 100% dos testes.
- **Backend:** `dotnet run --project backend/tests/RagnaForge.Tests/RagnaForge.Tests.csproj`
- **Frontend:** `cd frontend && npm run test --watchAll=false`

## Regras de Segurança
Qualquer envio de código que ameace estas regras será sumariamente rejeitado:
- **Nunca** commite arquivos `.grf`, `.gpf`, `.thor`, `.act`, `.spr` da sua instalação do rAthena/Client.
- **Nunca** crie ou ative endpoints de `Apply` ou `Rollback` na API ou na Interface sem instrução explícita de feature documentada. Atualmente a arquitetura dita **Read-Only**.
- **Nunca** envie arquivos do diretório `data/cache/`, `data/indexes/` ou logs.
- **Nunca** exponha caminhos locais absolutos do seu PC ou credenciais no código-fonte ou documentação.

## Fluxo de Submissão
1. Verifique que seu PR está apontando para a branch adequada (ex: `main`).
2. Preencha e responda ao checklist do nosso template de Pull Request.
3. Aguarde o pipeline local e as avaliações de segurança rodarem.
