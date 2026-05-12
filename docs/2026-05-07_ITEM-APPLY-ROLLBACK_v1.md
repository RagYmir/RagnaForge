# 2026-05-07 - Item apply rollback v1

## Resumo

Foi preparada a primeira base transacional de `item apply` e `item rollback`, com backup e log no workspace e confirmação explícita obrigatória.

## Escopo

- apply real apenas para pipeline de item legado;
- sem autoexecução;
- sem tocar em GRFs originais;
- logs e backups somente em `data/logs/items` e `data/backups/items`.

## Regras de segurança

- `item apply` exige `--confirm APPLY`;
- `item rollback` exige `--confirm ROLLBACK`;
- o serviço recusa alvos fora de `rAthenaPath` e `PatchPath`;
- o serviço recusa rollback com log fora do workspace;
- cada arquivo alterado recebe backup antes da escrita;
- a escrita usa arquivo temporário e substituição atômica local quando possível.

## Arquivos envolvidos

- `backend/src/RagnaForge.Infrastructure/Items/LegacyItemApplyService.cs`
- `backend/src/RagnaForge.Domain/Items/ItemApplyOperation.cs`
- `backend/src/RagnaForge.Cli/Program.cs`

## Validacao

- `dotnet build RagnaForge.slnx`
- `dotnet run --project backend\tests\RagnaForge.Tests\RagnaForge.Tests.csproj`
- smoke de segurança:
  - `dotnet run --project backend\src\RagnaForge.Cli -- item apply --config data\manifests\repositories.local.json --aegis RF_CONFIRM_TEST --name "RagnaForge Confirm Test" --resource RF_CONFIRM_TEST`
  - resultado: recusado por falta de `--confirm APPLY`

## Resultado atual

- apply/rollback já existe em código;
- o fluxo real continua bloqueado sem confirmação explícita;
- a validação completa do round-trip ficou coberta por teste automatizado em workspace temporário.
