# Security Policy

O RagnaForge leva a segurança de arquivos de servidores, repositórios rAthena, Patch/client, GRFs e manipulação visual a sério.

O projeto trabalha com leitura, análise e geração de propostas sobre estruturas sensíveis de servidor e client Ragnarok. Por isso, qualquer operação de escrita deve ser tratada como crítica e controlada.

## Read-Only Mode (API e UI)

A API e a interface administrativa operam exclusivamente em modo **Read-Only**, **Dry-Run** e **Diff-Preview** nesta fase.

Não existem endpoints de escrita (`apply` ou `rollback`) mapeados na API, e a interface administrativa não possui botões, rotas ou fluxos para executar essas operações.

A API/UI não deve:
- aplicar alterações em rAthena;
- aplicar alterações no Patch/client;
- alterar GRFs originais;
- copiar assets para Patch;
- executar comandos da CLI;
- editar `.lub` bytecode;
- criar fluxos automáticos de repair/write.

## CLI (Command Line Interface)

A CLI possui comandos funcionais de `apply` e `rollback` para algumas categorias já implementadas no pipeline.

Esses comandos são protegidos por:
- confirmação explícita obrigatória (`--confirm APPLY` ou `--confirm ROLLBACK`);
- geração de diff/dry-run antes da escrita;
- criação automática de backups antes de qualquer alteração;
- logs detalhados em `data/logs/`;
- manifests de rollback;
- validação de hash para evitar rollback cego sobre arquivos modificados manualmente;
- restrição de escrita às raízes permitidas.

Esses comandos **não estão disponíveis via API/UI nesta fase**.

## Asset Preview Read-Only

O endpoint `POST /api/assets/preview` é estritamente de leitura.

Ele permite visualizar ativos nos formatos:
- **Bitmaps:** `.bmp`, `.png`, `.jpg`, `.jpeg`, `.webp` (Visual completo)
- **Complexos:** `.spr` (Preview visual best-effort com fallback para metadados); `.act` (Metadata-only no v1).
- **Placeholders:** `.tga`, `.gat`, `.gnd`, `.rsw`, `.rsm` permanecem como placeholders informativos até a implementação de parsers/conversores seguros.

O processo utiliza extração temporária e controlada via `tmp/`, apenas para conversão imediata para DataURL/base64.

A segurança é garantida por:
- `PathValidationHelper`: bloqueio de traversal, caminhos rootados e normalização de caminhos lógicos.
- Validação de fronteira via `Path.GetRelativePath`: impede escape das raízes de Patch e GRF Repository.
- Limite físico de 10MB por ativo.
- Limpeza imediata de temporários.

Os arquivos temporários devem ser removidos imediatamente após o processamento, sem escrita persistente nos repositórios de rAthena, Patch/client, GRFs ou diretórios de cache/log/backups.

## API Key e Configurações Locais

A chave administrativa `X-RagnaForge-Api-Key` deve ficar sempre protegida localmente.

Não faça commit de:
- `.env`;
- arquivos de secrets;
- `repositories.local.json`;
- arquivos com caminhos absolutos reais do ambiente;
- configurações locais com credenciais;
- dumps de banco;
- logs reais sensíveis.

Use templates limpos, como `repositories.example.json`, para documentar a estrutura esperada sem expor dados locais.

## Arquivos Proibidos no Git

É proibido commitar:
- GRFs originais;
- Thor/GPF privados;
- assets extraídos de GRF;
- sprites, ACTs, BMPs, TGAs, texturas, mapas ou arquivos do client core;
- dumps MySQL/rAthena;
- backups reais;
- logs reais com caminhos locais;
- arquivos temporários de extração;
- arquivos contendo API keys, senhas, tokens ou caminhos sensíveis.

Pastas como `data/cache/`, `data/indexes/`, `data/logs/`, `data/backups/` e `tmp/` devem manter apenas `.gitkeep` ou templates seguros quando necessário.

## Reportando Vulnerabilidades

Se você descobrir uma falha de segurança, bypass de read-only, vazamento de arquivo, path traversal, escrita indevida ou possibilidade de exploração:

- utilize o **GitHub Security Advisory**, se disponível;
- ou entre em contato privado com o mantenedor;
- se abrir uma Issue pública, não inclua exploits funcionais, payloads destrutivos, caminhos sensíveis, chaves, tokens, dumps, arquivos privados ou instruções detalhadas de bypass.

Issues públicas devem descrever apenas o impacto de forma sanitizada, sem material reproduzível perigoso.

## Escopo de Segurança Atual

Nesta fase, a política oficial é:

- API/UI: read-only, dry-run e diff-preview;
- CLI: apply/rollback apenas com confirmação explícita e trilha de segurança;
- GRFs originais: nunca alterados diretamente;
- `.lub` bytecode: bloqueado para edição/decompilação/recompilação;
- assets complexos: apenas placeholder (GAT/RSW/RSM) ou metadados seguros (SPR/ACT);
- apply/rollback via API/UI: fora de escopo até nova decisão formal de segurança.
