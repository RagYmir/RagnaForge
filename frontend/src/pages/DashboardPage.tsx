import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { ApiClientError } from "../api/client";
import { ApiStatusBadge } from "../components/ApiStatusBadge";
import { DiffWorkbench } from "../components/DiffWorkbench";
import { EntityGrid } from "../components/EntityGrid";
import { FieldGroup } from "../components/FieldGroup";
import { JsonInspector } from "../components/JsonInspector";
import { PageHeader } from "../components/PageHeader";
import { PipelineWorkspaceLayout } from "../components/PipelineWorkspaceLayout";
import { ProblemDetailsView } from "../components/ProblemDetailsView";
import { ReadinessRibbon } from "../components/ReadinessRibbon";
import { ResponseMeta } from "../components/ResponseMeta";
import { SafetyBanner } from "../components/SafetyBanner";
import { ValidationMatrix } from "../components/ValidationMatrix";
import { useApiConfig } from "../features/connection/ApiConfigContext";
import { listPipelineHistory } from "../features/shared/localHistory";
import { defaultConfigPath } from "../features/shared/requestBuilders";

function asRecord(value: unknown): Record<string, unknown> | undefined {
  return value && typeof value === "object" ? (value as Record<string, unknown>) : undefined;
}

export function DashboardPage() {
  const { client, connection } = useApiConfig();

  const healthQuery = useQuery({
    queryKey: ["health", connection.baseUrl],
    queryFn: () => client.health(),
  });

  const statusQuery = useQuery({
    queryKey: ["status", connection.baseUrl, connection.apiKey],
    queryFn: () => client.status(),
  });

  const capabilitiesQuery = useQuery({
    queryKey: ["capabilities", connection.baseUrl, connection.apiKey],
    queryFn: () => client.safetyCapabilities(),
  });

  const configQuery = useQuery({
    queryKey: [
      "config-validate-dashboard",
      connection.baseUrl,
      connection.apiKey,
      defaultConfigPath,
    ],
    queryFn: () => client.validateConfig({ configPath: defaultConfigPath }),
  });

  const discoverQuery = useQuery({
    queryKey: [
      "discover-dashboard",
      connection.baseUrl,
      connection.apiKey,
      defaultConfigPath,
    ],
    queryFn: () =>
      client.discover({
        configPath: defaultConfigPath,
        maxGrfContainers: 10,
        saveCache: true,
      }),
  });

  const status = statusQuery.data?.data;
  const config = configQuery.data?.data;
  const discovery = discoverQuery.data?.data;
  const discoveryPatch = asRecord(asRecord(discovery)?.patch);
  const discoveryGrf = asRecord(asRecord(discovery)?.grf);
  const discoveryEditor = asRecord(asRecord(discovery)?.grfEditor);
  const discoveryRAthena = asRecord(asRecord(discovery)?.rAthena);

  const configIssues =
    config?.validation?.issues?.map((issue) => `${issue.code}: ${issue.message}`) ?? [];
  const dashboardWarnings = [
    ...(configQuery.data?.warnings ?? []),
    ...(discoverQuery.data?.warnings ?? []),
    ...configIssues.filter((issue) => !issue.toLowerCase().includes("error")),
  ];
  const dashboardErrors = [
    ...(configQuery.data?.errors ?? []),
    ...(discoverQuery.data?.errors ?? []),
    ...configIssues.filter((issue) => issue.toLowerCase().includes("error")),
  ];
  const pendingRisks = [
    config?.manifest?.clientDateStatus === "Unknown"
      ? "Manifest ainda indica ClientDate desconhecido."
      : "",
    ...(status?.disabledWriteOperations?.map((operation) => `${operation} bloqueado pela API`) ??
      []),
  ].filter(Boolean);
  const recentHistory = listPipelineHistory().slice(0, 5);
  const historyByCategory = [
    { label: "Itens", count: listPipelineHistory("items").length },
    { label: "Equipamentos", count: listPipelineHistory("equipment").length },
    { label: "NPCs", count: listPipelineHistory("npcs").length },
    { label: "Monstros", count: listPipelineHistory("monsters").length },
    { label: "Mapas", count: listPipelineHistory("maps").length },
  ];

  const moduleItems = [
    {
      key: "dashboard-api",
      label: "API e modo seguro",
      subtitle: healthQuery.data?.status ?? "Verificando health",
      status: status?.readOnlyMode ? ("good" as const) : ("danger" as const),
      href: "#dashboard-api",
    },
    {
      key: "dashboard-config",
      label: "Configuracao",
      subtitle: config?.validation?.isValid ? "Manifest validado" : "Validacao pendente",
      status: config?.validation?.isValid ? ("good" as const) : ("warning" as const),
      href: "#dashboard-config",
    },
    {
      key: "dashboard-repos",
      label: "Repositorios e assets",
      subtitle: discoveryRAthena?.repositoryRoot ? "Discovery carregado" : "Aguardando discovery",
      status: discoveryRAthena?.repositoryRoot ? ("good" as const) : ("warning" as const),
      href: "#dashboard-repos",
    },
    {
      key: "dashboard-readiness",
      label: "Readiness por modulo",
      subtitle: `${status?.capabilities?.length ?? 0} categorias reportadas`,
      status: status?.capabilities?.length ? ("good" as const) : ("neutral" as const),
      href: "#dashboard-readiness",
    },
    {
      key: "dashboard-risks",
      label: "Riscos pendentes",
      subtitle: `${pendingRisks.length + dashboardErrors.length} itens para acompanhar`,
      status:
        pendingRisks.length + dashboardErrors.length > 0
          ? ("warning" as const)
          : ("good" as const),
      href: "#dashboard-risks",
    },
  ];

  const lastProblem =
    (statusQuery.error as ApiClientError | null)?.problem ??
    (configQuery.error as ApiClientError | null)?.problem ??
    (discoverQuery.error as ApiClientError | null)?.problem ??
    (capabilitiesQuery.error as ApiClientError | null)?.problem;

  return (
    <div className="stack-lg">
      <PageHeader
        title="Dashboard"
        description="Painel operacional do modo seguro da API, inspirado na leitura tecnica compacta do RagnarokSDE."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <div className="stack-lg">
            <EntityGrid
              title="Visao rapida"
              description="Mapa das areas acompanhadas no dashboard."
              items={moduleItems}
              activeKey="dashboard-api"
            />
            <section className="panel">
              <div className="panel-header">
                <h3>Atalhos seguros</h3>
              </div>
              <div className="button-row">
                <Link className="nav-link nav-link-active" to="/itens">
                  Item dry-run
                </Link>
                <Link className="nav-link nav-link-active" to="/equipamentos">
                  Equipment dry-run
                </Link>
                <Link className="nav-link nav-link-active" to="/seguranca">
                  Seguranca/API
                </Link>
                <Link className="nav-link nav-link-active" to="/auditoria">
                  Historico local
                </Link>
              </div>
            </section>
          </div>
        }
        primary={
          <div className="stack-lg">
            <SafetyBanner status={status} />
            <FieldGroup
              id="dashboard-api"
              title="API health e modo seguro"
              description="Resumo do estado atual da API local endurecida e das operacoes liberadas."
            >
              <div className="card-grid card-grid--compact">
                <article className="stat-card">
                  <h3>Health</h3>
                  <p>{healthQuery.data?.status ?? "Carregando..."}</p>
                </article>
                <article className="stat-card">
                  <h3>API mode</h3>
                  <p>{status?.mode ?? "Carregando..."}</p>
                </article>
                <article className="stat-card">
                  <h3>ReadOnlyMode</h3>
                  <p>{String(status?.readOnlyMode ?? true)}</p>
                </article>
                <article className="stat-card">
                  <h3>Auth</h3>
                  <p>{status?.requireApiKey ? "API key obrigatoria" : "API key opcional"}</p>
                </article>
              </div>
            </FieldGroup>
            <FieldGroup
              id="dashboard-config"
              title="Configuracao ativa"
              description="Manifest, client date e limites de operacao que influenciam a UI."
            >
              <div className="detail-grid">
                <div>
                  <dt>Config validation</dt>
                  <dd>{config?.validation?.isValid ? "Valida" : "Pendente"}</dd>
                </div>
                <div>
                  <dt>Client date</dt>
                  <dd>
                    {String(
                      config?.manifest?.episodeProfile?.clientDate ??
                        discoveryPatch?.clientDate ??
                        "unknown",
                    )}
                  </dd>
                </div>
                <div>
                  <dt>Request size</dt>
                  <dd>{String(status?.maxRequestBodyBytes ?? "-")} bytes</dd>
                </div>
                <div>
                  <dt>Max diff hunks</dt>
                  <dd>{String(status?.maxDiffHunksPerResponse ?? "-")}</dd>
                </div>
              </div>
            </FieldGroup>
            <FieldGroup
              id="dashboard-repos"
              title="rAthena, Patch e GRFs"
              description="Snapshot rapido do discovery read-only para os repositorios externos e dependencias."
            >
              <div className="card-grid card-grid--compact">
                <article className="stat-card">
                  <h3>rAthena</h3>
                  <p>{String(discoveryRAthena?.repositoryRoot ?? "Nao carregado")}</p>
                </article>
                <article className="stat-card">
                  <h3>Patch</h3>
                  <p>{String(discoveryPatch?.repositoryRoot ?? "Nao carregado")}</p>
                </article>
                <article className="stat-card">
                  <h3>GRF Editor</h3>
                  <p>{String(discoveryEditor?.rootPath ?? "Nao carregado")}</p>
                </article>
                <article className="stat-card">
                  <h3>GRFs</h3>
                  <p>
                    {String(
                      (discoveryGrf?.containers as unknown[] | undefined)?.length ?? 0,
                    )}{" "}
                    containers
                  </p>
                </article>
              </div>
            </FieldGroup>
            <FieldGroup
              id="dashboard-history"
              title="Historico local de dry-runs"
              description="Resumo do historico salvo no navegador para acelerar reuso e comparacao."
            >
              <div className="card-grid card-grid--compact">
                {historyByCategory.map((entry) => (
                  <article key={entry.label} className="stat-card">
                    <h3>{entry.label}</h3>
                    <p>{entry.count} resultado(s)</p>
                  </article>
                ))}
              </div>
              {recentHistory.length ? (
                <div className="stack-sm">
                  {recentHistory.map((entry) => (
                    <article key={entry.id} className="history-entry">
                      <div className="history-entry__meta">
                        <strong>{entry.summary}</strong>
                        <div className="token-wrap">
                          <span className="mono-pill">{entry.category}</span>
                          <span className="mono-pill">{entry.kind}</span>
                          <span className="mono-pill">{entry.readiness ?? "-"}</span>
                        </div>
                      </div>
                    </article>
                  ))}
                </div>
              ) : (
                <div className="notice notice--info">
                  Nenhum dry-run local salvo ainda. Os proximos resultados entram automaticamente no historico local.
                </div>
              )}
            </FieldGroup>
          </div>
        }
        inspector={
          <div className="stack-lg">
            <ReadinessRibbon
              readiness={config?.validation?.isValid ? "Ready for read-only UI" : "Pending validation"}
              canApply={false}
              warningsCount={dashboardWarnings.length}
              errorsCount={dashboardErrors.length}
              correlationId={statusQuery.data?.correlationId}
              modeLabel={status?.mode}
            />
            <ValidationMatrix
              warnings={dashboardWarnings}
              errors={dashboardErrors}
              blockReasons={pendingRisks}
            />
            <FieldGroup
              id="dashboard-readiness"
              title="Readiness por modulo"
              description="Categorias expostas hoje pela API segura."
            >
              <div className="token-wrap">
                {(status?.capabilities ?? []).map((capability) => (
                  <ApiStatusBadge
                    key={capability.category}
                    label={`${capability.category}: ${capability.dryRun ? "dry-run" : "read-only"}`}
                    tone={capability.diffPreview ? "good" : "warning"}
                  />
                ))}
              </div>
            </FieldGroup>
            <DiffWorkbench
              title="Detalhes tecnicos"
              tabs={[
                {
                  key: "status",
                  label: "Status",
                  content: (
                    <div className="stack-lg">
                      <ResponseMeta response={statusQuery.data ?? null} />
                      <JsonInspector title="Status bruto" value={status} />
                    </div>
                  ),
                },
                {
                  key: "discovery",
                  label: "Discovery",
                  content: (
                    <div className="stack-lg">
                      <ResponseMeta response={discoverQuery.data ?? null} />
                      <JsonInspector title="Discovery bruto" value={discovery} />
                    </div>
                  ),
                },
                {
                  key: "guards",
                  label: "Guards",
                  content: (
                    <div className="stack-lg">
                      <ResponseMeta response={capabilitiesQuery.data ?? null} />
                      <JsonInspector
                        title="Safety capabilities"
                        value={capabilitiesQuery.data?.data}
                      />
                    </div>
                  ),
                },
              ]}
            />
            <div id="dashboard-risks">
              <ProblemDetailsView problem={lastProblem} />
            </div>
          </div>
        }
      />
    </div>
  );
}
