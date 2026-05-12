import { useQuery } from "@tanstack/react-query";
import { ApiClientError } from "../api/client";
import { ApiStatusBadge } from "../components/ApiStatusBadge";
import { ConnectionPanel } from "../components/ConnectionPanel";
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

export function SecurityPage() {
  const { client, connection } = useApiConfig();

  const statusQuery = useQuery({
    queryKey: ["status", connection.baseUrl, connection.apiKey],
    queryFn: () => client.status(),
  });

  const capabilitiesQuery = useQuery({
    queryKey: ["capabilities", connection.baseUrl, connection.apiKey],
    queryFn: () => client.safetyCapabilities(),
  });

  const error = (statusQuery.error ?? capabilitiesQuery.error) as ApiClientError | null;
  const status = statusQuery.data?.data;
  const capabilities = capabilitiesQuery.data?.data ?? [];

  const navigatorItems = [
    {
      key: "security-auth",
      label: "Autenticacao local",
      subtitle: connection.apiKey ? "API key configurada" : "API key ausente",
      status: connection.apiKey ? ("good" as const) : ("warning" as const),
      href: "#security-auth",
    },
    {
      key: "security-guards",
      label: "Operation guards",
      subtitle: `${status?.disabledWriteOperations?.length ?? 0} bloqueios ativos`,
      status: status?.readOnlyMode ? ("good" as const) : ("danger" as const),
      href: "#security-guards",
    },
    {
      key: "security-cors",
      label: "CORS e limites",
      subtitle: "Origins locais e limites de request",
      status: ("neutral" as const),
      href: "#security-cors",
    },
    {
      key: "security-openapi",
      label: "OpenAPI",
      subtitle: "/openapi/v1.json",
      status: ("neutral" as const),
      href: "#security-openapi",
    },
  ];

  return (
    <section className="page">
      <PageHeader
        title="Seguranca e API"
        description="Conexao local, autenticacao, operation guards e visibilidade do modo seguro da API."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <div className="stack-lg">
            <EntityGrid
              title="Checklist de seguranca"
              description="O que esta protegido e o que a UI pode consumir nesta fase."
              items={navigatorItems}
              activeKey="security-auth"
            />
            <section className="panel panel--danger">
              <div className="panel-header">
                <h3>Bloqueio fixo</h3>
              </div>
              <p>
                Apply e rollback nao existem nesta interface nesta fase. Esta UI e apenas para
                analise, dry-run e diff-preview.
              </p>
            </section>
          </div>
        }
        primary={
          <div className="stack-lg">
            {status ? <SafetyBanner status={status} /> : null}
            <FieldGroup
              id="security-auth"
              title="Autenticacao local"
              description="API Base URL, API key e teste de conexao local."
            >
              <ConnectionPanel compact />
            </FieldGroup>
            <FieldGroup
              id="security-guards"
              title="Operation guards"
              description="Operacoes liberadas e operacoes bloqueadas pela API endurecida."
            >
              <div className="token-wrap">
                <span className="token">Permitido: ReadOnly</span>
                <span className="token">Permitido: DryRun</span>
                <span className="token">Permitido: DiffPreview</span>
                <span className="token token--danger">Bloqueado: Apply</span>
                <span className="token token--danger">Bloqueado: Rollback</span>
                <span className="token token--danger">Bloqueado: FileWrite</span>
                <span className="token token--danger">Bloqueado: ExternalRepoWrite</span>
                <span className="token token--danger">Bloqueado: GrfWrite</span>
              </div>
            </FieldGroup>
            <FieldGroup
              id="security-cors"
              title="CORS e rate limits"
              description="Resumo do envelope seguro usado pela UI nesta fase."
            >
              <div className="detail-grid">
                <div>
                  <dt>API Base URL</dt>
                  <dd>{connection.baseUrl}</dd>
                </div>
                <div>
                  <dt>Status da autenticacao</dt>
                  <dd>{connection.apiKey ? "API key presente" : "API key ausente"}</dd>
                </div>
                <div>
                  <dt>CORS esperado</dt>
                  <dd>http://127.0.0.1:5173, http://localhost:5173</dd>
                </div>
                <div>
                  <dt>Max request body</dt>
                  <dd>{String(status?.maxRequestBodyBytes ?? "-")} bytes</dd>
                </div>
                <div>
                  <dt>Max GRF containers</dt>
                  <dd>{String(status?.maxGrfContainersPerRequest ?? "-")}</dd>
                </div>
                <div>
                  <dt>Max diff hunks</dt>
                  <dd>{String(status?.maxDiffHunksPerResponse ?? "-")}</dd>
                </div>
              </div>
            </FieldGroup>
          </div>
        }
        inspector={
          <div className="stack-lg">
            <ReadinessRibbon
              readiness={status?.readOnlyMode ? "ReadOnly mode ativo" : "ReadOnly mode ausente"}
              canApply={false}
              warningsCount={capabilitiesQuery.data?.warnings?.length ?? 0}
              errorsCount={
                (statusQuery.data?.errors?.length ?? 0) + (capabilitiesQuery.data?.errors?.length ?? 0)
              }
              correlationId={statusQuery.data?.correlationId ?? capabilitiesQuery.data?.correlationId}
              modeLabel={status?.mode}
            />
            <ValidationMatrix
              warnings={[
                ...(statusQuery.data?.warnings ?? []),
                ...(capabilitiesQuery.data?.warnings ?? []),
              ]}
              errors={[
                ...(statusQuery.data?.errors ?? []),
                ...(capabilitiesQuery.data?.errors ?? []),
              ]}
              blockReasons={status?.disabledWriteOperations ?? []}
            />
            <DiffWorkbench
              title="Detalhes da seguranca"
              tabs={[
                {
                  key: "summary",
                  label: "Resumo",
                  content: (
                    <div className="stack-lg">
                      <ProblemDetailsView problem={error?.problem} />
                      <ResponseMeta response={statusQuery.data ?? capabilitiesQuery.data ?? null} />
                      <div className="badge-grid">
                        <ApiStatusBadge
                          label={`ReadOnlyMode = ${String(status?.readOnlyMode ?? false)}`}
                          tone={status?.readOnlyMode ? "good" : "danger"}
                        />
                        <ApiStatusBadge
                          label={`ApplyEnabled = ${String(status?.applyEndpointsEnabled ?? false)}`}
                          tone={status?.applyEndpointsEnabled ? "danger" : "good"}
                        />
                        <ApiStatusBadge
                          label={`RollbackEnabled = ${String(status?.rollbackEndpointsEnabled ?? false)}`}
                          tone={status?.rollbackEndpointsEnabled ? "danger" : "good"}
                        />
                      </div>
                    </div>
                  ),
                },
                {
                  key: "capabilities",
                  label: "Capabilities",
                  content: <JsonInspector title="Capabilities" value={capabilities} />,
                },
                {
                  key: "status",
                  label: "Status bruto",
                  content: (
                    <div className="stack-lg">
                      <JsonInspector title="Status detalhado" value={status} />
                      <JsonInspector
                        title="OpenAPI"
                        value={{
                          openApiUrl: `${connection.baseUrl}/openapi/v1.json`,
                        }}
                      />
                    </div>
                  ),
                },
              ]}
            />
            <FieldGroup
              id="security-openapi"
              title="OpenAPI"
              description="Link seguro para a documentacao viva da API."
            >
              <a href={`${connection.baseUrl}/openapi/v1.json`} target="_blank" rel="noreferrer">
                {connection.baseUrl}/openapi/v1.json
              </a>
            </FieldGroup>
          </div>
        }
      />
    </section>
  );
}
