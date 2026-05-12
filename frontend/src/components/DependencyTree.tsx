export type DependencyTreeStatus =
  | "resolved"
  | "missing"
  | "ambiguous"
  | "blocked"
  | "read-only"
  | "needs-copy-future";

export interface DependencyTreeItem {
  label: string;
  hint?: string;
  status?: DependencyTreeStatus;
  origin?: string;
  note?: string;
}

interface DependencyTreeGroup {
  title: string;
  items: Array<string | DependencyTreeItem>;
}

interface DependencyTreeProps {
  title: string;
  groups: DependencyTreeGroup[];
}

export function DependencyTree({ title, groups }: DependencyTreeProps) {
  const visibleGroups = groups.filter((group) => group.items.length > 0);

  if (!visibleGroups.length) {
    return null;
  }

  return (
    <section className="panel dependency-tree">
      <div className="panel-header">
        <h3>{title}</h3>
      </div>
      <div className="dependency-tree__groups">
        {visibleGroups.map((group) => (
          <article key={group.title} className="dependency-tree__group">
            <h4>{group.title}</h4>
            <ul className="flat-list">
              {group.items.map((item) => {
                const label = typeof item === "string" ? item : item.label;
                const hint = typeof item === "string" ? undefined : item.hint;
                const status = typeof item === "string" ? undefined : item.status;
                const origin = typeof item === "string" ? undefined : item.origin;
                const note = typeof item === "string" ? undefined : item.note;

                return (
                  <li key={`${group.title}-${label}`} className="dependency-tree__item">
                    <div className="dependency-tree__item-main">
                      <span>{label}</span>
                      {status ? (
                        <span className={`mono-pill dependency-status dependency-status--${status}`}>
                          {status}
                        </span>
                      ) : null}
                      {origin ? <span className="mono-pill">{origin}</span> : null}
                    </div>
                    {hint ? <div className="muted-text">{hint}</div> : null}
                    {note ? <div className="muted-text">{note}</div> : null}
                  </li>
                );
              })}
            </ul>
          </article>
        ))}
      </div>
    </section>
  );
}
