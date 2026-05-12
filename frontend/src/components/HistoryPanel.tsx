import type { PipelineHistoryEntry } from "../features/shared/localHistory";

export function HistoryPanel({
  title,
  entries,
  onApplyPayload,
  onClear,
  onExportJson,
  onExportMarkdown,
}: {
  title: string;
  entries: PipelineHistoryEntry[];
  onApplyPayload: (entry: PipelineHistoryEntry) => void;
  onClear: () => void;
  onExportJson: (entry: PipelineHistoryEntry) => void;
  onExportMarkdown: (entry: PipelineHistoryEntry) => void;
}) {
  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <h3>{title}</h3>
          <p className="muted-text">
            Historico local do navegador. Nao salva API key nem executa chamadas automaticamente.
          </p>
        </div>
        <button type="button" className="button-secondary" onClick={onClear}>
          Limpar historico
        </button>
      </div>
      {entries.length ? (
        <div className="stack-sm">
          {entries.map((entry) => (
            <article key={entry.id} className="history-entry">
              <div className="history-entry__meta">
                <strong>{entry.summary}</strong>
                <div className="token-wrap">
                  <span className="mono-pill">{entry.kind}</span>
                  <span className="mono-pill">{entry.readiness ?? "-"}</span>
                  <span className="mono-pill">Warnings {entry.warningsCount}</span>
                  <span className="mono-pill">Errors {entry.errorsCount}</span>
                </div>
              </div>
              <div className="history-entry__actions">
                <button type="button" onClick={() => onApplyPayload(entry)}>
                  Reenviar ao formulario
                </button>
                <button type="button" className="button-secondary" onClick={() => onExportJson(entry)}>
                  Exportar JSON
                </button>
                <button type="button" className="button-secondary" onClick={() => onExportMarkdown(entry)}>
                  Exportar MD
                </button>
              </div>
            </article>
          ))}
        </div>
      ) : (
        <p className="muted-text">Nenhum historico local registrado para esta categoria.</p>
      )}
    </section>
  );
}
