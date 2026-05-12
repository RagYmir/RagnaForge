export interface PassiveAssetPreviewItem {
  key: string;
  name?: string;
  path: string;
  expectedPath?: string;
  type?: string;
  category?: string;
  origin?: string;
  provenance?: string;
  status: "resolved" | "missing" | "ambiguous" | "blocked" | "read-only" | "needs-copy-future";
  note?: string;
}

export function PassiveAssetPreviewPanel({
  title,
  items,
}: {
  title: string;
  items: PassiveAssetPreviewItem[];
}) {
  if (!items.length) {
    return null;
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <h3>{title}</h3>
          <p className="muted-text">
            Preview passivo: apenas classificacao, path esperado e proveniencia. Preview visual real fica para uma etapa futura.
          </p>
        </div>
      </div>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>Asset</th>
              <th>Category / Type</th>
              <th>Origin / Provenance</th>
              <th>Preview</th>
              <th>Status</th>
              <th>Note</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.key}>
                <td>
                  <div className="asset-preview-cell">
                    <strong>{item.name ?? item.path.split(/[\\/]/).pop() ?? item.path}</strong>
                    <div className="mono-text">{item.path}</div>
                    {item.expectedPath && item.expectedPath !== item.path ? (
                      <div className="muted-text">Esperado: {item.expectedPath}</div>
                    ) : null}
                  </div>
                </td>
                <td>
                  <div>{item.category ?? "-"}</div>
                  <div className="muted-text">{item.type ?? "-"}</div>
                </td>
                <td>
                  <div>{item.origin ?? "-"}</div>
                  <div className="muted-text">{item.provenance ?? "-"}</div>
                </td>
                <td>
                  <span className="asset-preview-placeholder">
                    Preview visual real pendente de endpoint seguro de leitura.
                  </span>
                </td>
                <td>
                  <span className={`mono-pill dependency-status dependency-status--${item.status}`}>
                    {item.status}
                  </span>
                </td>
                <td>{item.note ?? "-"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
