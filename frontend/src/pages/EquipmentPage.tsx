import { useMutation } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { ApiClientError } from "../api/client";
import { ApplyReadinessPanel } from "../components/ApplyReadinessPanel";
import { AssetLookupPanel } from "../components/AssetLookupPanel";
import { BytecodeBlockPanel } from "../components/BytecodeBlockPanel";
import { ClientSidePlanPanel } from "../components/ClientSidePlanPanel";
import { DependencyTree } from "../components/DependencyTree";
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
  defaultEquipmentFormValues,
  equipmentPresets,
  type EquipmentFormValues,
} from "../features/shared/pipelinePresets";
import { splitLines, splitTokens } from "../features/shared/requestBuilders";

const equipmentTypes = ["Armor", "Weapon", "Card", "ShadowGear"];
const visualCategories = ["Headgear", "Robe", "Weapon", "Shield", "Accessory"];

export function EquipmentPage() {
  const { client } = useApiConfig();
  const [form, setForm] = useState<EquipmentFormValues>(defaultEquipmentFormValues);
  const [historyRevision, setHistoryRevision] = useState(0);

  const buildPayload = () => ({
    configPath: form.configPath,
    aegisName: form.aegisName,
    displayName: form.displayName,
    resourceName: form.resourceName,
    type: form.type,
    equipLocations: splitTokens(form.locations),
    buy: Number(form.buy) || 0,
    sell: Number(form.sell) || 0,
    weight: Number(form.weight) || 0,
    identifiedDescriptionLines: splitLines(form.description),
    viewId: Number(form.viewId) || 0,
    visualCategory: form.visualCategory,
    clientSymbolName: form.clientSymbolName,
    clientSpriteName: form.clientSpriteName,
    weaponBaseType: form.weaponBaseType || undefined,
    assetGrfContainer: form.assetGrfContainer || undefined,
    scanGrfAssets: form.scanGrfAssets,
  });

  function refreshHistory() {
    setHistoryRevision((current) => current + 1);
  }

  function updateField<K extends keyof EquipmentFormValues>(
    key: K,
    value: EquipmentFormValues[K],
  ) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function applySavedForm(value: Partial<EquipmentFormValues>) {
    setForm((current) => ({ ...current, ...value }));
  }

  const dryRunMutation = useMutation({
    mutationFn: () => client.equipmentDryRun(buildPayload()),
    onSuccess: (response) => {
      savePipelineHistoryEntry({
        category: "equipment",
        kind: "dry-run",
        payload: { ...form },
        responseData: response.data,
        success: response.success,
        warningsCount: response.warnings.length + (response.data.validationWarnings?.length ?? 0),
        errorsCount: response.errors.length + (response.data.validationErrors?.length ?? 0),
        summary: form.aegisName || form.displayName || "Equipment dry-run",
        correlationId: response.correlationId,
        readiness: String(response.data.applyReadiness ?? ""),
        canApply: response.data.canApply,
        diffFileCount: response.data.diffPreview?.fileCount,
      });
      refreshHistory();
    },
  });

  const diffMutation = useMutation({
    mutationFn: () => client.equipmentDiffPreview(buildPayload()),
    onSuccess: (response) => {
      if (!response.data) {
        return;
      }
      savePipelineHistoryEntry({
        category: "equipment",
        kind: "diff-preview",
        payload: { ...form },
        responseData: response.data,
        success: response.success,
        warningsCount: response.warnings.length,
        errorsCount: response.errors.length,
        summary: `${form.aegisName || form.displayName || "Equipment"} diff-preview`,
        correlationId: response.correlationId,
        readiness: `${response.data.fileCount ?? 0} arquivos`,
        diffFileCount: response.data.fileCount,
      });
      refreshHistory();
    },
  });

  const historyEntries = useMemo(
    () => listPipelineHistory("equipment"),
    [historyRevision],
  );

  const report = dryRunMutation.data?.data;
  const diff = diffMutation.data?.data;
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

  const combinedBytecodeBlocks = [
    ...(report?.clientSidePlan?.bytecodeBlockedFiles ?? []),
    ...(report?.visualClientSidePlan?.bytecodeBlockedFiles ?? []),
  ];

  const previewItems = [
    ...(report?.itemAssetLookup?.matches?.slice(0, 3).map((match, index) => ({
      key: `item-asset-${index}`,
      path: String(match.relativePath ?? "-"),
      type: match.extension ?? "-",
      origin: match.containerPath ?? report.itemAssetLookup?.source ?? "Unknown",
      status: "resolved" as const,
      note: "Lookup do item base.",
    })) ?? []),
    ...(report?.visualAssetLookup?.matches?.slice(0, 3).map((match, index) => ({
      key: `visual-asset-${index}`,
      path: String(match.relativePath ?? "-"),
      type: match.extension ?? "-",
      origin: match.containerPath ?? report.visualAssetLookup?.source ?? "Unknown",
      status: "resolved" as const,
      note: "Lookup do visual client-side.",
    })) ?? []),
  ];

  const navigatorItems = [
    {
      key: "equipment-base",
      label: "Item base",
      subtitle: form.aegisName || "AegisName, nome e resource",
      status:
        form.aegisName && form.displayName ? ("good" as const) : ("warning" as const),
      href: "#equipment-base",
    },
    {
      key: "equipment-gear",
      label: "Equipamento",
      subtitle: `${form.type} | ${form.locations}`,
      status: "neutral" as const,
      href: "#equipment-gear",
    },
    {
      key: "equipment-visual",
      label: "Visual client-side",
      subtitle: `${form.visualCategory} | ViewID ${form.viewId}`,
      status: report?.visualClientSidePlan?.canApply
        ? ("good" as const)
        : report
          ? ("warning" as const)
          : ("neutral" as const),
      href: "#equipment-visual",
    },
    {
      key: "equipment-assets",
      label: "Assets",
      subtitle: form.resourceName || form.clientSpriteName || "Sem sprite definido",
      status:
        form.resourceName || form.clientSpriteName ? ("good" as const) : ("warning" as const),
      href: "#equipment-assets",
    },
    {
      key: "equipment-validation",
      label: "Validacoes",
      subtitle: report?.applyReadiness ? String(report.applyReadiness) : "Aguardando analise",
      status:
        combinedErrors.length ? ("danger" as const) : combinedWarnings.length ? ("warning" as const) : ("neutral" as const),
      href: "#equipment-validation",
    },
    {
      key: "equipment-diff",
      label: "Diff",
      subtitle: diff?.fileCount ? `${diff.fileCount} arquivos` : "Ainda nao gerado",
      status: diff?.fileCount ? ("good" as const) : ("neutral" as const),
      href: "#equipment-diff",
    },
  ];

  return (
    <section className="page">
      <PageHeader
        title="Equipamentos"
        description="Workspace visual de equipamento como especializacao de item, com presets seguros, assets passivos e historico local."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <div className="stack-lg">
            <EntityGrid
              title="Workspace do equipamento"
              description="Secoes inspiradas na organizacao de item + visual do RagnarokSDE."
              items={navigatorItems}
              activeKey="equipment-base"
            />
            <section className="panel">
              <div className="panel-header">
                <h3>Acoes seguras</h3>
              </div>
              <div className="form-actions">
                <button
                  onClick={() => dryRunMutation.mutate()}
                  type="button"
                  disabled={!form.aegisName || !form.displayName}
                >
                  Gerar dry-run
                </button>
                <button
                  onClick={() => diffMutation.mutate()}
                  type="button"
                  disabled={!form.aegisName || !form.displayName}
                >
                  Gerar diff-preview
                </button>
                <button
                  type="button"
                  className="button-secondary"
                  onClick={() => setForm(defaultEquipmentFormValues)}
                >
                  Limpar formulario
                </button>
              </div>
            </section>
            <PresetPanel title="Presets seguros" presets={equipmentPresets} onApply={setForm} />
            <HistoryPanel
              title="Historico local"
              entries={historyEntries}
              onApplyPayload={(entry) => applySavedForm(entry.payload as Partial<EquipmentFormValues>)}
              onClear={() => {
                clearPipelineHistory("equipment");
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
              id="equipment-base"
              title="Item base"
              description="Identificacao e dados server-side comuns ao item base."
            >
              <div className="form-grid">
                <label>
                  <span>Config path</span>
                  <input value={form.configPath} onChange={(event) => updateField("configPath", event.target.value)} />
                </label>
                <label>
                  <span>AegisName</span>
                  <input value={form.aegisName} onChange={(event) => updateField("aegisName", event.target.value)} />
                </label>
                <label>
                  <span>Nome</span>
                  <input value={form.displayName} onChange={(event) => updateField("displayName", event.target.value)} />
                </label>
                <label>
                  <span>Resource</span>
                  <input value={form.resourceName} onChange={(event) => updateField("resourceName", event.target.value)} />
                </label>
                <label>
                  <span>Buy</span>
                  <input value={form.buy} onChange={(event) => updateField("buy", event.target.value)} />
                </label>
                <label>
                  <span>Sell</span>
                  <input value={form.sell} onChange={(event) => updateField("sell", event.target.value)} />
                </label>
                <label>
                  <span>Weight</span>
                  <input value={form.weight} onChange={(event) => updateField("weight", event.target.value)} />
                </label>
                <label>
                  <span>Descricao identificada</span>
                  <textarea rows={4} value={form.description} onChange={(event) => updateField("description", event.target.value)} />
                </label>
              </div>
            </FieldGroup>
            <FieldGroup
              id="equipment-gear"
              title="Equipamento"
              description="Tipo de equip, slots logicos e dados do item especializado."
            >
              <div className="form-grid">
                <label>
                  <span>Type</span>
                  <select value={form.type} onChange={(event) => updateField("type", event.target.value)}>
                    {equipmentTypes.map((entry) => (
                      <option key={entry} value={entry}>
                        {entry}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>Locations</span>
                  <input value={form.locations} onChange={(event) => updateField("locations", event.target.value)} />
                </label>
              </div>
            </FieldGroup>
            <FieldGroup
              id="equipment-visual"
              title="Visual client-side"
              description="ViewID, categoria visual, simbolo e sprite do client."
            >
              <div className="form-grid">
                <label>
                  <span>ViewID</span>
                  <input value={form.viewId} onChange={(event) => updateField("viewId", event.target.value)} />
                </label>
                <label>
                  <span>VisualCategory</span>
                  <select value={form.visualCategory} onChange={(event) => updateField("visualCategory", event.target.value)}>
                    {visualCategories.map((entry) => (
                      <option key={entry} value={entry}>
                        {entry}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>ClientSymbol</span>
                  <input value={form.clientSymbolName} onChange={(event) => updateField("clientSymbolName", event.target.value)} />
                </label>
                <label>
                  <span>ClientSprite</span>
                  <input value={form.clientSpriteName} onChange={(event) => updateField("clientSpriteName", event.target.value)} />
                </label>
                <label>
                  <span>WeaponBaseType</span>
                  <input value={form.weaponBaseType} onChange={(event) => updateField("weaponBaseType", event.target.value)} />
                </label>
                <label>
                  <span>VisualTheme</span>
                  <input value={form.visualTheme} onChange={(event) => updateField("visualTheme", event.target.value)} />
                </label>
              </div>
            </FieldGroup>
            <FieldGroup
              id="equipment-assets"
              title="Assets"
              description="Origem esperada dos assets e parametros de lookup em GRF."
            >
              <div className="form-grid">
                <label className="form-grid__wide">
                  <span>asset-grf-container opcional</span>
                  <input value={form.assetGrfContainer} onChange={(event) => updateField("assetGrfContainer", event.target.value)} />
                </label>
                <label className="checkbox-field">
                  <input checked={form.scanGrfAssets} onChange={(event) => updateField("scanGrfAssets", event.target.checked)} type="checkbox" />
                  <span>scan-grf-assets</span>
                </label>
              </div>
              <div className="notice notice--info">
                VisualTheme e uma anotacao local de UX nesta fase. O backend atual nao recebe esse campo como contrato formal.
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
              modeLabel={
                report?.clientSidePlan?.clientSideMode
                  ? `ClientSideMode = ${report.clientSidePlan.clientSideMode}`
                  : undefined
              }
            />
            <DiffWorkbench
              title="Resultado do equipamento"
              diffEntries={diff?.entries}
              warnings={combinedWarnings}
              tabs={[
                {
                  key: "validation",
                  label: "Validacoes",
                  content: (
                    <div id="equipment-validation" className="stack-lg">
                      <ProblemDetailsView problem={error?.problem} />
                      <ResponseMeta response={dryRunMutation.data ?? diffMutation.data ?? null} />
                      <ApplyReadinessPanel
                        label="Apply readiness"
                        readiness={String(report?.applyReadiness ?? "")}
                        blockReasons={
                          (report?.visualClientSidePlan?.blockReasons as string[] | undefined) ??
                          (report?.clientSidePlan?.blockReasons as string[] | undefined)
                        }
                      />
                      <ValidationMatrix
                        warnings={combinedWarnings}
                        errors={combinedErrors}
                        blockReasons={[
                          ...((report?.visualClientSidePlan?.blockReasons as string[] | undefined) ?? []),
                          ...((report?.clientSidePlan?.blockReasons as string[] | undefined) ?? []),
                        ]}
                        validationWarnings={report?.postWriteValidationPlan ?? []}
                      />
                    </div>
                  ),
                },
                {
                  key: "client",
                  label: "Client-side",
                  content: (
                    <div className="stack-lg">
                      <ClientSidePlanPanel title="ClientSidePlan" plan={report?.clientSidePlan} />
                      <ClientSidePlanPanel title="VisualClientSidePlan" plan={report?.visualClientSidePlan} />
                      <DependencyTree
                        title="Visual checks"
                        groups={[
                          {
                            title: "Accessory plan",
                            items: report?.accessoryPlan ? [JSON.stringify(report.accessoryPlan)] : [],
                          },
                          {
                            title: "Robe plan",
                            items: report?.robePlan ? [JSON.stringify(report.robePlan)] : [],
                          },
                          {
                            title: "Weapon plan",
                            items: report?.weaponPlan ? [JSON.stringify(report.weaponPlan)] : [],
                          },
                          {
                            title: "Shield restriction",
                            items: report?.shieldRestriction ? [report.shieldRestriction] : [],
                          },
                        ]}
                      />
                      <BytecodeBlockPanel files={combinedBytecodeBlocks} />
                    </div>
                  ),
                },
                {
                  key: "assets",
                  label: "Assets",
                  content: (
                    <div className="stack-lg">
                      <AssetLookupPanel title="ItemAssetLookup" report={report?.itemAssetLookup} />
                      <AssetLookupPanel title="VisualAssetLookup" report={report?.visualAssetLookup} />
                      <PassiveAssetPreviewPanel title="Preview passivo de assets" items={previewItems} />
                      <section className="panel">
                        <div className="panel-header">
                          <h3>Visual summary</h3>
                        </div>
                        <dl className="detail-grid">
                          <div>
                            <dt>ViewID</dt>
                            <dd>{String(report?.viewId ?? form.viewId)}</dd>
                          </div>
                          <div>
                            <dt>ViewSprite</dt>
                            <dd>{String(report?.viewSprite ?? form.clientSpriteName ?? "-")}</dd>
                          </div>
                          <div>
                            <dt>VisualTheme</dt>
                            <dd>{form.visualTheme || "Nao informado"}</dd>
                          </div>
                          <div>
                            <dt>ShieldRestriction</dt>
                            <dd>{String(report?.shieldRestriction ?? "Nenhuma")}</dd>
                          </div>
                        </dl>
                      </section>
                    </div>
                  ),
                },
                {
                  key: "diff",
                  label: "Diff",
                  content: (
                    <div id="equipment-diff" className="stack-lg">
                      {diff ? (
                        <DiffViewer diff={diff} />
                      ) : (
                        <section className="panel">
                          <p className="muted-text">
                            Gere um diff-preview para inspecionar item DB, tabelas visuais e client-side.
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
                      <JsonInspector
                        title="Campos visuais"
                        value={{
                          viewId: report?.viewId,
                          viewSprite: report?.viewSprite,
                          shieldRestriction: report?.shieldRestriction,
                          visualThemeHint: form.visualTheme || null,
                        }}
                      />
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
