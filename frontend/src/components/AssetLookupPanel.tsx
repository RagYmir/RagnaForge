import type { AssetLookupReport } from "../api/types";
import { WarningList } from "./WarningList";

export function AssetLookupPanel({ title, report }: { title: string; report?: AssetLookupReport }) {
  if (!report) {
    return null;
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <h3>{title}</h3>
      </div>
      <dl className="detail-grid compact">
        <div>
          <dt>Source</dt>
          <dd>{String(report.source ?? "-")}</dd>
        </div>
        <div>
          <dt>Total matches</dt>
          <dd>{String(report.totalMatches ?? report.matches?.length ?? 0)}</dd>
        </div>
        <div>
          <dt>Local indexes</dt>
          <dd>{String(report.localIndexesLoaded ?? 0)}</dd>
        </div>
        <div>
          <dt>Live scans</dt>
          <dd>{String(report.liveContainersScanned ?? 0)}</dd>
        </div>
      </dl>
      <WarningList warnings={report.warnings ?? []} />
      {report.matches?.length ? (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Container</th>
                <th>Path</th>
                <th>Extension</th>
              </tr>
            </thead>
            <tbody>
              {report.matches.slice(0, 10).map((match, index) => (
                <tr key={`${match.containerPath ?? "match"}-${index}`}>
                  <td className="mono-text">{match.containerPath ?? "-"}</td>
                  <td className="mono-text">{match.relativePath ?? "-"}</td>
                  <td>{match.extension ?? "-"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="muted-text">Nenhum asset correspondente foi retornado.</p>
      )}
    </section>
  );
}
