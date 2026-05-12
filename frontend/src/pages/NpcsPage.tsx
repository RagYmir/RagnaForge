import { useMutation } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { ApiClientError } from "../api/client";
import { ApplyReadinessPanel } from "../components/ApplyReadinessPanel";
import { AssetLookupPanel } from "../components/AssetLookupPanel";
import { ClientIdentityPlanPanel } from "../components/ClientIdentityPlanPanel";
import { DiffViewer } from "../components/DiffViewer";
import { DiffWorkbench } from "../components/DiffWorkbench";
import { EntityGrid } from "../components/EntityGrid";
import { FieldGroup } from "../components/FieldGroup";
import { HistoryPanel } from "../components/HistoryPanel";
import { JsonInspector } from "../components/JsonInspector";
import { PageHeader } from "../components/PageHeader";
import { PassiveAssetPreviewPanel } from "../components/PassiveAssetPreviewPanel";
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
  defaultNpcFormValues,
  npcPresets,
  type NpcFormValues,
} from "../features/shared/pipelinePresets";
import { asRecord, asStringArray, toBoolean, toStringValue } from "../features/shared/viewData";

export function NpcsPage() {
  const { client } = useApiConfig();
  const [form, setForm] = useState<NpcFormValues>(defaultNpcFormValues);
  const [historyRevision, setHistoryRevision] = useState(0);

  const buildPayload = () => ({
    configPath: form.configPath,
    name: form.name,
    mapName: form.mapName,
    x: Number(form.x) || 0,
    y: Number(form.y) || 0,
    direction: Number(form.dir) || 0,
    sprite: form.sprite,
    scriptBody: form.scriptBody,
    assetGrfContainer: form.assetGrfContainer || undefined,
    scanGrfAssets: form.scanGrfAssets,
  });

  function refreshHistory() {
    setHistoryRevision((current) => current + 1);
  }

  function updateField<K extends keyof NpcFormValues>(key: K, value: NpcFormValues[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  const dryRunMutation = useMutation({
    mutationFn: () => client.npcDryRun(buildPayload()),
    onSuccess: (response) => {
      savePipelineHistoryEntry({
        category: "npcs",
        kind: "dry-run",
        payload: { ...form },
        responseData: response.data,
        success: response.success,
        warningsCount:
          response.warnings.length +
          asStringArray(response.data.clientIdentityPlan?.validationWarnings).length,
        errorsCount:
          response.errors.length +
          asStringArray(response.data.clientIdentityPlan?.validationErrors).length,
        summary: form.name || "NPC dry-run",
        correlationId: response.correlationId,
        readiness: String(response.data.applyReadiness ?? ""),
        canApply: response.data.canApply,
        diffFileCount: response.data.diffPreview?.fileCount,
      });
      refreshHistory();
    },
  });

  const diffMutation = useMutation({
    mutationFn: () => client.npcDiffPreview(buildPayload()),
    onSuccess: (response) => {
      if (!response.data) {
        return;
      }
      savePipelineHistoryEntry({
        category: "npcs",
        kind: "diff-preview",
        payload: { ...form },
        responseData: response.data,
        success: response.success,
        warningsCount: response.warnings.length,
        errorsCount: response.errors.length,
        summary: `${form.name || "NPC"} diff-preview`,
        correlationId: response.correlationId,
        readiness: `${response.data.fileCount ?? 0} arquivos`,
        diffFileCount: response.data.fileCount,
      });
      refreshHistory();
    },
  });

  const historyEntries = useMemo(() => listPipelineHistory("npcs"), [historyRevision]);
  const report = dryRunMutation.data?.data;
  const diff = diffMutation.data?.data ?? report?.diffPreview;
  const error = (dryRunMutation.error ?? diffMutation.error) as ApiClientError | null;
  const spriteResolution = asRecord(report?.spriteResolution);
  const spriteValidation = asRecord((report as Record<string, unknown> | undefined)?.spriteValidation);
  const spriteLookup = asRecord(spriteValidation?.assetLookup);
  const spriteMatches = Array.isArray(spriteLookup?.matches)
    ? (spriteLookup.matches as Array<Record<string, unknown>>)
    : [];

  const combinedWarnings = useMemo(
    () => [
      ...(dryRunMutation.data?.warnings ?? []),
      ...(report?.warnings ?? []),
      ...asStringArray(report?.clientIdentityPlan?.validationWarnings),
      ...asStringArray(report?.postWriteValidationPlan),
    ],
    [dryRunMutation.data, report],
  );

  const combinedErrors = useMemo(
    () => [
      ...(dryRunMutation.data?.errors ?? []),
      ...asStringArray(report?.clientIdentityPlan?.validationErrors),
    ],
    [dryRunMutation.data, report],
  );

  const identityPlan = report?.clientIdentityPlan;
  const selectedFormats = identityPlan?.filesDetected?.map((file) => String(file.format)) ?? [];
  const previewItems = spriteMatches.length
    ? spriteMatches.slice(0, 5).map((match, index) => ({
        key: `sprite-${index}`,
        path: toStringValue(match.relativePath),
        type: toStringValue(match.extension, "-"),
        origin: toStringValue(match.containerPath, toStringValue(report?.detectionSource)),
        status: "resolved" as const,
        note: "Sprite resolvido em modo read-only.",
      }))
    : form.sprite
      ? [
          {
            key: "sprite-placeholder",
            path: form.sprite,
            type: "npc-sprite",
            origin: toStringValue(report?.detectionSource, "Unknown"),
            status: toBoolean(spriteResolution?.resolved) ? ("resolved" as const) : ("read-only" as const),
            note: "Preview visual real sera etapa futura.",
          },
        ]
      : [];

  const navigatorItems = [
    {
      key: "npc-identificacao",
      label: "Identificacao",
      subtitle: form.name || "Nome e slug do NPC",
      status: form.name ? ("good" as const) : ("warning" as const),
      href: "#npc-identificacao",
    },
    {
      key: "npc-localizacao",
      label: "Localizacao",
      subtitle: form.mapName ? `${form.mapName} @ ${form.x}, ${form.y}` : "Mapa e coordenadas",
      status: form.mapName ? ("good" as const) : ("warning" as const),
      href: "#npc-localizacao",
    },
    {
      key: "npc-sprite",
      label: "Sprite",
      subtitle: form.sprite || "Sprite nao informado",
      status:
        toBoolean(spriteResolution?.resolved)
          ? ("good" as const)
          : form.sprite
            ? ("warning" as const)
            : ("neutral" as const),
      href: "#npc-sprite",
    },
    {
      key: "npc-script",
      label: "Dialogo/script",
      subtitle: form.scriptBody ? `${form.scriptBody.split(/\r?\n/).length} linhas` : "Sem corpo",
      status: form.scriptBody ? ("good" as const) : ("warning" as const),
      href: "#npc-script",
    },
    {
      key: "npc-identity",
      label: "Client identity",
      subtitle: identityPlan ? toStringValue(identityPlan.applyReadiness, "Planejada") : "Aguardando dry-run",
      status:
        identityPlan?.canApply
          ? ("good" as const)
          : identityPlan
            ? ("warning" as const)
            : ("neutral" as const),
      href: "#npc-identity",
    },
    {
      key: "npc-validation",
      label: "Validacao",
      subtitle: report?.applyReadiness ? String(report.applyReadiness) : "Aguardando analise",
      status:
        combinedErrors.length > 0
          ? ("danger" as const)
          : combinedWarnings.length > 0
            ? ("warning" as const)
            : ("neutral" as const),
      href: "#npc-validation",
    },
    {
      key: "npc-diff",
      label: "Diff",
      subtitle: diff?.fileCount ? `${diff.fileCount} arquivos` : "Ainda nao gerado",
      status: diff?.fileCount ? ("good" as const) : ("neutral" as const),
      href: "#npc-diff",
    },
  ];

  return (
    <section className="page">
      <PageHeader
        title="NPCs"
        description="Workspace operacional para NPCs padrao ou custom, com presets seguros, sprite resolution, client identity e historico local."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <div className="stack-lg">
            <EntityGrid
              title="Workspace do NPC"
              description="Secoes de identificacao, sprite, client identity e diff."
              items={navigatorItems}
              activeKey="npc-identificacao"
            />
            <section className="panel panel--danger">
              <div className="panel-header">
                <h3>CLI escape</h3>
              </div>
              <p>
                <code>--allow-server-only</code> existe apenas na CLI. Nesta interface ele aparece
                somente como contexto informativo, nunca como acao clicavel.
              </p>
            </section>
            <PresetPanel title="Presets seguros" presets={npcPresets} onApply={setForm} />
            <HistoryPanel
              title="Historico local"
              entries={historyEntries}
              onApplyPayload={(entry) => setForm(((entry.payload as unknown) as NpcFormValues) ?? defaultNpcFormValues)}
              onClear={() => {
                clearPipelineHistory("npcs");
                refreshHistory();
              }}
              onExportJson={exportHistoryEntryJson}
              onExportMarkdown={exportHistoryEntryMarkdown}
            />
          </div>
        }
        primary={
          <div className="stack-lg">
            <FieldGroup
              id="npc-identificacao"
              title="Identificacao"
              description="Dados centrais do NPC e do arquivo de configuracao usado no dry-run."
            >
              <div className="form-grid">
                <label>
                  <span>Config path</span>
                  <input value={form.configPath} onChange={(event) => updateField("configPath", event.target.value)} />
                </label>
                <label>
                  <span>Name</span>
                  <input value={form.name} onChange={(event) => updateField("name", event.target.value)} />
                </label>
              </div>
            </FieldGroup>
            <FieldGroup
              id="npc-localizacao"
              title="Localizacao"
              description="Mapa, coordenadas e direcao do NPC server-side."
            >
              <div className="form-grid">
                <label>
                  <span>Map</span>
                  <input value={form.mapName} onChange={(event) => updateField("mapName", event.target.value)} />
                </label>
                <label>
                  <span>X</span>
                  <input value={form.x} onChange={(event) => updateField("x", event.target.value)} />
                </label>
                <label>
                  <span>Y</span>
                  <input value={form.y} onChange={(event) => updateField("y", event.target.value)} />
                </label>
                <label>
                  <span>Dir</span>
                  <input value={form.dir} onChange={(event) => updateField("dir", event.target.value)} />
                </label>
              </div>
            </FieldGroup>
            <FieldGroup
              id="npc-sprite"
              title="Sprite"
              description="Sprite padrao ou custom, com contexto de Patch/GRF quando necessario."
            >
              <div className="form-grid">
                <label>
                  <span>Sprite</span>
                  <input value={form.sprite} onChange={(event) => updateField("sprite", event.target.value)} />
                </label>
                <label className="form-grid__wide">
                  <span>asset-grf-container opcional</span>
                  <input value={form.assetGrfContainer} onChange={(event) => updateField("assetGrfContainer", event.target.value)} />
                </label>
                <label className="checkbox-field">
                  <input checked={form.scanGrfAssets} onChange={(event) => updateField("scanGrfAssets", event.target.checked)} type="checkbox" />
                  <span>scan-grf-assets</span>
                </label>
              </div>
            </FieldGroup>
            <FieldGroup
              id="npc-script"
              title="Dialogo/script basico"
              description="Script server-side usado na proposta atual."
            >
              <label>
                <span>Dialogo/script basico</span>
                <textarea rows={8} value={form.scriptBody} onChange={(event) => updateField("scriptBody", event.target.value)} />
              </label>
            </FieldGroup>
            <FieldGroup
              id="npc-identity"
              title="Client identity"
              description="Identidade client-side planejada a partir de jobname/jobidentity/npcidentity."
            >
              <div className="badge-grid">
                <span className="mono-pill">
                  DetectionSource = {toStringValue(report?.detectionSource, "-")}
                </span>
                <span className="mono-pill">
                  RequiredClientFiles = {String(report?.requiredClientFiles?.length ?? 0)}
                </span>
                <span className="mono-pill">
                  IdentityFormats = {selectedFormats.length ? selectedFormats.join(", ") : "-"}
                </span>
              </div>
            </FieldGroup>
            <FieldGroup
              id="npc-validation"
              title="Server-side e validacao"
              description="Acoes seguras para gerar planejamento e diff-preview."
            >
              <div className="form-actions">
                <button
                  onClick={() => dryRunMutation.mutate()}
                  type="button"
                  disabled={!form.name || !form.mapName || !form.sprite}
                >
                  Gerar dry-run
                </button>
                <button
                  onClick={() => diffMutation.mutate()}
                  type="button"
                  disabled={!form.name || !form.mapName || !form.sprite}
                >
                  Gerar diff-preview
                </button>
                <button type="button" className="button-secondary" onClick={() => setForm(defaultNpcFormValues)}>
                  Limpar formulario
                </button>
              </div>
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
              modeLabel={report?.clientIdentityPlan ? "Client identity planejada" : undefined}
            />
            <DiffWorkbench
              title="Resultado do NPC"
              diffEntries={diff?.entries}
              warnings={combinedWarnings}
              tabs={[
                {
                  key: "identity",
                  label: "Identity",
                  content: (
                    <div className="stack-lg">
                      <ClientIdentityPlanPanel report={report} />
                      <AssetLookupPanel title="Sprite asset lookup" report={spriteLookup as never} />
                      <PassiveAssetPreviewPanel title="Preview passivo do sprite" items={previewItems} />
                    </div>
                  ),
                },
                {
                  key: "validation",
                  label: "Validacao",
                  content: (
                    <div className="stack-lg">
                      <ProblemDetailsView problem={error?.problem} />
                      <ResponseMeta response={dryRunMutation.data ?? diffMutation.data ?? null} />
                      <ApplyReadinessPanel
                        label="Apply readiness"
                        readiness={String(report?.applyReadiness ?? "")}
                        blockReasons={report?.clientIdentityPlan?.blockReasons as string[] | undefined}
                      />
                      <ValidationMatrix
                        warnings={combinedWarnings}
                        errors={combinedErrors}
                        blockReasons={report?.clientIdentityPlan?.blockReasons ?? []}
                        validationWarnings={asStringArray(report?.postWriteValidationPlan)}
                      />
                    </div>
                  ),
                },
                {
                  key: "diff",
                  label: "Diff",
                  content: (
                    <div id="npc-diff" className="stack-lg">
                      {diff ? (
                        <DiffViewer diff={diff} />
                      ) : (
                        <section className="panel">
                          <p className="muted-text">
                            Gere um diff-preview para comparar script server-side e hunks client-side textuais.
                          </p>
                        </section>
                      )}
                    </div>
                  ),
                },
                {
                  key: "json",
                  label: "JSON",
                  content: (
                    <div className="stack-lg">
                      <JsonInspector title="Dry-run bruto" value={report} />
                      <JsonInspector title="Diff bruto" value={diff} />
                    </div>
                  ),
                },
              ]}
            />
          </div>
        }
      />
    </section>
  );
}
