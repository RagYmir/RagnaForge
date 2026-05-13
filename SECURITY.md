# Security Policy

O RagnaForge leva a segurança de arquivos de servidores e manipulação visual a sério. Devido ao design do software, nós manipulamos os bancos de dados originais de rAthena e os diretórios visuais.

## Read-Only Mode
A API e a interface administrativa operam em modo **Read-Only** (Dry-Run / Diff-Preview). A CLI possui comandos **apply/rollback** protegidos por confirmação explícita, logs, backups e rollback, mas esses comandos não estão disponíveis via API/UI nesta fase.

O preview visual de assets utiliza extração controlada em memória. Arquivos temporários são criados apenas para conversão imediata para DataURL e são removidos do disco logo após o processamento, sem persistência fora da sessão de leitura.

## Alertas sobre API Key e Configurações
- A chave do administrador `X-RagnaForge-Api-Key` deve ficar sempre protegida localmente em arquivos `.env` ou settings do seu sistema operacional.
- O seu arquivo com configurações locais (e possíveis Absolute Paths) **nunca** deve ser incluído no repositório. Mantenha `repositories.local.json` isolado.

## Arquivos Proibidos no Git
Por medidas de segurança intelectual e de arquivos volumosos, proíbe-se expressamente que qualquer usuário faça commit de:
- Conteúdos advindos de arquivos originais GRF
- Sprites/Assets (ex: BMP, SPR, ACT) do Client Core
- Arquivos de credenciais ou dumps do MySQL rAthena
- Scripts locais com senhas preenchidas

## Reportando Vulnerabilidades
Se você descobrir alguma falha de segurança ou possibilidade de bypass dos bloqueios da API:
- Utilize o **GitHub Security Advisory**, se disponível;
- Ou entre em contato privado com o mantenedor;
- Caso abra uma Issue pública, **não inclua** exploits funcionais, caminhos sensíveis, chaves, payloads destrutivos ou instruções detalhadas de bypass. Levante apenas a falha de bloqueio para o time corrigir imediatamente.
