import { useQuery } from "@tanstack/react-query";
import { ApiClientError } from "../api/client";
import type { AgentHealthSummary } from "../api/types";
import { ApiStatusBadge } from "../components/ApiStatusBadge";
import { FieldGroup } from "../components/FieldGroup";
import { JsonInspector } from "../components/JsonInspector";
import { PageHeader } from "../components/PageHeader";
import { PipelineWorkspaceLayout } from "../components/PipelineWorkspaceLayout";
import { ProblemDetailsView } from "../components/ProblemDetailsView";
import { ResponseMeta } from "../components/ResponseMeta";
import { useApiConfig } from "../features/connection/ApiConfigContext";

function formatSafetyLabel(key: string) {
  const spaced = key.replace(/([A-Z])/g, " $1").trim();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

export function AgentHealthPage() {
  const { client, connection } = useApiConfig();

  const agentQuery = useQuery({
    queryKey: ["agent-health", connection.baseUrl, connection.apiKey],
    queryFn: () => client.agentHealth(),
    refetchInterval: 60_000, // Auto-refresh every 60s
  });

  const agent = agentQuery.data?.data as AgentHealthSummary | undefined;
  const problem = (agentQuery.error as ApiClientError | null)?.problem;
  const cacheWarnings = agent?.warnings?.filter((warning) =>
    warning.toLowerCase().includes("stale") ||
    warning.toLowerCase().includes("cache") ||
    warning.toLowerCase().includes("fingerprint")
  ) ?? [];

  return (
    <div className="stack-lg">
      <PageHeader
        title="Agent Health"
        description="Painel de diagnostico read-only do RagnaForge Agent local. Nenhuma operacao de escrita e permitida."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <div className="stack-lg">
            <section className="panel">
              <div className="panel-header">
                <h3>Status rapido</h3>
              </div>
              <div className="token-wrap">
                <ApiStatusBadge
                  label={agent?.agentReachable ? "Agent Online" : "Agent Offline"}
                  tone={agent?.agentReachable ? "good" : "danger"}
                />
                <ApiStatusBadge
                  label={agent?.statusOk ? "Status OK" : "Status Falha"}
                  tone={agent?.statusOk ? "good" : "danger"}
                />
                <ApiStatusBadge
                  label={agent?.doctorOk ? `Doctor ${agent.doctor.passed}/${agent.doctor.totalChecks}` : "Doctor Falha"}
                  tone={agent?.doctorOk ? "good" : "danger"}
                />
                <ApiStatusBadge
                  label={agent?.grfProtected ? "GRF Protegido" : "GRF Desprotegido"}
                  tone={agent?.grfProtected ? "good" : "danger"}
                />
                <ApiStatusBadge
                  label={agent?.lubEditingBlocked ? ".lub Bloqueado" : ".lub Liberado"}
                  tone={agent?.lubEditingBlocked ? "good" : "danger"}
                />
              </div>
            </section>
            <section className="panel">
              <div className="panel-header">
                <h3>Perfil e versao</h3>
              </div>
              <div className="detail-grid">
                <div>
                  <dt>Perfil ativo</dt>
                  <dd>{agent?.activeProfile ?? "n/a"}</dd>
                </div>
                <div>
                  <dt>Versao do agente</dt>
                  <dd><span className="mono-pill">{agent?.agentVersion ?? "n/a"}</span></dd>
                </div>
                <div>
                  <dt>DB Mode</dt>
                  <dd>{agent?.dbMode ?? "n/a"}</dd>
                </div>
                <div>
                  <dt>Cache existe</dt>
                  <dd>{agent?.cacheExists ? "Sim" : "Nao"}</dd>
                </div>
                <div>
                  <dt>Cache valido</dt>
                  <dd>{agent?.cacheMatchesFingerprint ? "Sim" : "Nao"}</dd>
                </div>
              </div>
            </section>
            <section className="panel">
              <div className="panel-header">
                <h3>Politica de seguranca</h3>
              </div>
              {agent?.safety ? (
                <>
                  <div className="notice notice--info">
                    Esta integracao e estritamente read-only. Apply continua bloqueado e rollback real continua bloqueado.
                  </div>
                  <div className="token-wrap">
                    {Object.entries(agent.safety).map(([key, value]) => (
                      <ApiStatusBadge
                        key={key}
                        label={formatSafetyLabel(key)}
                        tone={value ? "good" : "danger"}
                      />
                    ))}
                  </div>
                </>
              ) : (
                <p className="muted-text">Carregando...</p>
              )}
            </section>
          </div>
        }
        primary={
          <div className="stack-lg">
            {/* Entity Index Summary */}
            <FieldGroup
              id="agent-index"
              title="Entidades indexadas"
              description="Resumo do cache de entidades do agente local (items, NPCs, monstros, mapas)."
            >
              {cacheWarnings.length > 0 ? (
                <div className="notice notice--warning">
                  Cache do Agent requer atencao: {cacheWarnings.join(" ")}
                </div>
              ) : null}
              {agent?.index ? (
                <div className="card-grid card-grid--compact">
                  <article className="stat-card">
                    <h3>Itens</h3>
                    <p>{agent.index.itemsFound.toLocaleString()}</p>
                  </article>
                  <article className="stat-card">
                    <h3>Monstros</h3>
                    <p>{agent.index.monstersFound.toLocaleString()}</p>
                  </article>
                  <article className="stat-card">
                    <h3>NPCs</h3>
                    <p>{agent.index.npcsFound.toLocaleString()}</p>
                  </article>
                  <article className="stat-card">
                    <h3>Mapas</h3>
                    <p>{agent.index.mapsFound.toLocaleString()}</p>
                  </article>
                  <article className="stat-card">
                    <h3>Arquivos escaneados</h3>
                    <p>{agent.index.filesScanned.toLocaleString()}</p>
                  </article>
                  <article className="stat-card">
                    <h3>Arquivos parseados</h3>
                    <p>{agent.index.filesParsed.toLocaleString()}</p>
                  </article>
                  <article className="stat-card">
                    <h3>Duracao</h3>
                    <p>{agent.index.durationMs}ms</p>
                  </article>
                  <article className="stat-card">
                    <h3>Indexado em</h3>
                    <p>{agent.index.generatedAtUtc ? new Date(agent.index.generatedAtUtc).toLocaleString() : "n/a"}</p>
                  </article>
                </div>
              ) : (
                <div className="notice notice--info">
                  Indice de entidades nao disponivel. Execute <code>ragnaforge index --entities --json</code> no agente local.
                </div>
              )}
            </FieldGroup>

            {/* Validation Summary */}
            <FieldGroup
              id="agent-validate"
              title="Validacao"
              description="Resumo dos issues encontrados pela validacao read-only do agente."
            >
              {agent?.validation ? (
                <>
                  <div className="card-grid card-grid--compact">
                    <article className="stat-card">
                      <h3>Total issues</h3>
                      <p>{agent.validation.totalIssues}</p>
                    </article>
                    <article className="stat-card">
                      <h3>Erros</h3>
                      <p>{agent.validation.errorCount}</p>
                    </article>
                    <article className="stat-card">
                      <h3>Warnings</h3>
                      <p>{agent.validation.warningCount}</p>
                    </article>
                  </div>
                  {agent.validation.topCategories.length > 0 ? (
                    <table className="data-table" style={{ marginTop: "1rem" }}>
                      <thead>
                        <tr>
                          <th>Codigo</th>
                          <th>Quantidade</th>
                        </tr>
                      </thead>
                      <tbody>
                        {agent.validation.topCategories.map((cat) => (
                          <tr key={cat.code}>
                            <td><code>{cat.code}</code></td>
                            <td>{cat.count}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  ) : null}
                </>
              ) : (
                <div className="notice notice--info">
                  Validacao nao executada neste ciclo.
                </div>
              )}
            </FieldGroup>

            {/* Doctor Checks */}
            <FieldGroup
              id="agent-doctor"
              title="Doctor checks"
              description={`${agent?.doctor.totalChecks ?? 0} checks executados pelo agente local.`}
            >
              {agent?.doctor ? (
                <>
                  <div className="card-grid card-grid--compact">
                    <article className="stat-card">
                      <h3>Passaram</h3>
                      <p>{agent.doctor.passed}</p>
                    </article>
                    <article className="stat-card">
                      <h3>Warnings</h3>
                      <p>{agent.doctor.warnings}</p>
                    </article>
                    <article className="stat-card">
                      <h3>Erros</h3>
                      <p>{agent.doctor.errors}</p>
                    </article>
                  </div>
                  {agent.doctor.failedChecks.length > 0 ? (
                    <table className="data-table" style={{ marginTop: "1rem" }}>
                      <thead>
                        <tr>
                          <th>Check</th>
                          <th>Severidade</th>
                          <th>Mensagem</th>
                        </tr>
                      </thead>
                      <tbody>
                        {agent.doctor.failedChecks.map((check) => (
                          <tr key={check.check}>
                            <td><code>{check.check}</code></td>
                            <td>
                              <ApiStatusBadge
                                label={check.severity}
                                tone={check.severity === "warning" ? "warning" : "danger"}
                              />
                            </td>
                            <td>{check.message}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  ) : (
                    <div className="notice notice--good">Todos os checks passaram com sucesso.</div>
                  )}
                </>
              ) : (
                <p className="muted-text">Carregando...</p>
              )}
            </FieldGroup>

            {/* Scan Summary */}
            {agent?.scan ? (
              <FieldGroup
                id="agent-scan"
                title="Scan do projeto"
                description="Resumo do ultimo scan read-only do projeto principal."
              >
                <div className="card-grid card-grid--compact">
                  <article className="stat-card">
                    <h3>Arquivos visitados</h3>
                    <p>{agent.scan.filesVisited}</p>
                  </article>
                  <article className="stat-card">
                    <h3>Arquivos indexados</h3>
                    <p>{agent.scan.filesIndexed}</p>
                  </article>
                  <article className="stat-card">
                    <h3>Diretorios</h3>
                    <p>{agent.scan.directoriesVisited}</p>
                  </article>
                  <article className="stat-card">
                    <h3>Duracao</h3>
                    <p>{agent.scan.durationMs}ms</p>
                  </article>
                </div>
              </FieldGroup>
            ) : null}
          </div>
        }
        inspector={
          <div className="stack-lg">
            {(agent?.warnings?.length ?? 0) > 0 || (agent?.errors?.length ?? 0) > 0 ? (
              <FieldGroup title="Warnings e Erros do Agente">
                {agent!.errors.map((e, i) => (
                  <div key={`err-${i}`} className="notice notice--danger">{e}</div>
                ))}
                {agent!.warnings.map((w, i) => (
                  <div key={`warn-${i}`} className="notice notice--warning">{w}</div>
                ))}
              </FieldGroup>
            ) : null}
            <ResponseMeta response={agentQuery.data ?? null} />
            <JsonInspector title="Agent Health (raw)" value={agent} />
            <ProblemDetailsView problem={problem} />
          </div>
        }
      />
    </div>
  );
}
