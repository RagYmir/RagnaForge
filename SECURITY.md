# Security Policy

O RagnaForge leva a segurança de arquivos de servidores e manipulação visual a sério. Devido ao design do software, nós manipulamos os bancos de dados originais de rAthena e os diretórios visuais.

## Read-Only Mode (API e UI)
A API e a interface administrativa operam exclusivamente em modo **Read-Only** (Dry-Run / Diff-Preview) nesta fase. Não existem endpoints de escrita (`apply` ou `rollback`) mapeados na API, e a interface administrativa não possui botões ou rotas para tais operações.

## CLI (Command Line Interface)
A CLI possui comandos funcionais de `apply` e `rollback`. No entanto, estes comandos são protegidos por:
- Confirmação explícita obrigatória (`--confirm APPLY` ou `--confirm ROLLBACK`);
- Criação automática de backups antes de qualquer alteração;
- Registro detalhado de logs em `data/logs/`;
- Possibilidade de reverter alterações usando os manifests de log gerados.

## Asset Preview Read-Only
O endpoint `POST /api/assets/preview` é estritamente de leitura. Ele permite visualizar ícones e assets bitmap nos formatos:
- **.bmp**
- **.png**
- **.jpg / .jpeg**
- **.webp**

O processo utiliza extração temporária e controlada em memória (via `tmp/`). Os arquivos extraídos são removidos do disco imediatamente após o processamento, sem qualquer escrita persistente nos repositórios de rAthena, Patch ou GRFs. Formatos complexos como SPR, ACT, TGA, GAT, GND, RSW e RSM permanecem como placeholders informativos e não possuem visualização real nesta fase.

## Alertas sobre API Key e Configurações
- A chave do administrador `X-RagnaForge-Api-Key` deve ficar sempre protegida localmente.
- O arquivo `data/manifests/repositories.local.json` **nunca** deve ser incluído no repositório Git, pois contém caminhos locais absolutos.

## Arquivos Proibidos no Git
É expressamente proibido o commit de:
- Conteúdos extraídos de arquivos GRF;
- Assets visuais (ícones, sprites, mapas);
- Arquivos de credenciais ou dumps de banco de dados;
- Arquivos `.env` ou segredos;
- Logs reais de operações (`data/logs/`).

## Reportando Vulnerabilidades
Se você descobrir alguma falha de segurança ou possibilidade de bypass dos bloqueios da API:
- Utilize o **GitHub Security Advisory** do repositório;
- Ou entre em contato privado com o mantenedor.
- **Importante:** Caso abra uma Issue pública, não inclua exploits funcionais, payloads destrutivos, chaves ou caminhos sensíveis. Levante apenas a falha de bloqueio para correção imediata.
