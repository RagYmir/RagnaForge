# Security Policy

O RagnaForge leva a segurança de arquivos de servidores e manipulação visual a sério. Devido ao design do software, nós manipulamos os bancos de dados originais de rAthena e os diretórios visuais.

## Read-Only Mode
No atual estado do projeto, o RagnaForge opera exclusivamente em modo **Read-Only** (Dry-Run / Diff-Preview). Não forneça chaves de banco de dados e não subverta as configurações de segurança inseridas pelo `ApiSafetyPolicy`.

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
Se você descobrir alguma rota que possa ser abusada no modo Read-Only, contorne os bloqueios da API, ou introduza riscos de sobrescrita destrutiva dos bancos rAthena:
- Por favor, abra um relatório de Issue focado na flag [Security].
- Não divulgue exploits funcionalmente ativos de `Apply`/`Rollback` nos fóruns abertos. Levante a falha de bloqueio para o time corrigir imediatamente.
