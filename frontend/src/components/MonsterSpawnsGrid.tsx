import type { PipelineReport } from "../api/types";

export function MonsterSpawnsGrid({ spawns }: { spawns?: PipelineReport["spawns"] }) {
  if (!spawns?.length) {
    return (
      <section className="panel">
        <div className="panel-header">
          <h3>Spawns</h3>
        </div>
        <p className="muted-text">Nenhum spawn planejado.</p>
      </section>
    );
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <h3>Spawns</h3>
      </div>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>Map</th>
              <th>Coords</th>
              <th>Area</th>
              <th>Amount</th>
              <th>Respawn</th>
              <th>Label</th>
            </tr>
          </thead>
          <tbody>
            {spawns.map((spawn, index) => (
              <tr key={`spawn-${index}`}>
                <td>{String(spawn.mapName ?? "-")}</td>
                <td>
                  {String(spawn.x ?? "-")}, {String(spawn.y ?? "-")}
                </td>
                <td>
                  {String(spawn.areaX ?? "-")} x {String(spawn.areaY ?? "-")}
                </td>
                <td>{String(spawn.amount ?? "-")}</td>
                <td>{String(spawn.respawnMilliseconds ?? "-")}</td>
                <td>{String(spawn.label ?? spawn.eventLabel ?? "-")}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
