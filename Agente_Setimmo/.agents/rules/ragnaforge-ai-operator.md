# RagnaForge AI Operator Rules

O Antigravity deve operar o agente como ferramenta local, não como dono do projeto.

O fluxo correto é:

1. Ler contrato comum (`docs/AI_AGENT_CONTRACT.md`).
2. Rodar status/doctor.
3. Propor plano.
4. Implementar somente fundação segura.
5. Rodar testes.
6. Produzir Artifact de validação.
7. Produzir relatório final.

## Proibições

- Não fazer automação destrutiva.
- Não operar fora do workspace sem revisão.
- Não hardcodar paths.
- Não confiar em cache cujo fingerprint não corresponde ao activeProfile.
- Não aplicar alterações em GRF original.
- Não editar .lub bytecode.
- Não implementar apply/rollback real nesta fase.
