import { useMutation, useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { ApiClientError } from "../api/client";
import type {
  ApiResponse,
  PipelineDependencyItem,
  PipelineDiffPreviewResponse,
  PipelineDryRunResponse,
  PipelineIssuesResponse,
  PipelinePlanResponse,
  PipelineWorkspaceRequest,
} from "../api/types";
import { ApiStatusBadge } from "../components/ApiStatusBadge";
import { DependencyTree, type DependencyTreeStatus } from "../components/DependencyTree";
import { DiffWorkbench } from "../components/DiffWorkbench";
import { FieldGroup } from "../components/FieldGroup";
import { JsonInspector } from "../components/JsonInspector";
import { PageHeader } from "../components/PageHeader";
import { PipelineWorkspaceLayout } from "../components/PipelineWorkspaceLayout";
import { ProblemDetailsView } from "../components/ProblemDetailsView";
import { ReadinessRibbon } from "../components/ReadinessRibbon";
import { ResponseMeta } from "../components/ResponseMeta";
import { useApiConfig } from "../features/connection/ApiConfigContext";

const defaultPayloads: Record<string, Record<string, unknown>> = {
  item: {
    id: 501,
    aegisName: "RF_PIPELINE_ITEM",
    displayName: "RF Pipeline Item",
    type: "Usable",
  },
  equipment: {
    id: 22001,
    aegisName: "RF_PIPELINE_HEADGEAR",
    displayName: "RF Pipeline Headgear",
    type: "Armor",
    equipLocations: ["UpperHead"],
    visualCategory: "Headgear",
    viewId: 1200,
  },
  npc: {
    name: "rf_pipeline_npc",
    mapName: "prontera",
    x: 150,
    y: 150,
    direction: 4,
    sprite: 100,
    scriptBody: "mes \"Pipeline read-only\";\nclose;",
  },
  monster: {
    id: 3999,
    aegisName: "RF_PIPELINE_MOB",
    displayName: "RF Pipeline Mob",
    mapName: "prontera",
    level: 1,
    hp: 10,
    amount: 1,
    respawnMilliseconds: 60000,
  },
  map: {
    mapName: "rf_pipeline_map",
  },
  asset: {
    source: "Patch",
    entryPath: "data/sprite/item/rf_pipeline_item.spr",
    expectedExtension: ".spr",
  },
};

const statusMap: Record<string, DependencyTreeStatus> = {
  present: "resolved",
  resolved: "resolved",
  missing: "missing",
  ambiguous: "ambiguous",
  blocked: "blocked",
  unsupported: "blocked",
  placeholder: "read-only",
  notchecked: "read-only",
  externaldatawarning: "needs-copy-future",
};

function dependencyStatus(status: string): DependencyTreeStatus {
  return statusMap[status.replace(/[^a-z]/gi, "").toLowerCase()] ?? "read-only";
}

function dependencyItems(items: PipelineDependencyItem[]) {
  return items.map((item) => ({
    label: `${item.name} (${item.type})`,
    hint: item.expectedPath,
    origin: item.source,
    note: item.notes ?? undefined,
    status: dependencyStatus(item.status),
  }));
}

function safeJsonParse(value: string) {
  try {
    return { value: JSON.parse(value) as Record<string, unknown>, error: "" };
  } catch (error) {
    return {
      value: null,
      error: error instanceof Error ? error.message : "JSON invalido.",
    };
  }
}

function collectProblem(...errors: unknown[]) {
  return errors
    .map((error) => (error instanceof ApiClientError ? error.problem : undefined))
    .find(Boolean);
}

export function ApiPipelineWorkspacePage() {
  const { client, connection } = useApiConfig();
  const [entityType, setEntityType] = useState("item");
  const [mode, setMode] = useState("inspect");
  const [sourceHints, setSourceHints] = useState("");
  const [includeAssets, setIncludeAssets] = useState(true);
  const [includeClientSide, setIncludeClientSide] = useState(true);
  const [includeServerSide, setIncludeServerSide] = useState(true);
  const [payloadText, setPayloadText] = useState(JSON.stringify(defaultPayloads.item, null, 2));
  const [payloadError, setPayloadError] = useState("");

  const statusQuery = useQuery({
    queryKey: ["pipeline-status", connection.baseUrl, connection.apiKey],
    queryFn: () => client.pipelineStatus(),
  });

  const reportsQuery = useQuery({
    queryKey: ["pipeline-reports", connection.baseUrl, connection.apiKey],
    queryFn: () => client.pipelineReports(),
  });

  const issuesQuery = useQuery({
    queryKey: ["pipeline-issues", connection.baseUrl, connection.apiKey],
    queryFn: () => client.pipelineIssues(),
  });

  const planMutation = useMutation<ApiResponse<PipelinePlanResponse>, ApiClientError, PipelineWorkspaceRequest>({
    mutationFn: (payload) => client.pipelinePlan(payload),
  });

  const dryRunMutation = useMutation<
    ApiResponse<PipelineDryRunResponse>,
    ApiClientError,
    { operationId: string; entityType: string; payload: Record<string, unknown> }
  >({
    mutationFn: (payload) => client.pipelineDryRun(payload),
  });

  const diffMutation = useMutation<
    ApiResponse<PipelineDiffPreviewResponse>,
    ApiClientError,
    { operationId: string; entityType: string; payload: Record<string, unknown> }
  >({
    mutationFn: (payload) => client.pipelineDiffPreview(payload),
  });

  const plan = planMutation.data?.data;
  const dryRun = dryRunMutation.data?.data;
  const diffPreview = diffMutation.data?.data;
  const issues = issuesQuery.data?.data as PipelineIssuesResponse | undefined;
  const status = statusQuery.data?.data;
  const problem = collectProblem(
    statusQuery.error,
    reportsQuery.error,
    issuesQuery.error,
    planMutation.error,
    dryRunMutation.error,
    diffMutation.error,
  );

  const allWarnings = [
    ...(status?.currentKnownLimitations ?? []),
    ...(plan?.warnings ?? []),
    ...(dryRun?.warnings ?? []),
    ...(issues?.warnings ?? []),
  ];
  const allErrors = [
    ...(plan?.errors ?? []),
    ...(dryRun?.errors ?? []),
    ...(issues?.errors ?? []),
  ];

  const dependencyGroups = useMemo(() => {
    if (!plan?.dependencySummary) {
      return [];
    }

    return [
      { title: "Server DB", items: dependencyItems(plan.dependencySummary.serverDb) },
      { title: "Client-side", items: dependencyItems(plan.dependencySummary.clientDb) },
      { title: "Scripts", items: dependencyItems(plan.dependencySummary.scripts) },
      { title: "Assets", items: dependencyItems(plan.dependencySummary.assets) },
    ];
  }, [plan]);

  function onEntityChange(nextEntity: string) {
    setEntityType(nextEntity);
    setPayloadText(JSON.stringify(defaultPayloads[nextEntity] ?? {}, null, 2));
    setPayloadError("");
  }

  function buildPlanRequest(): PipelineWorkspaceRequest | null {
    const parsed = safeJsonParse(payloadText);
    if (!parsed.value) {
      setPayloadError(parsed.error);
      return null;
    }

    setPayloadError("");
    return {
      entityType,
      mode,
      payload: parsed.value,
      sourceHints,
      includeAssets,
      includeClientSide,
      includeServerSide,
    };
  }

  function runPlan() {
    const request = buildPlanRequest();
    if (request) {
      planMutation.mutate(request);
    }
  }

  function runDryRun() {
    const request = buildPlanRequest();
    const operationId = plan?.operationId;
    if (request && operationId) {
      dryRunMutation.mutate({ operationId, entityType: request.entityType, payload: request.payload });
    }
  }

  function runDiffPreview() {
    const request = buildPlanRequest();
    const operationId = plan?.operationId;
    if (request && operationId) {
      diffMutation.mutate({ operationId, entityType: request.entityType, payload: request.payload });
    }
  }

  return (
    <div className="stack-lg">
      <PageHeader
        title="API Pipeline Workspace"
        description="Workspace read-only para planejar, executar dry-run seguro e gerar diff-preview sem apply, rollback ou escrita externa."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <div className="stack-lg">
            <FieldGroup title="Seguranca operacional">
              <div className="token-wrap">
                <ApiStatusBadge label="ReadOnly = true" tone="good" />
                <ApiStatusBadge label="Dry-run seguro" tone="good" />
                <ApiStatusBadge label="Diff-preview seguro" tone="good" />
                <ApiStatusBadge label="Apply bloqueado" tone="danger" />
                <ApiStatusBadge label="Rollback real bloqueado" tone="danger" />
              </div>
              <div className="notice notice--info">
                Esta tela nao aceita comando livre, nao chama CLI pelo navegador e nao possui fluxo destrutivo.
              </div>
            </FieldGroup>

            <FieldGroup title="Entrada do pipeline" description="O JSON preenche apenas a chamada segura da API.">
              <div className="form-panel stack-sm">
                <label>
                  Entidade
                  <select value={entityType} onChange={(event) => onEntityChange(event.target.value)}>
                    <option value="item">Item</option>
                    <option value="equipment">Equipamento</option>
                    <option value="npc">NPC</option>
                    <option value="monster">Monstro</option>
                    <option value="map">Mapa</option>
                    <option value="asset">Asset</option>
                  </select>
                </label>
                <label>
                  Modo
                  <select value={mode} onChange={(event) => setMode(event.target.value)}>
                    <option value="inspect">Inspect</option>
                    <option value="dry-run">Dry-run</option>
                    <option value="diff-preview">Diff-preview</option>
                  </select>
                </label>
                <label>
                  Source hints
                  <input value={sourceHints} onChange={(event) => setSourceHints(event.target.value)} />
                </label>
                <label className="checkbox-field">
                  <input type="checkbox" checked={includeServerSide} onChange={(event) => setIncludeServerSide(event.target.checked)} />
                  Server-side
                </label>
                <label className="checkbox-field">
                  <input type="checkbox" checked={includeClientSide} onChange={(event) => setIncludeClientSide(event.target.checked)} />
                  Client-side
                </label>
                <label className="checkbox-field">
                  <input type="checkbox" checked={includeAssets} onChange={(event) => setIncludeAssets(event.target.checked)} />
                  Assets
                </label>
                <label>
                  Payload JSON
                  <textarea
                    rows={16}
                    value={payloadText}
                    spellCheck={false}
                    onChange={(event) => setPayloadText(event.target.value)}
                  />
                </label>
                {payloadError ? <div className="notice notice--danger">{payloadError}</div> : null}
                <div className="form-actions">
                  <button type="button" className="button-primary" onClick={runPlan} disabled={planMutation.isPending}>
                    Gerar plano
                  </button>
                  <button type="button" className="button-secondary" onClick={runDryRun} disabled={!plan || dryRunMutation.isPending}>
                    Executar dry-run seguro
                  </button>
                  <button type="button" className="button-secondary" onClick={runDiffPreview} disabled={!plan || diffMutation.isPending}>
                    Gerar diff-preview
                  </button>
                </div>
              </div>
            </FieldGroup>
          </div>
        }
        primary={
          <div className="stack-lg">
            <FieldGroup title="Status do pipeline" description="Resumo nativo da API e do Agent quando disponivel.">
              <div className="card-grid card-grid--compact">
                <article className="stat-card">
                  <h3>Read-only</h3>
                  <p>{status?.apiReadOnly ? "Sim" : "Nao"}</p>
                </article>
                <article className="stat-card">
                  <h3>Safe for audit</h3>
                  <p>{status?.safeForReadOnlyWork ? "Sim" : "Nao"}</p>
                </article>
                <article className="stat-card">
                  <h3>Safe for dry-run</h3>
                  <p>{status?.safeForDryRun ? "Sim" : "Nao"}</p>
                </article>
                <article className="stat-card">
                  <h3>Safe for apply</h3>
                  <p>{status?.safeForApply ? "Sim" : "Nao"}</p>
                </article>
              </div>
            </FieldGroup>

            <ReadinessRibbon
              readiness={plan ? (plan.readiness.canDryRun ? "Ready" : "Blocked") : "Pending"}
              canApply={plan?.readiness.canApply ?? false}
              warningsCount={allWarnings.length}
              errorsCount={allErrors.length}
              correlationId={planMutation.data?.correlationId ?? statusQuery.data?.correlationId}
              modeLabel="Pipeline API read-only"
            />

            {plan ? (
              <>
                <DependencyTree title="Dependency Resolver Summary" groups={dependencyGroups} />
                <FieldGroup title="Planned steps">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Step</th>
                        <th>Action</th>
                        <th>Status</th>
                        <th>Target</th>
                      </tr>
                    </thead>
                    <tbody>
                      {[...plan.plannedSteps, ...plan.blockedSteps].map((step) => (
                        <tr key={`${step.name}-${step.target}`}>
                          <td>{step.name}</td>
                          <td>{step.action}</td>
                          <td>{step.status}</td>
                          <td className="mono-text">{step.target}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </FieldGroup>
              </>
            ) : (
              <div className="notice notice--info">Gere um plano para ver dependencies, readiness, risks e blocked steps.</div>
            )}

            {dryRun ? (
              <FieldGroup title="Dry-run seguro">
                <div className="token-wrap">
                  <ApiStatusBadge label={`NoPersistentWrites = ${String(dryRun.noPersistentWrites)}`} tone={dryRun.noPersistentWrites ? "good" : "danger"} />
                  <ApiStatusBadge label={`SafeForApply = ${String(dryRun.safeForApply)}`} tone={dryRun.safeForApply ? "warning" : "danger"} />
                </div>
                <ul className="flat-list">
                  {dryRun.generatedFilesPreview.map((entry) => (
                    <li key={entry}>{entry}</li>
                  ))}
                </ul>
              </FieldGroup>
            ) : null}

            {diffPreview ? (
              <DiffWorkbench
                title="Diff Preview"
                diffEntries={diffPreview.diffByFile}
                warnings={allWarnings}
                tabs={[
                  {
                    key: "summary",
                    label: "Resumo",
                    content: (
                      <div className="token-wrap">
                        <ApiStatusBadge label={`Additions = ${diffPreview.additions}`} tone="neutral" />
                        <ApiStatusBadge label={`Modifications = ${diffPreview.modifications}`} tone="neutral" />
                        <ApiStatusBadge label={`Deletions = ${diffPreview.deletions}`} tone={diffPreview.deletions > 0 ? "danger" : "good"} />
                        <ApiStatusBadge label={`Risk = ${diffPreview.riskLevel}`} tone={diffPreview.riskLevel === "Low" ? "good" : "warning"} />
                      </div>
                    ),
                  },
                  { key: "raw", label: "Raw JSON", content: <JsonInspector title="Diff raw" value={diffPreview} /> },
                ]}
              />
            ) : null}

            {issues ? (
              <FieldGroup title="Issues dashboard" description="Resumo read-only do Agent para separar dados externos de risco de projeto.">
                <div className="card-grid card-grid--compact">
                  <article className="stat-card">
                    <h3>Total</h3>
                    <p>{issues.summary.total}</p>
                  </article>
                  <article className="stat-card">
                    <h3>External-data</h3>
                    <p>{issues.summary.externalDataCount}</p>
                  </article>
                  <article className="stat-card">
                    <h3>Apply blockers</h3>
                    <p>{issues.summary.applyBlockersCount}</p>
                  </article>
                  <article className="stat-card">
                    <h3>Dry-run blockers</h3>
                    <p>{issues.summary.dryRunBlockersCount}</p>
                  </article>
                </div>
              </FieldGroup>
            ) : null}
          </div>
        }
        inspector={
          <div className="stack-lg">
            <FieldGroup title="Warnings e errors">
              {allErrors.length ? allErrors.map((error) => <div key={error} className="notice notice--danger">{error}</div>) : null}
              {allWarnings.length ? allWarnings.map((warning) => <div key={warning} className="notice notice--warning">{warning}</div>) : null}
              {!allErrors.length && !allWarnings.length ? <div className="notice notice--good">Sem warnings/errors nesta consulta.</div> : null}
            </FieldGroup>
            <FieldGroup title="Reports read-only">
              {reportsQuery.data?.data?.length ? (
                <div className="report-list">
                  {reportsQuery.data.data.map((report) => (
                    <article key={report.id} className="report-list__item">
                      <strong>{report.title}</strong>
                      <div className="report-list__meta">
                        <span className="mono-pill">{report.entityType}</span>
                        <span className="mono-pill">{report.id}</span>
                        <span className="mono-pill">{report.sizeBytes} bytes</span>
                      </div>
                    </article>
                  ))}
                </div>
              ) : (
                <p className="muted-text">Nenhum report listado pela API.</p>
              )}
            </FieldGroup>
            <ResponseMeta response={planMutation.data ?? dryRunMutation.data ?? diffMutation.data ?? statusQuery.data ?? null} />
            <ProblemDetailsView problem={problem} />
            <JsonInspector title="Plan raw" value={plan} />
            <JsonInspector title="Dry-run raw" value={dryRun} />
            <JsonInspector title="Issues raw" value={issues} />
          </div>
        }
      />
    </div>
  );
}
