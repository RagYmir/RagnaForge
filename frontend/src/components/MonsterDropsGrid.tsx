import type { PipelineReport } from "../api/types";

export function MonsterDropsGrid({ drops }: { drops?: PipelineReport["drops"] }) {
  if (!drops?.length) {
    return (
      <section className="panel">
        <div className="panel-header">
          <h3>Drops</h3>
        </div>
        <p className="muted-text">Nenhum drop planejado.</p>
      </section>
    );
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <h3>Drops</h3>
      </div>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>Item</th>
              <th>ID</th>
              <th>Chance</th>
              <th>Qty</th>
              <th>MVP</th>
              <th>Source</th>
            </tr>
          </thead>
          <tbody>
            {drops.map((drop, index) => (
              <tr key={`drop-${index}`}>
                <td>{String(drop.itemReference ?? drop.resolvedAegisName ?? "-")}</td>
                <td>{String(drop.resolvedItemId ?? "-")}</td>
                <td>{String(drop.chance ?? "-")}</td>
                <td>{String(drop.quantity ?? "-")}</td>
                <td>{String(drop.isMvp ?? false)}</td>
                <td>{String(drop.source ?? "-")}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
