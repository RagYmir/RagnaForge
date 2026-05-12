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
              rows.map((row) => (
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
                  <td>{row.recommendedAction}</td>
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
              ))
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
