import type { PipelineReport } from "../api/types";

export function MapCachePlanPanel({ plan }: { plan?: PipelineReport["mapCachePlan"] }) {
  if (!plan) {
    return (
      <section className="panel">
        <div className="panel-header">
          <h3>Map cache</h3>
        </div>
        <p className="muted-text">Nenhum plano de map cache retornado.</p>
      </section>
    );
  }

  const record = plan as Record<string, unknown>;
  const commands = Array.isArray(record.commands) ? record.commands : [];
  const notes = Array.isArray(record.notes) ? record.notes : [];

  return (
    <section className="panel">
      <div className="panel-header">
        <h3>Map cache</h3>
      </div>
      <dl className="detail-grid">
        <div>
          <dt>ToolDetected</dt>
          <dd>{String(record.toolDetected ?? false)}</dd>
        </div>
        <div>
          <dt>ExistingCacheDetected</dt>
          <dd>{String(record.existingCacheDetected ?? false)}</dd>
        </div>
        <div>
          <dt>ToolPath</dt>
          <dd className="mono-text">{String(record.toolPath ?? "-")}</dd>
        </div>
        <div>
          <dt>CachePath</dt>
          <dd className="mono-text">{String(record.cachePath ?? "-")}</dd>
        </div>
      </dl>
      {commands.length ? (
        <div className="stack-sm">
          <h4>Commands</h4>
          <ul className="flat-list">
            {commands.map((command, index) => (
              <li key={`command-${index}`} className="mono-text">
                {String(command)}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
      {notes.length ? (
        <div className="stack-sm">
          <h4>Notes</h4>
          <ul className="flat-list">
            {notes.map((note, index) => (
              <li key={`note-${index}`}>{String(note)}</li>
            ))}
          </ul>
        </div>
      ) : null}
    </section>
  );
}
