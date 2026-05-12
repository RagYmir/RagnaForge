import type { ReactNode } from "react";

interface PipelineWorkspaceLayoutProps {
  sidebar: ReactNode;
  primary: ReactNode;
  inspector: ReactNode;
  footer?: ReactNode;
}

export function PipelineWorkspaceLayout({
  sidebar,
  primary,
  inspector,
  footer,
}: PipelineWorkspaceLayoutProps) {
  return (
    <div className="workspace-layout">
      <aside className="workspace-layout__sidebar">{sidebar}</aside>
      <section className="workspace-layout__primary">{primary}</section>
      <aside className="workspace-layout__inspector">{inspector}</aside>
      {footer ? <section className="workspace-layout__footer">{footer}</section> : null}
    </div>
  );
}
