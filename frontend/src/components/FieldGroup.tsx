import type { ReactNode } from "react";

interface FieldGroupProps {
  id?: string;
  title: string;
  description?: string;
  actions?: ReactNode;
  children: ReactNode;
}

export function FieldGroup({
  id,
  title,
  description,
  actions,
  children,
}: FieldGroupProps) {
  return (
    <section id={id} className="panel field-group">
      <div className="panel-header">
        <div>
          <h3>{title}</h3>
          {description ? <p className="muted-text">{description}</p> : null}
        </div>
        {actions}
      </div>
      {children}
    </section>
  );
}
