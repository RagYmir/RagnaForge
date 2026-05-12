# 2026-05-07 MONSTER-NPC-VALIDATION v1

## Monstro

- `monster diff-preview` agora pode propor append em `db/import/mob_skill_db.txt`.
- O spawn dry-run passou a aceitar:
  - `spawn-x`
  - `spawn-y`
  - `spawn-area-x`
  - `spawn-area-y`
  - `spawn-label`

## NPC

- `npc dry-run` agora tenta confirmar sprite padrao em:
  - `jobname.lub`
  - `jobidentity.lub`
  - `npcidentity.lub`
- Quando a confirmacao falha, o relatorio passa a marcar `RequiresAdditionalClientValidation = true`.
- Se houver sprite asset solto ou em GRF, isso aparece como evidencia auxiliar; nao vira apply automatico.

## Shield visual

- Quando um visual com cara de shield ja aparece nas robe tables do client, o sistema passa a orientar `robe` / `Costume_Garment` como pipeline oficial.
