import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { ApiClientError } from "../api/client";
import { ApiStatusBadge } from "../components/ApiStatusBadge";
import { DependencyTree } from "../components/DependencyTree";
import { DiffWorkbench } from "../components/DiffWorkbench";
import { EntityGrid } from "../components/EntityGrid";
import { FieldGroup } from "../components/FieldGroup";
import { JsonInspector } from "../components/JsonInspector";
import { PageHeader } from "../components/PageHeader";
import { PassiveAssetPreviewPanel } from "../components/PassiveAssetPreviewPanel";
import { PipelineWorkspaceLayout } from "../components/PipelineWorkspaceLayout";
import { ProblemDetailsView } from "../components/ProblemDetailsView";
import { ReadinessRibbon } from "../components/ReadinessRibbon";
import { ResponseMeta } from "../components/ResponseMeta";
import { ValidationIssueRow } from "../components/ValidationIssueTable";
import { ValidationMatrix } from "../components/ValidationMatrix";
import { useApiConfig } from "../features/connection/ApiConfigContext";
import { listPipelineHistory, type PipelineCategory } from "../features/shared/localHistory";
import { buildIssueCategorySummary, collectValidationCenterData, buildResourceValidationBadges } from "../features/shared/resourceValidation";
import { defaultConfigPath } from "../features/shared/requestBuilders";
import { ReportListPanel } from "../components/ReportListPanel";

export function ValidationPage() {
  const { client, connection } = useApiConfig();
  const [historyCategory, setHistoryCategory] = useState<PipelineCategory | "all">("all");
  const [historyRevision, setHistoryRevision] = useState(0);

  const statusQuery = useQuery({
    queryKey: ["status-validation", connection.baseUrl, connection.apiKey],
    queryFn: () => client.status(),
  });

  const configQuery = useQuery({
    queryKey: ["config-validation-center", connection.baseUrl, connection.apiKey, defaultConfigPath],
    queryFn: () => client.validateConfig({ configPath: defaultConfigPath }),
  });

  const discoverQuery = useQuery({
    queryKey: ["discover-validation-center", connection.baseUrl, connection.apiKey, defaultConfigPath],
    queryFn: () =>
      client.discover({
        configPath: defaultConfigPath,
        maxGrfContainers: 10,
        saveCache: true,
      }),
  });

  const liveIssues: ValidationIssueRow[] = [
    ...(configQuery.data?.data.validation.issues ?? []).map((issue, index) => ({
      key: `config-${index}`,
      severity:
        issue.severity.toLowerCase() === "error"
          ? ("danger" as const)
          : ("warning" as const),
      category: "Server DB",
      entity: "Configuracao",
      file: configQuery.data?.data.manifestPath ?? defaultConfigPath,
      origin: issue.code,
      message: issue.message,
      recommendedAction: "Revisar manifest e confirmar os caminhos configurados.",
      tags: ["blocked"],
      blocksFutureApply: true,
      raw: issue,
    })),
    ...(statusQuery.data?.data.disabledWriteOperations ?? []).map((operation, index) => ({
      key: `guard-${index}`,
      severity: "info" as const,
      category: "API",
      entity: "API",
      file: "/api/*",
      origin: "SafetyPolicy",
      message: `${operation} bloqueado nesta fase.`,
      recommendedAction: "Continuar usando apenas read-only, dry-run e diff-preview.",
      tags: ["blocked"],
      blocksFutureApply: true,
      raw: { operation },
    })),
  ];

  const historyEntries = useMemo(
    () => listPipelineHistory(historyCategory === "all" ? undefined : historyCategory),
    [historyCategory, historyRevision],
  );
  const derived = useMemo(() => collectValidationCenterData(historyEntries), [historyEntries]);
  const issues = useMemo(
    () => [...liveIssues, ...derived.issues],
    [liveIssues, derived.issues],
  );
  const categories = useMemo(() => buildIssueCategorySummary(issues), [issues]);
  const resourceBadges = useMemo(() => buildResourceValidationBadges(issues, derived.previewItems), [issues, derived.previewItems]);
  const resourceBadgeCategories = useMemo(() => Array.from(new Set(resourceBadges.map(b => b.category))), [resourceBadges]);
  const reportCards = useMemo(
    () =>
      historyEntries.slice(0, 8).map((entry) => ({
        key: entry.id,
        title: entry.summary,
        description: `${entry.kind} | readiness ${entry.readiness ?? "-"} | cid ${entry.correlationId ?? "-"}`,
        status: entry.success ? "Success" : "Failure",
      })),
    [historyEntries],
  );

  const liveWarnings = [
    ...(statusQuery.data?.warnings ?? []),
    ...(configQuery.data?.warnings ?? []),
    ...(discoverQuery.data?.warnings ?? []),
  ];
  const liveErrors = [
    ...(statusQuery.data?.errors ?? []),
    ...(configQuery.data?.errors ?? []),
    ...(discoverQuery.data?.errors ?? []),
  ];
  const lastProblem =
    (statusQuery.error as ApiClientError | null)?.problem ??
    (configQuery.error as ApiClientError | null)?.problem ??
    (discoverQuery.error as ApiClientError | null)?.problem;

  return (
    <section className="page">
      <PageHeader
        title="Validacao"
        description="Centro read-only de validacao inspirado no SDE, reunindo issues vivas da API segura, filtros e familias de recursos."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <div className="stack-lg">
            <EntityGrid
              title="Categorias"
              description="Resumo agregado do historico local, dos guards da API e das familias de recursos."
              items={categories.map((category) => ({
                ...category,
                href: "#validation-center",
              }))}
              activeKey={categories[0]?.key}
            />
            <section className="panel">
              <div className="panel-header">
                <h3>Historico considerado</h3>
              </div>
              <div className="form-grid">
                <label>
                  <span>Categoria</span>
                  <select value={historyCategory} onChange={(event) => setHistoryCategory(event.target.value as PipelineCategory | "all")}>
                    <option value="all">Todas</option>
                    <option value="items">Itens</option>
                    <option value="equipment">Equipamentos</option>
                    <option value="npcs">NPCs</option>
                    <option value="monsters">Monstros</option>
                    <option value="maps">Mapas</option>
                  </select>
                </label>
                <label>
                  <span>Total de entradas</span>
                  <input value={String(historyEntries.length)} readOnly />
                </label>
              </div>
              <div className="form-actions">
                <button type="button" className="button-secondary" onClick={() => setHistoryRevision((current) => current + 1)}>
                  Atualizar do historico
                </button>
              </div>
            </section>
          </div>
        }
        primary={
          <div className="stack-lg">
            <FieldGroup
              title="Resumo da validacao"
              description="Painel central das categorias vivas hoje, com foco em recursos, client-side e bloqueios futuros."
            >
              <div className="badge-grid">
                {categories.map((category) => (
                  <ApiStatusBadge key={category.key} label={category.label} tone={category.status} />
                ))}
              </div>
            </FieldGroup>
            <FieldGroup
              title="Sem repair automatico"
              description="Esta aba informa o problema e a acao recomendada, mas nao executa reparo nem escrita."
            >
              <div className="notice notice--warning">
                Nenhuma acao de repair, apply ou rollback existe aqui. O objetivo e consolidar riscos e orientar a analise.
              </div>
            </FieldGroup>
            <FieldGroup
              title="Validacao de recursos inspirada no RagnarokSDE"
              description="Classificacao dinamica de recursos com base nos dry-runs locais: missing, ambiguous, blocked e needs-copy-future."
            >
              <div className="stack-lg">
                {resourceBadgeCategories.length > 0 ? (
                  resourceBadgeCategories.map(category => (
                    <div key={category}>
                      <h4>{category}</h4>
                      <div className="badge-grid" style={{ marginTop: "0.5rem" }}>
                        {resourceBadges.filter(b => b.category === category).map(badge => (
                          <ApiStatusBadge key={badge.key} label={badge.label} tone={badge.tone} />
                        ))}
                      </div>
                    </div>
                  ))
                ) : (
                  <p className="muted-text">Nenhum recurso especifico identificado nos dados atuais.</p>
                )}
              </div>
            </FieldGroup>
            <FieldGroup
              title="Ultimos resultados considerados"
              description="A validacao central usa o historico local de dry-runs e diff-previews sem reenviar nada automaticamente."
            >
              <ReportListPanel title="Entradas recentes" items={reportCards.length ? reportCards : [
                {
                  key: "no-history",
                  title: "Nenhum historico local ainda",
                  description: "Rode dry-runs/diff-previews nas abas de pipeline para alimentar este centro.",
                  status: "Placeholder",
                },
              ]} />
            </FieldGroup>
          </div>
        }
        inspector={
          <div className="stack-lg">
            <ReadinessRibbon
              readiness="Validation center"
              canApply={false}
              warningsCount={liveWarnings.length}
              errorsCount={liveErrors.length}
              correlationId={statusQuery.data?.correlationId ?? configQuery.data?.correlationId}
              modeLabel={statusQuery.data?.data.mode}
            />
            <DiffWorkbench
              title="Validation center"
              tabs={[
                {
                  key: "issues",
                  label: "Issues",
                  content: (
                    <div id="validation-center" className="stack-lg">
                      <ProblemDetailsView problem={lastProblem} />
                      <ResponseMeta response={statusQuery.data ?? configQuery.data ?? null} />
                      <ValidationMatrix
                        warnings={liveWarnings}
                        errors={liveErrors}
                        blockReasons={statusQuery.data?.data.disabledWriteOperations ?? []}
                        issues={issues}
                      />
                    </div>
                  ),
                },
                {
                  key: "resources",
                  label: "Resources",
                  content: (
                    <div className="stack-lg">
                      <PassiveAssetPreviewPanel
                        title="Preview passivo consolidado"
                        items={derived.previewItems}
                      />
                      <DependencyTree
                        title="Arvore consolidada de recursos"
                        groups={derived.dependencyGroups}
                      />
                    </div>
                  ),
                },
                {
                  key: "json",
                  label: "JSON",
                  content: (
                    <div className="stack-lg">
                      <JsonInspector title="Status bruto" value={statusQuery.data?.data} />
                      <JsonInspector title="Config validation" value={configQuery.data?.data} />
                      <JsonInspector title="Discovery bruto" value={discoverQuery.data?.data} />
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
