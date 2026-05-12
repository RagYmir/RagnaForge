export interface EntityGridItem {
  key: string;
  label: string;
  subtitle?: string;
  status?: "good" | "warning" | "danger" | "neutral";
  href?: string;
}

interface EntityGridProps {
  title: string;
  description?: string;
  items: EntityGridItem[];
  activeKey?: string;
}

export function EntityGrid({ title, description, items, activeKey }: EntityGridProps) {
  return (
    <section className="panel entity-grid">
      <div className="panel-header">
        <div>
          <h3>{title}</h3>
          {description ? <p className="muted-text">{description}</p> : null}
        </div>
      </div>
      <div className="entity-grid__list">
        {items.map((item) => {
          const className = [
            "entity-grid__item",
            activeKey === item.key ? "entity-grid__item--active" : "",
          ]
            .filter(Boolean)
            .join(" ");

          const content = (
            <>
              <span className="entity-grid__item-title">{item.label}</span>
              {item.subtitle ? (
                <span className="entity-grid__item-subtitle">{item.subtitle}</span>
              ) : null}
              {item.status ? (
                <span className={`entity-grid__item-status entity-grid__item-status--${item.status}`} />
              ) : null}
            </>
          );

          return item.href ? (
            <a key={item.key} className={className} href={item.href}>
              {content}
            </a>
          ) : (
            <div key={item.key} className={className}>
              {content}
            </div>
          );
        })}
      </div>
    </section>
  );
}
