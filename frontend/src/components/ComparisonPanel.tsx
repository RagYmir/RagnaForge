import type { PipelineComparison } from "../features/shared/localHistory";
import { JsonInspector } from "./JsonInspector";

export function ComparisonPanel({
  comparison,
}: {
  comparison: PipelineComparison;
}) {
  if (!comparison.left || !comparison.right) {
    return (
      <section className="panel">
        <div className="panel-header">
          <h3>Comparacao entre dry-runs</h3>
        </div>
        <p className="muted-text">Selecione dois resultados do historico para comparar.</p>
      </section>
    );
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <h3>Comparacao entre dry-runs</h3>
      </div>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>Campo</th>
              <th>Esquerda</th>
              <th>Direita</th>
              <th>Mudou</th>
            </tr>
          </thead>
          <tbody>
            {comparison.rows.map((row) => (
              <tr key={row.key}>
                <td>{row.label}</td>
                <td>{row.left}</td>
                <td>{row.right}</td>
                <td>
                  <span className={`mono-pill ${row.changed ? "validation-pill validation-pill--warning" : ""}`}>
                    {row.changed ? "Sim" : "Nao"}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="content-grid content-grid--two">
        <JsonInspector title="JSON esquerdo" value={comparison.left.responseData} />
        <JsonInspector title="JSON direito" value={comparison.right.responseData} />
      </div>
    </section>
  );
}
