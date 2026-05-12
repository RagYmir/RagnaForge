import { useMutation } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { ApiClientError } from "../api/client";
import { ApplyReadinessPanel } from "../components/ApplyReadinessPanel";
import { DiffViewer } from "../components/DiffViewer";
import { DiffWorkbench } from "../components/DiffWorkbench";
import { EntityGrid } from "../components/EntityGrid";
import { FieldGroup } from "../components/FieldGroup";
import { HistoryPanel } from "../components/HistoryPanel";
import { JsonInspector } from "../components/JsonInspector";
import { MonsterDropsGrid } from "../components/MonsterDropsGrid";
import { MonsterSkillsGrid } from "../components/MonsterSkillsGrid";
import { MonsterSpawnsGrid } from "../components/MonsterSpawnsGrid";
import { PageHeader } from "../components/PageHeader";
import { PipelineWorkspaceLayout } from "../components/PipelineWorkspaceLayout";
import { PresetPanel } from "../components/PresetPanel";
import { ProblemDetailsView } from "../components/ProblemDetailsView";
import { ReadinessRibbon } from "../components/ReadinessRibbon";
import { ResponseMeta } from "../components/ResponseMeta";
import { ValidationMatrix } from "../components/ValidationMatrix";
import { useApiConfig } from "../features/connection/ApiConfigContext";
import {
  exportHistoryEntryJson,
  exportHistoryEntryMarkdown,
} from "../features/shared/exporters";
import {
  clearPipelineHistory,
  listPipelineHistory,
  savePipelineHistoryEntry,
} from "../features/shared/localHistory";
import {
  defaultMonsterFormValues,
  monsterPresets,
  type MonsterFormValues,
} from "../features/shared/pipelinePresets";
import {
  buildMonsterDrops,
  buildMonsterSkills,
  buildMonsterSpawns,
} from "../features/shared/requestBuilders";
import { asStringArray } from "../features/shared/viewData";

export function MonstersPage() {
  const { client } = useApiConfig();
  const [form, setForm] = useState<MonsterFormValues>(defaultMonsterFormValues);
  const [historyRevision, setHistoryRevision] = useState(0);

  const buildPayload = () => ({
    configPath: form.configPath,
    aegisName: form.aegisName,
    displayName: form.displayName,
    spriteOverride: form.spriteName,
    level: Number(form.level) || 1,
    hp: Number(form.hp) || 1,
    mapName: form.mapName,
    amount: Number(form.amount) || 1,
    respawnMilliseconds: Number(form.respawn) || 5000,
    drops: buildMonsterDrops(form.dropsText),
    skills: buildMonsterSkills(form.skillsText),
    spawns: buildMonsterSpawns(form.spawnsText),
  });

  function refreshHistory() {
    setHistoryRevision((current) => current + 1);
  }

  const dryRunMutation = useMutation({
    mutationFn: () => client.monsterDryRun(buildPayload()),
    onSuccess: (response) => {
      savePipelineHistoryEntry({
        category: "monsters",
        kind: "dry-run",
        payload: { ...form },
        responseData: response.data,
        success: response.success,
        warningsCount: response.warnings.length + (response.data.validationWarnings?.length ?? 0),
        errorsCount: response.errors.length + (response.data.validationErrors?.length ?? 0),
        summary: form.aegisName || form.displayName || "Monster dry-run",
        correlationId: response.correlationId,
        readiness: String(response.data.applyReadiness ?? ""),
        canApply: response.data.canApply,
        diffFileCount: response.data.diffPreview?.fileCount,
      });
      refreshHistory();
    },
  });

  const diffMutation = useMutation({
    mutationFn: () => client.monsterDiffPreview(buildPayload()),
    onSuccess: (response) => {
      if (!response.data) {
        return;
      }
      savePipelineHistoryEntry({
        category: "monsters",
        kind: "diff-preview",
        payload: { ...form },
        responseData: response.data,
        success: response.success,
        warningsCount: response.warnings.length,
        errorsCount: response.errors.length,
        summary: `${form.aegisName || form.displayName || "Monster"} diff-preview`,
        correlationId: response.correlationId,
        readiness: `${response.data.fileCount ?? 0} arquivos`,
        diffFileCount: response.data.fileCount,
      });
      refreshHistory();
    },
  });

  const historyEntries = useMemo(() => listPipelineHistory("monsters"), [historyRevision]);
  const report = dryRunMutation.data?.data;
  const diff = diffMutation.data?.data ?? report?.diffPreview;
  const error = (dryRunMutation.error ?? diffMutation.error) as ApiClientError | null;

  const combinedWarnings = useMemo(
    () => [
      ...(dryRunMutation.data?.warnings ?? []),
      ...(report?.warnings ?? []),
      ...(report?.validationWarnings ?? []),
      ...(diffMutation.data?.warnings ?? []),
    ],
    [dryRunMutation.data, diffMutation.data, report],
  );

  const combinedErrors = useMemo(
    () => [
      ...(dryRunMutation.data?.errors ?? []),
      ...(report?.errors ?? []),
      ...(report?.validationErrors ?? []),
      ...(diffMutation.data?.errors ?? []),
    ],
    [dryRunMutation.data, diffMutation.data, report],
  );

  const navigatorItems = [
    {
      key: "monster-base",
      label: "Dados base",
      subtitle: form.aegisName || "AegisName e nome do monstro",
      status:
        form.aegisName && form.displayName ? ("good" as const) : ("warning" as const),
      href: "#monster-base",
    },
    {
      key: "monster-stats",
      label: "Stats",
      subtitle: `Level ${form.level} | HP ${form.hp}`,
      status: "neutral" as const,
      href: "#monster-stats",
    },
    {
      key: "monster-drops",
      label: "Drops / MVP",
      subtitle: form.dropsText ? `${buildMonsterDrops(form.dropsText).length} registros` : "Sem drops",
      status: form.dropsText ? ("good" as const) : ("neutral" as const),
      href: "#monster-drops",
    },
    {
      key: "monster-skills",
      label: "Skills",
      subtitle: form.skillsText ? `${buildMonsterSkills(form.skillsText).length} registros` : "Sem skills",
      status: form.skillsText ? ("good" as const) : ("neutral" as const),
      href: "#monster-skills",
    },
    {
      key: "monster-spawns",
      label: "Spawns",
      subtitle: form.spawnsText ? `${buildMonsterSpawns(form.spawnsText).length} registros` : "Sem spawns extras",
      status: form.mapName ? ("good" as const) : ("warning" as const),
      href: "#monster-spawns",
    },
    {
      key: "monster-validation",
      label: "Validacoes",
      subtitle: report?.applyReadiness ? String(report.applyReadiness) : "Aguardando analise",
      status:
        combinedErrors.length > 0
          ? ("danger" as const)
          : combinedWarnings.length > 0
            ? ("warning" as const)
            : ("neutral" as const),
      href: "#monster-validation",
    },
  ];

  return (
    <section className="page">
      <PageHeader
        title="Monstros"
        description="Workspace de monstros com presets seguros, grids de drops/skills/spawns, historico local e diff seguro."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <div className="stack-lg">
            <EntityGrid
              title="Workspace do monstro"
              description="Navegacao por dados base, grids de drops/skills/spawns e diff."
              items={navigatorItems}
              activeKey="monster-base"
            />
            <section className="panel">
              <div className="panel-header">
                <h3>Acoes seguras</h3>
              </div>
              <div className="form-actions">
                <button onClick={() => dryRunMutation.mutate()} type="button" disabled={!form.aegisName || !form.displayName || !form.spriteName}>
                  Gerar dry-run
                </button>
                <button onClick={() => diffMutation.mutate()} type="button" disabled={!form.aegisName || !form.displayName || !form.spriteName}>
                  Gerar diff-preview
                </button>
                <button type="button" className="button-secondary" onClick={() => setForm(defaultMonsterFormValues)}>
                  Limpar formulario
                </button>
              </div>
            </section>
            <PresetPanel title="Presets seguros" presets={monsterPresets} onApply={setForm} />
            <HistoryPanel
              title="Historico local"
              entries={historyEntries}
              onApplyPayload={(entry) => setForm(((entry.payload as unknown) as MonsterFormValues) ?? defaultMonsterFormValues)}
              onClear={() => {
                clearPipelineHistory("monsters");
                refreshHistory();
              }}
              onExportJson={exportHistoryEntryJson}
              onExportMarkdown={exportHistoryEntryMarkdown}
            />
          </div>
        }
        primary={
          <div className="stack-lg">
            <FieldGroup id="monster-base" title="Dados base" description="Identificacao, sprite e contexto geral do monstro.">
              <div className="form-grid">
                <label>
                  <span>Config path</span>
                  <input value={form.configPath} onChange={(event) => setForm((current) => ({ ...current, configPath: event.target.value }))} />
                </label>
                <label>
                  <span>AegisName</span>
                  <input value={form.aegisName} onChange={(event) => setForm((current) => ({ ...current, aegisName: event.target.value }))} />
                </label>
                <label>
                  <span>Name</span>
                  <input value={form.displayName} onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))} />
                </label>
                <label>
                  <span>Sprite</span>
                  <input value={form.spriteName} onChange={(event) => setForm((current) => ({ ...current, spriteName: event.target.value }))} />
                </label>
              </div>
            </FieldGroup>
            <FieldGroup id="monster-stats" title="Stats" description="Mapa base, quantidade, respawn e atributos centrais.">
              <div className="form-grid">
                <label>
                  <span>Level</span>
                  <input value={form.level} onChange={(event) => setForm((current) => ({ ...current, level: event.target.value }))} />
                </label>
                <label>
                  <span>HP</span>
                  <input value={form.hp} onChange={(event) => setForm((current) => ({ ...current, hp: event.target.value }))} />
                </label>
                <label>
                  <span>Map</span>
                  <input value={form.mapName} onChange={(event) => setForm((current) => ({ ...current, mapName: event.target.value }))} />
                </label>
                <label>
                  <span>Amount</span>
                  <input value={form.amount} onChange={(event) => setForm((current) => ({ ...current, amount: event.target.value }))} />
                </label>
                <label>
                  <span>Respawn (ms)</span>
                  <input value={form.respawn} onChange={(event) => setForm((current) => ({ ...current, respawn: event.target.value }))} />
                </label>
              </div>
            </FieldGroup>
            <FieldGroup id="monster-drops" title="Drops e MVP" description="Sintaxe livre para manter a expressividade dos casos complexos.">
              <label>
                <span>Drops (item=...,chance=...,quantity=...,mvp=true)</span>
                <textarea rows={5} value={form.dropsText} onChange={(event) => setForm((current) => ({ ...current, dropsText: event.target.value }))} />
              </label>
            </FieldGroup>
            <FieldGroup id="monster-skills" title="Skills" description="Apoia multiplas skills mantendo a sintaxe ja usada na CLI.">
              <label>
                <span>Skills (id=...,state=...,rate=...,castTime=...,target=...)</span>
                <textarea rows={5} value={form.skillsText} onChange={(event) => setForm((current) => ({ ...current, skillsText: event.target.value }))} />
              </label>
            </FieldGroup>
            <FieldGroup id="monster-spawns" title="Spawns" description="Mapa base e spawns adicionais com area, label e evento.">
              <label>
                <span>Spawns (map=...,amount=...,x=...,y=...,areaX=...,areaY=...,respawn=...,label=...)</span>
                <textarea rows={5} value={form.spawnsText} onChange={(event) => setForm((current) => ({ ...current, spawnsText: event.target.value }))} />
              </label>
            </FieldGroup>
          </div>
        }
        inspector={
          <div className="stack-lg">
            <ReadinessRibbon
              readiness={String(report?.applyReadiness ?? "Aguardando dry-run")}
              canApply={report?.canApply}
              warningsCount={combinedWarnings.length}
              errorsCount={combinedErrors.length}
              correlationId={dryRunMutation.data?.correlationId ?? diffMutation.data?.correlationId}
              modeLabel={report ? `Drops=${report.drops?.length ?? 0} Skills=${report.skills?.length ?? 0}` : undefined}
            />
            <DiffWorkbench
              title="Resultado do monstro"
              diffEntries={diff?.entries}
              warnings={combinedWarnings}
              tabs={[
                { key: "drops", label: "Drops", content: <MonsterDropsGrid drops={report?.drops} /> },
                { key: "skills", label: "Skills", content: <MonsterSkillsGrid skills={report?.skills} /> },
                { key: "spawns", label: "Spawns", content: <MonsterSpawnsGrid spawns={report?.spawns} /> },
                {
                  key: "validation",
                  label: "Validacoes",
                  content: (
                    <div id="monster-validation" className="stack-lg">
                      <ProblemDetailsView problem={error?.problem} />
                      <ResponseMeta response={dryRunMutation.data ?? diffMutation.data ?? null} />
                      <ApplyReadinessPanel label="Apply readiness" readiness={String(report?.applyReadiness ?? "")} blockReasons={[]} />
                      <ValidationMatrix
                        warnings={combinedWarnings}
                        errors={combinedErrors}
                        blockReasons={asStringArray(report?.unsupportedFields)}
                        validationWarnings={asStringArray(report?.postWriteValidationPlan)}
                      />
                    </div>
                  ),
                },
                {
                  key: "diff",
                  label: "Diff",
                  content: diff ? (
                    <DiffViewer diff={diff} />
                  ) : (
                    <section className="panel">
                      <p className="muted-text">
                        Gere um diff-preview para inspecionar mob_db, mob_avail, mob_skill_db e spawn.
                      </p>
                    </section>
                  ),
                },
                { key: "json", label: "JSON", content: <JsonInspector title="Dry-run bruto" value={report} /> },
              ]}
            />
          </div>
        }
      />
    </section>
  );
}
