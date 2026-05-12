import { useMutation } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { ApiClientError } from "../api/client";
import { ApplyReadinessPanel } from "../components/ApplyReadinessPanel";
import { DependencyTree } from "../components/DependencyTree";
import { DiffViewer } from "../components/DiffViewer";
import { DiffWorkbench } from "../components/DiffWorkbench";
import { EntityGrid } from "../components/EntityGrid";
import { FieldGroup } from "../components/FieldGroup";
import { HistoryPanel } from "../components/HistoryPanel";
import { JsonInspector } from "../components/JsonInspector";
import { MapCachePlanPanel } from "../components/MapCachePlanPanel";
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
import { defaultMapFormValues, mapPresets, type MapFormValues } from "../features/shared/pipelinePresets";
import { asRecord, asRecordArray, asStringArray, toStringValue } from "../features/shared/viewData";

export function MapsPage() {
  const { client } = useApiConfig();
  const [form, setForm] = useState<MapFormValues>(defaultMapFormValues);
  const [historyRevision, setHistoryRevision] = useState(0);

  const buildPayload = () => ({
    configPath: form.configPath,
    mapName: form.mapName,
    assetGrfContainer: form.assetGrfContainer || undefined,
    scanGrfAssets: form.scanGrfAssets,
  });

  function refreshHistory() {
    setHistoryRevision((current) => current + 1);
  }

  const dryRunMutation = useMutation({
    mutationFn: () => client.mapDryRun(buildPayload()),
    onSuccess: (response) => {
      savePipelineHistoryEntry({
        category: "maps",
        kind: "dry-run",
        payload: { ...form },
        responseData: response.data,
        success: response.success,
        warningsCount: response.warnings.length + (response.data.warnings?.length ?? 0),
        errorsCount: response.errors.length + (response.data.errors?.length ?? 0),
        summary: form.mapName || "Map dry-run",
        correlationId: response.correlationId,
        readiness: String(response.data.applyReadiness ?? ""),
        canApply: response.data.canApply,
        diffFileCount: response.data.diffPreview?.fileCount,
      });
      refreshHistory();
    },
  });

  const diffMutation = useMutation({
    mutationFn: () => client.mapDiffPreview(buildPayload()),
    onSuccess: (response) => {
      if (!response.data) {
        return;
      }
      savePipelineHistoryEntry({
        category: "maps",
        kind: "diff-preview",
        payload: { ...form },
        responseData: response.data,
        success: response.success,
        warningsCount: response.warnings.length,
        errorsCount: response.errors.length,
        summary: `${form.mapName || "Map"} diff-preview`,
        correlationId: response.correlationId,
        readiness: `${response.data.fileCount ?? 0} arquivos`,
        diffFileCount: response.data.fileCount,
      });
      refreshHistory();
    },
  });

  const historyEntries = useMemo(() => listPipelineHistory("maps"), [historyRevision]);
  const report = dryRunMutation.data?.data;
  const diff = diffMutation.data?.data ?? report?.diffPreview;
  const error = (dryRunMutation.error ?? diffMutation.error) as ApiClientError | null;

  const combinedWarnings = useMemo(
    () => [
      ...(dryRunMutation.data?.warnings ?? []),
      ...(report?.warnings ?? []),
      ...asStringArray(asRecord(report)?.dependencyScan ? asRecord(asRecord(report)?.dependencyScan)?.warnings : []),
      ...(diffMutation.data?.warnings ?? []),
    ],
    [dryRunMutation.data, diffMutation.data, report],
  );

  const combinedErrors = useMemo(
    () => [
      ...(dryRunMutation.data?.errors ?? []),
      ...(report?.errors ?? []),
      ...(diffMutation.data?.errors ?? []),
    ],
    [dryRunMutation.data, diffMutation.data, report],
  );

  const assetPlans = asRecordArray(report?.assetPlans);
  const dependencyScan = asRecord(report?.dependencyScan);
  const referencedAssets = asRecordArray(dependencyScan?.referencedAssets);
  const missingAssets = referencedAssets.filter((asset) => asset.resolved === false);
  const resolvedAssets = referencedAssets.filter((asset) => asset.resolved === true);
  const ambiguousAssets = assetPlans.filter((plan) =>
    String(plan.sourceKind ?? "").toLowerCase().includes("ambiguous"),
  );
  const requiredFiles = assetPlans.filter((plan) => Boolean(plan.required));
  const renameBlocked = combinedWarnings.concat(combinedErrors).some((entry) =>
    entry.toLowerCase().includes("rename"),
  );

  const previewItems = [
    ...resolvedAssets.slice(0, 5).map((asset, index) => ({
      key: `resolved-${index}`,
      path: toStringValue(asset.referencePath),
      type: toStringValue(asset.category, "-"),
      origin: toStringValue(asset.sourceKind, "Patch"),
      status: "resolved" as const,
      note: "Resolvido sem extracao.",
    })),
    ...missingAssets.slice(0, 5).map((asset, index) => ({
      key: `missing-${index}`,
      path: toStringValue(asset.referencePath),
      type: toStringValue(asset.category, "-"),
      origin: "Unknown",
      status: "missing" as const,
      note: "Asset ausente ou nao resolvido.",
    })),
  ];

  const navigatorItems = [
    {
      key: "map-base",
      label: "Mapa",
      subtitle: form.mapName || "Nome do mapa e config",
      status: form.mapName ? ("good" as const) : ("warning" as const),
      href: "#map-base",
    },
    {
      key: "map-assets",
      label: "Dependencies",
      subtitle: assetPlans.length ? `${assetPlans.length} asset plans` : "Sem scan ainda",
      status: assetPlans.length ? ("good" as const) : ("neutral" as const),
      href: "#map-assets",
    },
    {
      key: "map-cache",
      label: "Map cache",
      subtitle: report?.mapCachePlan ? "Plano disponivel" : "Aguardando dry-run",
      status: report?.mapCachePlan ? ("good" as const) : ("warning" as const),
      href: "#map-cache",
    },
    {
      key: "map-validation",
      label: "Validacoes",
      subtitle: report?.applyReadiness ? String(report.applyReadiness) : "Aguardando analise",
      status:
        combinedErrors.length > 0
          ? ("danger" as const)
          : combinedWarnings.length > 0
            ? ("warning" as const)
            : ("neutral" as const),
      href: "#map-validation",
    },
  ];

  return (
    <section className="page">
      <PageHeader
        title="Mapas"
        description="Workspace de dependencias de mapa com presets seguros, historico local, arvore de dependencias e preview passivo."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <div className="stack-lg">
            <EntityGrid
              title="Workspace do mapa"
              description="Visao por mapa, dependencias, cache e diff seguro."
              items={navigatorItems}
              activeKey="map-base"
            />
            <section className="panel panel--danger">
              <div className="panel-header">
                <h3>Bloqueio operacional</h3>
              </div>
              <p>
                Apply de mapa nao existe na interface. Aqui so mostramos riscos, dependencias, planos de copia e map cache.
              </p>
            </section>
            <PresetPanel title="Presets seguros" presets={mapPresets} onApply={setForm} />
            <HistoryPanel
              title="Historico local"
              entries={historyEntries}
              onApplyPayload={(entry) => setForm(((entry.payload as unknown) as MapFormValues) ?? defaultMapFormValues)}
              onClear={() => {
                clearPipelineHistory("maps");
                refreshHistory();
              }}
              onExportJson={exportHistoryEntryJson}
              onExportMarkdown={exportHistoryEntryMarkdown}
            />
          </div>
        }
        primary={
          <div className="stack-lg">
            <FieldGroup id="map-base" title="Mapa" description="Parametros minimos para discovery e diff de mapa.">
              <div className="form-grid">
                <label>
                  <span>Config path</span>
                  <input value={form.configPath} onChange={(event) => setForm((current) => ({ ...current, configPath: event.target.value }))} />
                </label>
                <label>
                  <span>MapName</span>
                  <input value={form.mapName} onChange={(event) => setForm((current) => ({ ...current, mapName: event.target.value }))} />
                </label>
                <label className="form-grid__wide">
                  <span>asset-grf-container opcional</span>
                  <input value={form.assetGrfContainer} onChange={(event) => setForm((current) => ({ ...current, assetGrfContainer: event.target.value }))} />
                </label>
                <label className="checkbox-field">
                  <input checked={form.scanGrfAssets} onChange={(event) => setForm((current) => ({ ...current, scanGrfAssets: event.target.checked }))} type="checkbox" />
                  <span>scan-grf-assets</span>
                </label>
              </div>
            </FieldGroup>
            <FieldGroup id="map-assets" title="Dependencies" description="Dry-run seguro com arvore de arquivos, assets resolvidos e riscos de fonte.">
              <div className="form-actions">
                <button onClick={() => dryRunMutation.mutate()} type="button" disabled={!form.mapName}>
                  Gerar dry-run
                </button>
                <button onClick={() => diffMutation.mutate()} type="button" disabled={!form.mapName}>
                  Gerar diff-preview
                </button>
                <button type="button" className="button-secondary" onClick={() => setForm(defaultMapFormValues)}>
                  Limpar formulario
                </button>
              </div>
            </FieldGroup>
            <FieldGroup id="map-cache" title="Map cache e configuracao" description="Planos de map_index, map config e cache aparecem no inspetor lateral.">
              <div className="notice notice--warning">
                Estados visuais fortes desta aba: mapa existente, dependencias ausentes, dependencias ambiguas, map_cache.dat necessario e rename binario bloqueado.
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
              modeLabel={report?.needsCopy != null ? `NeedsCopy = ${String(report.needsCopy)}` : undefined}
            />
            <DiffWorkbench
              title="Resultado do mapa"
              diffEntries={diff?.entries}
              warnings={combinedWarnings}
              tabs={[
                {
                  key: "dependencies",
                  label: "Dependencies",
                  content: (
                    <div className="stack-lg">
                      <DependencyTree
                        title="Dependency tree"
                        groups={[
                          {
                            title: "Required files",
                            items: requiredFiles.map((plan) => ({
                              label: toStringValue(plan.relativePath),
                              hint: `(${toStringValue(plan.category)})`,
                              status: "read-only" as const,
                              origin: toStringValue(plan.sourceKind, "Patch"),
                            })),
                          },
                          {
                            title: "Resolved assets",
                            items: resolvedAssets.map((asset) => ({
                              label: toStringValue(asset.referencePath),
                              hint: `(${toStringValue(asset.category)})`,
                              status: "resolved" as const,
                              origin: toStringValue(asset.sourceKind, "Patch"),
                            })),
                          },
                          {
                            title: "Missing assets",
                            items: missingAssets.map((asset) => ({
                              label: toStringValue(asset.referencePath),
                              hint: `(${toStringValue(asset.category)})`,
                              status: "missing" as const,
                              origin: "Unknown",
                            })),
                          },
                          {
                            title: "Ambiguous assets",
                            items: ambiguousAssets.map((asset) => ({
                              label: toStringValue(asset.relativePath),
                              hint: `(${toStringValue(asset.sourceKind)})`,
                              status: "ambiguous" as const,
                              origin: toStringValue(asset.sourceKind, "Unknown"),
                            })),
                          },
                          {
                            title: "Config plan",
                            items: assetPlans
                              .filter((plan) =>
                                ["config", "map_index", "cache"].includes(String(plan.category ?? "").toLowerCase()),
                              )
                              .map((plan) => ({
                                label: toStringValue(plan.targetPath),
                                status: "read-only" as const,
                                origin: toStringValue(plan.category, "Config"),
                              })),
                          },
                        ]}
                      />
                      <PassiveAssetPreviewPanel title="Preview passivo" items={previewItems} />
                      <MapCachePlanPanel plan={report?.mapCachePlan} />
                    </div>
                  ),
                },
                {
                  key: "validation",
                  label: "Validacoes",
                  content: (
                    <div id="map-validation" className="stack-lg">
                      <ProblemDetailsView problem={error?.problem} />
                      <ResponseMeta response={dryRunMutation.data ?? diffMutation.data ?? null} />
                      <ApplyReadinessPanel
                        label="Apply readiness"
                        readiness={String(report?.applyReadiness ?? "")}
                        blockReasons={renameBlocked ? ["Binary rename blocked"] : []}
                      />
                      <ValidationMatrix
                        warnings={combinedWarnings}
                        errors={combinedErrors}
                        blockReasons={[
                          ...(renameBlocked ? ["Binary rename blocked"] : []),
                          ...(missingAssets.length ? [`Missing assets: ${missingAssets.length}`] : []),
                          ...(ambiguousAssets.length ? [`Ambiguous assets: ${ambiguousAssets.length}`] : []),
                        ]}
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
                        Gere um diff-preview para ver map_index/config e o plano de cache.
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
