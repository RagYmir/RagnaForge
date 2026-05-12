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
  defaultItemFormValues,
  itemPresets,
  type ItemFormValues,
} from "../features/shared/pipelinePresets";
import { splitLines } from "../features/shared/requestBuilders";

const itemTypes = ["Etc", "Use", "Healing", "Card", "Armor", "Weapon"];

export function ItemsPage() {
  const { client } = useApiConfig();
  const [form, setForm] = useState<ItemFormValues>(defaultItemFormValues);
  const [historyRevision, setHistoryRevision] = useState(0);

  const buildPayload = () => ({
    configPath: form.configPath,
    aegisName: form.aegisName,
    displayName: form.displayName,
    resourceName: form.resourceName,
    type: form.type,
    buy: Number(form.buy) || 0,
    sell: Number(form.sell) || 0,
    weight: Number(form.weight) || 0,
    identifiedDescriptionLines: splitLines(form.description),
    assetGrfContainer: form.assetGrfContainer || undefined,
    scanGrfAssets: form.scanGrfAssets,
  });

  function refreshHistory() {
    setHistoryRevision((current) => current + 1);
  }

  function updateField<K extends keyof ItemFormValues>(key: K, value: ItemFormValues[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function applySavedForm(value: Partial<ItemFormValues>) {
    setForm((current) => ({ ...current, ...value }));
  }

  const dryRunMutation = useMutation({
    mutationFn: () => client.itemDryRun(buildPayload()),
    onSuccess: (response) => {
      savePipelineHistoryEntry({
        category: "items",
        kind: "dry-run",
        payload: { ...form },
        responseData: response.data,
        success: response.success,
        warningsCount: response.warnings.length + (response.data.validationWarnings?.length ?? 0),
        errorsCount: response.errors.length + (response.data.validationErrors?.length ?? 0),
        summary: form.aegisName || form.displayName || "Item dry-run",
        correlationId: response.correlationId,
        readiness: String(response.data.applyReadiness ?? ""),
        canApply: response.data.canApply,
        diffFileCount: response.data.diffPreview?.fileCount,
      });
      refreshHistory();
    },
  });

  const diffMutation = useMutation({
    mutationFn: () => client.itemDiffPreview(buildPayload()),
    onSuccess: (response) => {
      if (!response.data) {
        return;
      }
      savePipelineHistoryEntry({
        category: "items",
        kind: "diff-preview",
        payload: { ...form },
        responseData: response.data,
        success: response.success,
        warningsCount: response.warnings.length,
        errorsCount: response.errors.length,
        summary: `${form.aegisName || form.displayName || "Item"} diff-preview`,
        correlationId: response.correlationId,
        readiness: `${response.data.fileCount ?? 0} arquivos`,
        diffFileCount: response.data.fileCount,
      });
      refreshHistory();
    },
  });

  const historyEntries = useMemo(
    () => listPipelineHistory("items"),
    [historyRevision],
  );

  const report = dryRunMutation.data?.data;
  const diff = diffMutation.data?.data;
  const dryRunError = dryRunMutation.error as ApiClientError | null;
  const diffError = diffMutation.error as ApiClientError | null;
  const clientPlan = report?.clientSidePlan;

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

  const unknownItemRisk = !report
    ? "Nao avaliado"
    : clientPlan?.canApply === false ||
        (clientPlan?.blockReasons?.length ?? 0) > 0 ||
        (clientPlan?.bytecodeBlockedFiles?.length ?? 0) > 0
      ? "Alto"
      : report.assetLookup?.matches?.length === 0 && form.resourceName
        ? "Medio"
        : "Baixo";

  const previewItems = report?.assetLookup?.matches?.length
    ? report.assetLookup.matches.slice(0, 5).map((match, index) => ({
        key: `${match.relativePath ?? "asset"}-${index}`,
        path: String(match.relativePath ?? "-"),
        type: match.extension ?? "-",
        origin: match.containerPath ?? report.assetLookup?.source ?? "Unknown",
        status: "resolved" as const,
        note: "Origem somente leitura.",
      }))
    : form.resourceName
      ? [
          {
            key: "resource-placeholder",
            path: form.resourceName,
            type: "resource",
            origin: report?.assetLookup?.source ?? "Unknown",
            status: "read-only" as const,
            note: "Preview visual real sera ligado em etapa futura.",
          },
        ]
      : [];

  const navigatorItems = [
    {
      key: "item-identificacao",
      label: "Identificacao",
      subtitle: form.aegisName || "AegisName e nome do item",
      status:
        form.aegisName && form.displayName ? ("good" as const) : ("warning" as const),
      href: "#item-identificacao",
    },
    {
      key: "item-server",
      label: "Server DB",
      subtitle: `Type ${form.type} | Buy ${form.buy} | Sell ${form.sell}`,
      status: "neutral" as const,
      href: "#item-server",
    },
    {
      key: "item-client",
      label: "Client-side",
      subtitle: String(clientPlan?.clientSideMode ?? "Aguardando dry-run"),
      status: clientPlan?.canApply
        ? ("good" as const)
        : report
          ? ("warning" as const)
          : ("neutral" as const),
      href: "#item-client",
    },
    {
      key: "item-description",
      label: "Descricao",
      subtitle: form.description ? `${splitLines(form.description).length} linhas` : "Sem descricao",
      status: form.description ? ("good" as const) : ("neutral" as const),
      href: "#item-description",
    },
    {
      key: "item-resource",
      label: "Resource/Sprite",
      subtitle: form.resourceName || "Resource nao informado",
      status: form.resourceName ? ("good" as const) : ("warning" as const),
      href: "#item-resource",
    },
    {
      key: "item-validation",
      label: "Validacao",
      subtitle: report?.applyReadiness ? String(report.applyReadiness) : "Aguardando analise",
      status:
        combinedErrors.length > 0
          ? ("danger" as const)
          : combinedWarnings.length > 0
            ? ("warning" as const)
            : ("neutral" as const),
      href: "#item-validation",
    },
    {
      key: "item-diff",
      label: "Diff",
      subtitle: diff?.fileCount ? `${diff.fileCount} arquivos` : "Ainda nao gerado",
      status: diff?.fileCount ? ("good" as const) : ("neutral" as const),
      href: "#item-diff",
    },
  ];

  return (
    <section className="page">
      <PageHeader
        title="Itens"
        description="Workspace de proposta de item com foco em client-side, risco de Unknown Item ou Apple, presets seguros e historico local."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <div className="stack-lg">
            <EntityGrid
              title="Workspace do item"
              description="Navegacao por secoes da proposta atual."
              items={navigatorItems}
              activeKey="item-identificacao"
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
                  onClick={() => setForm(defaultItemFormValues)}
                >
                  Limpar formulario
                </button>
              </div>
            </section>
            <PresetPanel title="Presets seguros" presets={itemPresets} onApply={setForm} />
            <HistoryPanel
              title="Historico local"
              entries={historyEntries}
              onApplyPayload={(entry) => applySavedForm(entry.payload as Partial<ItemFormValues>)}
              onClear={() => {
                clearPipelineHistory("items");
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
              id="item-identificacao"
              title="Identificacao"
              description="Dados basicos da proposta que orientam os checks server/client."
            >
              <div className="form-grid">
                <label>
                  <span>Config path</span>
                  <input
                    value={form.configPath}
                    onChange={(event) => updateField("configPath", event.target.value)}
                  />
                </label>
                <label>
                  <span>AegisName</span>
                  <input
                    value={form.aegisName}
                    onChange={(event) => updateField("aegisName", event.target.value)}
                  />
                </label>
                <label>
                  <span>Nome</span>
                  <input
                    value={form.displayName}
                    onChange={(event) => updateField("displayName", event.target.value)}
                  />
                </label>
                <label>
                  <span>Resource</span>
                  <input
                    value={form.resourceName}
                    onChange={(event) => updateField("resourceName", event.target.value)}
                  />
                </label>
              </div>
            </FieldGroup>
            <FieldGroup
              id="item-server"
              title="Server DB"
              description="Campos centrais do item server-side."
            >
              <div className="form-grid">
                <label>
                  <span>Type</span>
                  <select value={form.type} onChange={(event) => updateField("type", event.target.value)}>
                    {itemTypes.map((entry) => (
                      <option key={entry} value={entry}>
                        {entry}
                      </option>
                    ))}
                  </select>
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
              </div>
            </FieldGroup>
            <FieldGroup
              id="item-client"
              title="Client-side"
              description="Parametros que influenciam itemInfo, tabelas legadas e lookup de assets."
            >
              <div className="form-grid">
                <label className="form-grid__wide">
                  <span>asset-grf-container opcional</span>
                  <input
                    value={form.assetGrfContainer}
                    onChange={(event) => updateField("assetGrfContainer", event.target.value)}
                  />
                </label>
                <label className="checkbox-field">
                  <input
                    checked={form.scanGrfAssets}
                    onChange={(event) => updateField("scanGrfAssets", event.target.checked)}
                    type="checkbox"
                  />
                  <span>scan-grf-assets</span>
                </label>
              </div>
            </FieldGroup>
            <FieldGroup
              id="item-description"
              title="Descricao"
              description="Linhas identificadas que entram na proposta client-side."
            >
              <label>
                <span>Descricao identificada</span>
                <textarea
                  rows={6}
                  value={form.description}
                  onChange={(event) => updateField("description", event.target.value)}
                />
              </label>
            </FieldGroup>
            <FieldGroup
              id="item-resource"
              title="Resource/Sprite"
              description="Estado do resource atual e do lookup futuro em Patch/GRF."
            >
              <div className="notice notice--info">
                A UI continua apenas em analise. O resource pode ser resolvido e auditado, mas nao
                existe qualquer fluxo de escrita nesta fase.
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
              title="Resultado do item"
              diffEntries={diff?.entries}
              warnings={combinedWarnings}
              tabs={[
                {
                  key: "validation",
                  label: "Validacao",
                  content: (
                    <div id="item-validation" className="stack-lg">
                      <ProblemDetailsView problem={dryRunError?.problem ?? diffError?.problem} />
                      <ResponseMeta response={dryRunMutation.data ?? diffMutation.data ?? null} />
                      <ApplyReadinessPanel
                        label="Apply readiness"
                        readiness={String(report?.applyReadiness ?? "")}
                        blockReasons={report?.clientSidePlan?.blockReasons as string[] | undefined}
                      />
                      <section className="panel">
                        <div className="panel-header">
                          <h3>Risco de Unknown Item/Apple</h3>
                        </div>
                        <p>{unknownItemRisk}</p>
                      </section>
                      <ValidationMatrix
                        warnings={combinedWarnings}
                        errors={combinedErrors}
                        blockReasons={report?.clientSidePlan?.blockReasons ?? []}
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
                      <ClientSidePlanPanel title="ClientSidePlan" plan={clientPlan} />
                      <AssetLookupPanel title="AssetLookup" report={report?.assetLookup} />
                      <PassiveAssetPreviewPanel title="Preview passivo de assets" items={previewItems} />
                      <DependencyTree
                        title="Registrations"
                        groups={[
                          {
                            title: "Existing client registration",
                            items:
                              report?.existingClientRegistration ??
                              clientPlan?.existingRegistrations ??
                              [],
                          },
                          {
                            title: "Proposed client registration",
                            items:
                              report?.proposedClientRegistration ??
                              clientPlan?.proposedRegistrations ??
                              [],
                          },
                        ]}
                      />
                      <BytecodeBlockPanel files={clientPlan?.bytecodeBlockedFiles} />
                    </div>
                  ),
                },
                {
                  key: "diff",
                  label: "Diff",
                  content: (
                    <div id="item-diff" className="stack-lg">
                      {diff ? (
                        <DiffViewer diff={diff} />
                      ) : (
                        <section className="panel">
                          <p className="muted-text">
                            Gere um diff-preview para ver o hunk server/client.
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
