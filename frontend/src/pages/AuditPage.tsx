import { useQuery } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { ApiStatusBadge } from "../components/ApiStatusBadge";
import { ComparisonPanel } from "../components/ComparisonPanel";
import { DiffWorkbench } from "../components/DiffWorkbench";
import { EntityGrid } from "../components/EntityGrid";
import { FieldGroup } from "../components/FieldGroup";
import { JsonInspector } from "../components/JsonInspector";
import { PageHeader } from "../components/PageHeader";
import { PipelineWorkspaceLayout } from "../components/PipelineWorkspaceLayout";
import { ReadinessRibbon } from "../components/ReadinessRibbon";
import { ReportListPanel } from "../components/ReportListPanel";
import { useApiConfig } from "../features/connection/ApiConfigContext";
import {
  exportComparisonJson,
  exportComparisonMarkdown,
  exportHistoryEntryJson,
  exportHistoryEntryMarkdown,
} from "../features/shared/exporters";
import {
  clearPipelineHistory,
  listPipelineHistory,
  summarizeHistoryForComparison,
  type PipelineCategory,
} from "../features/shared/localHistory";

const documents = [
  {
    key: "pre-production-audit",
    title: "Auditoria pre-producao",
    description: "Relatorio tecnico da readiness dos pipelines antes da API/interface.",
    status: "Documento",
  },
  {
    key: "ragnaroksde-analysis",
    title: "Analise RagnarokSDE",
    description: "Referencia conceitual e visual usada nas passadas de UX da Admin UI.",
    status: "Documento",
  },
  {
    key: "ui-visual-pass-1",
    title: "Admin UI visual pass v1",
    description: "Primeira rodada visual aplicada em Dashboard, Itens, Equipamentos e Seguranca/API.",
    status: "Documento",
  },
  {
    key: "ui-visual-pass-2",
    title: "Admin UI visual pass v2",
    description: "Expansao do workspace visual para NPCs, Monstros, Mapas, GRF, Validacao e Relatorios.",
    status: "Documento",
  },
];

const categories: PipelineCategory[] = ["items", "equipment", "npcs", "monsters", "maps"];

export function AuditPage() {
  const { client, connection } = useApiConfig();
  const [historyRevision, setHistoryRevision] = useState(0);
  const [selectedCategory, setSelectedCategory] = useState<PipelineCategory>("items");
  const [leftId, setLeftId] = useState("");
  const [rightId, setRightId] = useState("");

  const statusQuery = useQuery({
    queryKey: ["status-audit", connection.baseUrl, connection.apiKey],
    queryFn: () => client.status(),
  });

  const allHistory = useMemo(() => listPipelineHistory(), [historyRevision]);
  const filteredHistory = useMemo(
    () => listPipelineHistory(selectedCategory),
    [historyRevision, selectedCategory],
  );

  useEffect(() => {
    setLeftId(filteredHistory[0]?.id ?? "");
    setRightId(filteredHistory[1]?.id ?? filteredHistory[0]?.id ?? "");
  }, [selectedCategory, filteredHistory]);

  const leftEntry = filteredHistory.find((entry) => entry.id === leftId);
  const rightEntry = filteredHistory.find((entry) => entry.id === rightId);
  const comparison = summarizeHistoryForComparison(leftEntry, rightEntry);

  const readinessRows =
    statusQuery.data?.data.capabilities.map((capability) => ({
      key: capability.category,
      label: capability.category,
      subtitle: `ReadOnly=${String(capability.readOnly)} DryRun=${String(capability.dryRun)} Diff=${String(capability.diffPreview)}`,
      status: capability.diffPreview ? ("good" as const) : ("warning" as const),
      href: "#audit-readiness",
    })) ?? [];

  const historyCounts = categories.map((category) => ({
    key: category,
    label: category,
    subtitle: `${listPipelineHistory(category).length} entrada(s)`,
    status: listPipelineHistory(category).length ? ("good" as const) : ("neutral" as const),
    href: "#audit-history",
  }));

  return (
    <section className="page">
      <PageHeader
        title="Historico e relatorios"
        description="Area read-only para documentos, readiness matrix, historico local e comparacao simples entre dry-runs."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <div className="stack-lg">
            <EntityGrid
              title="Relatorios"
              description="Indice do material tecnico e do readiness atual."
              items={readinessRows.length ? readinessRows : historyCounts}
              activeKey={selectedCategory}
            />
            <section className="panel">
              <div className="panel-header">
                <h3>Historico local</h3>
              </div>
              <div className="form-grid">
                <label>
                  <span>Categoria</span>
                  <select value={selectedCategory} onChange={(event) => setSelectedCategory(event.target.value as PipelineCategory)}>
                    {categories.map((category) => (
                      <option key={category} value={category}>
                        {category}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>Total</span>
                  <input value={String(filteredHistory.length)} readOnly />
                </label>
              </div>
              <div className="form-actions">
                <button
                  type="button"
                  className="button-secondary"
                  onClick={() => {
                    clearPipelineHistory(selectedCategory);
                    setHistoryRevision((current) => current + 1);
                  }}
                >
                  Limpar categoria
                </button>
                <button
                  type="button"
                  className="button-secondary"
                  onClick={() => {
                    clearPipelineHistory();
                    setHistoryRevision((current) => current + 1);
                  }}
                >
                  Limpar tudo
                </button>
              </div>
            </section>
          </div>
        }
        primary={
          <div className="stack-lg">
            <FieldGroup
              title="Documentos importantes"
              description="O frontend ainda nao le o filesystem local, entao os documentos aparecem aqui como indice conceitual."
            >
              <ReportListPanel title="Documentos" items={documents} />
            </FieldGroup>
            <FieldGroup
              id="audit-history"
              title="Ultimos resultados locais"
              description="Resultados de dry-run e diff-preview salvos localmente no navegador."
            >
              <div className="stack-sm">
                {filteredHistory.length ? (
                  filteredHistory.slice(0, 8).map((entry) => (
                    <article key={entry.id} className="history-entry">
                      <div className="history-entry__meta">
                        <strong>{entry.summary}</strong>
                        <div className="token-wrap">
                          <span className="mono-pill">{entry.kind}</span>
                          <span className="mono-pill">{entry.readiness ?? "-"}</span>
                          <span className="mono-pill">cid {entry.correlationId ?? "-"}</span>
                        </div>
                      </div>
                      <div className="history-entry__actions">
                        <button type="button" className="button-secondary" onClick={() => exportHistoryEntryJson(entry)}>
                          Exportar JSON
                        </button>
                        <button type="button" className="button-secondary" onClick={() => exportHistoryEntryMarkdown(entry)}>
                          Exportar MD
                        </button>
                      </div>
                    </article>
                  ))
                ) : (
                  <div className="notice notice--info">
                    Nenhum historico local salvo para esta categoria ainda.
                  </div>
                )}
              </div>
            </FieldGroup>
          </div>
        }
        inspector={
          <div className="stack-lg">
            <ReadinessRibbon
              readiness="Read-only reports"
              canApply={false}
              warningsCount={0}
              errorsCount={0}
              correlationId={statusQuery.data?.correlationId}
              modeLabel={statusQuery.data?.data.mode}
            />
            <DiffWorkbench
              title="Readiness matrix"
              tabs={[
                {
                  key: "matrix",
                  label: "Matrix",
                  content: (
                    <div id="audit-readiness" className="stack-lg">
                      <div className="badge-grid">
                        {(statusQuery.data?.data.capabilities ?? []).map((capability) => (
                          <ApiStatusBadge
                            key={capability.category}
                            label={`${capability.category}: ${capability.diffPreview ? "DiffPreview" : "ReadOnly"}`}
                            tone={capability.diffPreview ? "good" : "warning"}
                          />
                        ))}
                      </div>
                    </div>
                  ),
                },
                {
                  key: "comparison",
                  label: "Comparacao",
                  content: (
                    <div className="stack-lg">
                      <div className="form-grid">
                        <label>
                          <span>Esquerda</span>
                          <select value={leftId} onChange={(event) => setLeftId(event.target.value)}>
                            {filteredHistory.map((entry) => (
                              <option key={entry.id} value={entry.id}>
                                {entry.summary}
                              </option>
                            ))}
                          </select>
                        </label>
                        <label>
                          <span>Direita</span>
                          <select value={rightId} onChange={(event) => setRightId(event.target.value)}>
                            {filteredHistory.map((entry) => (
                              <option key={entry.id} value={entry.id}>
                                {entry.summary}
                              </option>
                            ))}
                          </select>
                        </label>
                      </div>
                      <div className="button-row">
                        <button type="button" className="button-secondary" onClick={() => exportComparisonJson(comparison)}>
                          Exportar JSON
                        </button>
                        <button type="button" className="button-secondary" onClick={() => exportComparisonMarkdown(comparison)}>
                          Exportar MD
                        </button>
                      </div>
                      <ComparisonPanel comparison={comparison} />
                    </div>
                  ),
                },
                {
                  key: "json",
                  label: "JSON",
                  content: (
                    <div className="stack-lg">
                      <JsonInspector title="Status bruto" value={statusQuery.data?.data} />
                      <JsonInspector title="Historico local" value={allHistory} />
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
