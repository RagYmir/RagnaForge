export interface ValidationIssueRow {
  key: string;
  severity: "info" | "warning" | "danger";
  category?: string;
  entity: string;
  file: string;
  origin: string;
  message: string;
  recommendedAction: string;
  tags?: string[];
  blocksFutureApply?: boolean;
  raw?: unknown;
}

function getStringList(raw: unknown, property: string): string[] {
  if (!raw || typeof raw !== "object" || !(property in raw)) {
    return [];
  }

  const value = (raw as Record<string, unknown>)[property];
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === "string") : [];
}

export function ValidationIssueTable({ rows }: { rows: ValidationIssueRow[] }) {
  return (
    <section className="panel">
      <div className="panel-header">
        <h3>Validation issues</h3>
      </div>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>Severity</th>
              <th>Category</th>
              <th>Entity</th>
              <th>File</th>
              <th>Origin</th>
              <th>Message</th>
              <th>Recommended action</th>
              <th>Blocks future apply?</th>
              <th>Raw</th>
            </tr>
          </thead>
          <tbody>
            {rows.length ? (
              rows.map((row) => {
                const knowledgeHints = getStringList(row.raw, "knowledgeHints");
                const recommendedKnowledgeEntryIds = getStringList(
                  row.raw,
                  "recommendedKnowledgeEntryIds",
                );

                return (
                  <tr key={row.key}>
                    <td>
                      <span className={`mono-pill validation-pill validation-pill--${row.severity}`}>
                        {row.severity}
                      </span>
                    </td>
                    <td>{row.category ?? "-"}</td>
                    <td>{row.entity}</td>
                    <td className="mono-text">{row.file}</td>
                    <td>{row.origin}</td>
                    <td>{row.message}</td>
                    <td>
                      <div>{row.recommendedAction}</div>
                      {knowledgeHints.length || recommendedKnowledgeEntryIds.length ? (
                        <div className="knowledge-hints-container">
                          {knowledgeHints.map((hint, i) => (
                            <div key={`hint-${i}`} className="knowledge-hint">
                              Hint: {hint}
                            </div>
                          ))}
                          {recommendedKnowledgeEntryIds.map((entryId, i) => (
                            <div key={`entry-${i}`} className="knowledge-entry-pill">
                              Entry: {entryId}
                            </div>
                          ))}
                        </div>
                      ) : null}
                    </td>
                    <td>
                      <span
                        className={`mono-pill validation-pill validation-pill--${
                          row.blocksFutureApply ? "danger" : "info"
                        }`}
                      >
                        {row.blocksFutureApply ? "yes" : "no"}
                      </span>
                    </td>
                    <td>
                      {row.raw !== undefined ? (
                        <details className="details-block">
                          <summary>Raw</summary>
                          <pre className="raw-json">{JSON.stringify(row.raw, null, 2)}</pre>
                        </details>
                      ) : (
                        <span className="muted-text">-</span>
                      )}
                    </td>
                  </tr>
                );
              })
            ) : (
              <tr>
                <td colSpan={9} className="muted-text">
                  Nenhum problema agregado nesta fase.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}
