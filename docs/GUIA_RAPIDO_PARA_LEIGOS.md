# Guia rapido para leigos

Este guia explica o Ragna_Forge sem pressupor conhecimento do codigo.

## O que e o Ragna_Forge

Ragna_Forge e um painel e uma API local para analisar mudancas de servidor Ragnarok antes de mexer em arquivos reais. Ele ajuda a encontrar riscos, dependencias, diffs e problemas de dados.

## O que e o Agente Setimmo

O Agente Setimmo fica em `Agente_Setimmo/`. Ele e uma ferramenta local que responde perguntas como:

- o ambiente esta configurado?
- o projeto esta legivel?
- quantos itens, monstros, NPCs e mapas existem?
- ha problemas de dados externos?
- a operacao seria segura para read-only ou dry-run?

O agente nao deve aplicar mudancas reais nesta fase.

## Fluxo seguro

1. Rodar o agente.
2. Rodar a API.
3. Abrir a UI.
4. Fazer dry-run.
5. Ver diff-preview.
6. Gerar ou revisar report.
7. Decidir manualmente o proximo passo.

## Termos importantes

- Read-only: so leitura, sem escrever em arquivos reais.
- Dry-run: simula o que faria, mas nao aplica.
- Diff-preview: mostra uma previa textual do que mudaria.
- safeForReadOnlyWork: seguro para auditoria e leitura.
- safeForDryRun: seguro para simulacao.
- safeForApply: hoje deve continuar `false` quando houver blocker.

## Comandos minimos

```powershell
Copy-Item .\data\manifests\repositories.example.json .\data\manifests\repositories.local.json
Copy-Item .\Agente_Setimmo\config\paths.example.json .\Agente_Setimmo\config\paths.json
.\scripts\publish-all.ps1
dotnet run --project .\backend\src\RagnaForge.Api\RagnaForge.Api.csproj
```

Em outro terminal:

```powershell
cd .\frontend
npm.cmd ci
npm.cmd run dev
```

## O que nao fazer

- Nao commitar `repositories.local.json`.
- Nao commitar `.env`.
- Nao commitar GRFs ou sprites.
- Nao tentar habilitar Apply na UI/API.
- Nao editar `.lub`.
