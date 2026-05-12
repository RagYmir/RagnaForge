## Descrição da Pull Request
[Descreva o que este PR adiciona ou corrige]

## Checklist de Segurança e Qualidade
Por favor, marque com `[x]` os itens que você garantiu nesta PR:

- [ ] O build do Backend passou (`dotnet build`).
- [ ] Todos os testes do Backend passaram (`dotnet run --project backend/tests/...`).
- [ ] O build do Frontend passou (`npm run build`).
- [ ] Todos os testes do Frontend passaram (`npm run test`).
- [ ] **Não adiciona** fluxos de `/apply` funcionais (Read-Only garantido).
- [ ] **Não adiciona** fluxos de `/rollback` funcionais.
- [ ] **Não commita** arquivos `.env`, segredos, ou `X-RagnaForge-Api-Key`.
- [ ] **Não commita** nenhum arquivo de jogo ou repositório privado (`.grf`, `.act`, `.spr`).
- [ ] **Não commita** o seu arquivo pessoal `repositories.local.json`.

## Verificações Finais
Asseguro que as mudanças respeitam a arquitetura Read-Only do RagnaForge.
