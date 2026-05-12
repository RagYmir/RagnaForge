# Segurança do Repositório Público

Em maio de 2026, o RagnaForge transitou de uma codebase 100% isolada e local para um repositório remoto público no GitHub.

Esta transição exigiu uma etapa intensa de sanitização de documentação. Este arquivo documenta o que deve permanecer fora do Git.

## O que foi Sanitizado
- Substituição de `C:\Users\...\Desktop` pelo placeholder genérico `<WORKSPACE_ROOT>`.
- Substituição de e-mails pessoais por `<USER_EMAIL>`.
- Mascaramento das origens locais (ex: `<RATHENA_PATH>`, `<GRF_REPOSITORY_PATH>`).
- Remoção do histórico do git do arquivo `repositories.local.json` que carregava caminhos locais.
- Alterações em `.gitignore` para bloquear toda e qualquer chave ou log.

## O que NUNCA deve ser commitado
- Extensões `.grf`, `.gpf`, `.thor`, `.act`, `.spr` originais.
- Pastas temporárias `tmp/`.
- Caches do GRF Index `data/cache/` ou `data/indexes/`.
- Dumps do rAthena `data/backups/`.
- `repositories.local.json`.
- Logs completas com caminhos do HD do usuário.
- Chaves/API Keys de ferramentas de terceiros no repositório aberto.

## Como verificar vazamentos locais antes de um Push
Recomenda-se fazer uso de comandos como:

```sh
git diff --cached --name-only
```
Para ver a lista final do que irá no commit.

Buscas de segurança nos arquivos em *staged*:
```sh
git grep -i --cached "api_key"
git grep -i --cached "C:\\Users"
git grep -i --cached "E:\\Ragnarok"
git grep -i --cached ".grf"
```

Qualquer alteração suspeita que referencie seu setup local privado deve ser mascarada, mockada ou ignorada no `.gitignore`.
