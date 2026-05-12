export interface ReportListItem {
  key: string;
  title: string;
  description: string;
  status?: string;
  href?: string;
}

export function ReportListPanel({
  title,
  items,
}: {
  title: string;
  items: ReportListItem[];
}) {
  return (
    <section className="panel">
      <div className="panel-header">
        <h3>{title}</h3>
      </div>
      <div className="report-list">
        {items.map((item) => (
          <article key={item.key} className="report-list__item">
            <div className="report-list__body">
              <h4>{item.title}</h4>
              <p className="muted-text">{item.description}</p>
            </div>
            <div className="report-list__meta">
              {item.status ? <span className="mono-pill">{item.status}</span> : null}
              {item.href ? (
                <a href={item.href} target="_blank" rel="noreferrer">
                  Abrir
                </a>
              ) : (
                <span className="muted-text">Sem endpoint ainda</span>
              )}
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
