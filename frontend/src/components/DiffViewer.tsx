import type { DiffPreview } from "../api/types";

function classifyLine(line: string) {
  if (line.startsWith("+++ ") || line.startsWith("--- ") || line.startsWith("@@")) {
    return "line-meta";
  }
  if (line.startsWith("+")) {
    return "line-add";
  }
  if (line.startsWith("-")) {
    return "line-remove";
  }
  return "line-context";
}

export function DiffViewer({ diff }: { diff?: DiffPreview | null }) {
  if (!diff?.entries?.length) {
    return (
      <section className="panel">
        <h3>Diff preview</h3>
        <p>Nenhum hunk disponível ainda.</p>
      </section>
    );
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <h3>Diff preview</h3>
        <span className="mono-pill">{diff.fileCount} arquivo(s)</span>
      </div>
      <div className="diff-list">
        {diff.entries.map((entry) => (
          <article key={`${entry.targetPath}-${entry.changeKind}`} className="diff-card">
            <header className="diff-header">
              <div>
                <strong>{entry.changeKind}</strong>
                <p className="mono-text">{entry.targetPath}</p>
              </div>
              <span className={`badge ${entry.exists ? "badge-neutral" : "badge-good"}`}>
                {entry.exists ? "existing" : "new"}
              </span>
            </header>
            <pre className="diff-block">
              {(entry.unifiedDiff ?? entry.preview ?? "").split("\n").map((line, index) => (
                <div key={`${entry.targetPath}-${index}`} className={classifyLine(line)}>
                  {line || " "}
                </div>
              ))}
            </pre>
          </article>
        ))}
      </div>
    </section>
  );
}
