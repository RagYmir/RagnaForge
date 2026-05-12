# Future Apply Rollback Policy

Data: 2026-05-12

## Objetivo

Definir os requisitos minimos para um dia liberar `apply` e `rollback` por API ou interface, sem implementar nada nesta rodada.

Este documento e deliberadamente conservador.
Ele nao autoriza escrita.
Ele nao muda a API atual.
Ele nao muda a UI atual.

## Estado atual

- API: somente `read-only`, `dry-run`, `diff-preview` e `cache write` interno controlado.
- UI: somente analise, validacao, historico, comparacao, exportacao local e preview passivo.
- `apply` e `rollback` nao existem por HTTP.
- a interface nao tem botoes operacionais de escrita.

## Principios obrigatorios

1. Nenhum apply por impulso.
2. Nenhum rollback cego.
3. Nenhuma escrita em mapa como primeira categoria liberada.
4. Nenhum bypass para `.lub` bytecode.
5. Nenhum apply com asset critico ausente.
6. Nenhum apply sem diff revisado.
7. Nenhum apply sem staging verificavel.
8. Nenhum rollback sem validacao de hash.

## Requisitos minimos antes de liberar escrita

### 1. Autenticacao forte

- API key local nao basta como controle final de escrita.
- A escrita futura deve exigir autenticacao mais forte.
- O minimo aceitavel:
  - identidade de operador;
  - segredo nao compartilhado;
  - expiracao/rotacao previsivel.

### 2. Autorizacao por papel

Perfis minimos sugeridos:

- `Viewer`: somente leitura
- `Planner`: leitura, dry-run e diff-preview
- `Operator`: pode solicitar apply/rollback
- `Approver`: pode confirmar execucao apos revisao

Apply e rollback nao devem ficar disponiveis para `Viewer` nem `Planner`.

### 3. Confirmacao humana forte

O fluxo futuro deve ter:

- confirmacao explicita;
- resumo do risco;
- diff revisado;
- exibicao dos arquivos afetados;
- etapa de confirmacao final separada da etapa de planejamento.

### 4. Revisao obrigatoria de diff

Antes de qualquer escrita:

- diff server-side deve ser exibido;
- diff client-side deve ser exibido;
- dependencias de assets devem ser exibidas;
- bloqueios devem ser resolvidos ou explicitamente reconhecidos.

### 5. Checklist de risco

Cada operacao deve checar pelo menos:

- `ClientDate` confirmado;
- assets obrigatorios resolvidos;
- ausencia de bytecode bloqueante;
- ausencia de ambiguidades criticas;
- hashes base atuais ainda validos;
- staging e validacao final limpos;
- rollback disponivel e testado para a categoria.

### 6. Politica de asset obrigatorio

Antes de liberar escrita automatica, o projeto precisa definir por categoria:

- quando asset ausente e apenas warning;
- quando asset ausente bloqueia;
- quando ambiguidade bloqueia;
- quando `needs-copy-future` impede apply.

Itens e equipamentos nao devem virar `Unknown Item/Apple` por omissao silenciosa.

### 7. Persistencia/confirmacao de ClientDate

Antes de qualquer escrita por API/UI, o `ClientDate` precisa ter politica formal:

- deteccao automatica;
- persistencia opcional ou obrigatoria;
- confirmacao humana quando houver divergencia;
- log/auditoria da decisao.

### 8. Logs e auditoria

Toda escrita futura deve gerar:

- operador responsavel;
- categoria;
- payload base;
- diff aprovado;
- horario;
- arquivos afetados;
- hashes antes/depois;
- resultado;
- rollback associado.

### 9. Rollback testado por categoria

Nao basta existir comando de rollback na CLI.
Antes de expor rollback por API/UI, a categoria precisa comprovar:

- rollback funcional;
- validacao de hash;
- bloqueio de drift manual;
- documentacao da recuperacao.

### 10. Modo staging obrigatorio

Toda escrita futura deve passar por staging:

- gerar saida final em staging;
- validar sintaxe/estrutura;
- validar assets/identidades quando aplicavel;
- so depois permitir substituicao final.

### 11. Bloqueios absolutos

Mesmo no futuro, estas condicoes devem bloquear:

- mapa ambiguo;
- rename binario de mapa nao resolvido;
- `.lub` bytecode bloqueado;
- asset critico ausente;
- `ViewID` duplicado;
- `AegisName/ID` em conflito;
- identidade client-side insegura;
- conflito de hash antes do write ou antes do rollback.

### 12. Feature flags desligadas por padrao

Qualquer capacidade futura de escrita deve nascer com:

- `EnableApplyEndpoints = false`
- `EnableRollbackEndpoints = false`

E cada categoria deve ter flag propria.

### 13. Liberacao por categoria

Se um dia a escrita for liberada, a ordem recomendada e:

1. item
2. equipamento
3. NPC
4. monstro
5. mapa por ultimo

`map apply` nunca deve ser a primeira categoria liberada.

## Implicacoes para API futura

Se houver endpoints de escrita no futuro, eles devem:

- ficar atras de auth forte e autorizacao por papel;
- exigir diff aprovado;
- exigir confirmacao forte;
- bloquear em `ReadOnlyMode`;
- usar `ProblemDetails` completo;
- registrar `correlationId`, operador e manifest aplicado;
- respeitar rate/concurrency mais restritivos que os endpoints read-only.

## Implicacoes para UI futura

Se houver escrita na interface um dia:

- nada de botao simples de apply;
- nada de escrita sem revisar diff;
- nada de esconder warnings;
- nada de esconder assets faltantes;
- nada de permitir bytecode bloqueado;
- nada de prometer rollback quando o estado atual ja divergiu.

## O que continua proibido agora

- implementar `apply` na API;
- implementar `rollback` na API;
- criar botoes de escrita;
- criar atalhos escondidos;
- copiar assets para o Patch;
- editar `.lub` bytecode.

## Conclusao

A politica futura de `apply/rollback` continua em fase de definicao.
O projeto ja tem base forte de leitura, planejamento, diff, validacao e auditoria.
A liberacao de escrita por API/UI deve ser tratada como uma fase separada, com criterios formais de seguranca e rollout por categoria.
