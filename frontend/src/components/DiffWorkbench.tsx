import { useMemo, useState } from "react";
import type { ReactNode } from "react";
import type { DiffEntry } from "../api/types";
import { groupDiffEntries } from "../features/shared/diffGrouping";

interface DiffWorkbenchTab {
  key: string;
  label: string;
  content: ReactNode;
}

interface DiffWorkbenchProps {
  title: string;
  tabs: DiffWorkbenchTab[];
  defaultTabKey?: string;
  diffEntries?: DiffEntry[];
  warnings?: string[];
}

export function DiffWorkbench({
  title,
  tabs,
  defaultTabKey,
  diffEntries = [],
  warnings = [],
}: DiffWorkbenchProps) {
  const availableTabs = useMemo(
    () => tabs.filter((tab) => tab.content != null),
    [tabs],
  );
  const diffGroups = useMemo(() => groupDiffEntries(diffEntries), [diffEntries]);

  const [activeKey, setActiveKey] = useState(
    defaultTabKey ?? availableTabs[0]?.key ?? "empty",
  );
  const [expandedGroups, setExpandedGroups] = useState<string[]>([]);

  const activeTab = availableTabs.find((tab) => tab.key === activeKey) ?? availableTabs[0];

  function toggleGroup(key: string) {
    setExpandedGroups((current) =>
      current.includes(key)
        ? current.filter((entry) => entry !== key)
        : [...current, key],
    );
  }

  async function copyDiff(entry: DiffEntry) {
    if (!entry.unifiedDiff && !entry.preview) {
      return;
    }

    const content = entry.unifiedDiff ?? entry.preview ?? "";
    if (navigator.clipboard) {
      await navigator.clipboard.writeText(content);
    }
  }

  return (
    <section className="panel diff-workbench">
      <div className="panel-header">
        <h3>{title}</h3>
      </div>
      <div className="diff-workbench__tabs" role="tablist" aria-label={title}>
        {availableTabs.map((tab) => (
          <button
            key={tab.key}
            className={`diff-workbench__tab ${tab.key === activeTab?.key ? "diff-workbench__tab--active" : ""}`}
            type="button"
            role="tab"
            aria-selected={tab.key === activeTab?.key}
            onClick={() => setActiveKey(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </div>
      {diffGroups.length ? (
        <div className="diff-groups">
          {diffGroups.map((group) => {
            const expanded = expandedGroups.includes(group.key);
            return (
              <section key={group.key} className="diff-group">
                <button
                  type="button"
                  className="diff-group__header"
                  onClick={() => toggleGroup(group.key)}
                >
                  <span>{group.label}</span>
                  <span className="mono-pill">{group.entries.length}</span>
                </button>
                {expanded ? (
                  <div className="diff-group__body">
                    {group.entries.map((entry) => (
                      <article key={`${group.key}-${entry.targetPath}`} className="diff-group__entry">
                        <div className="diff-group__entry-meta">
                          <strong className="mono-text">{entry.targetPath}</strong>
                          <div className="token-wrap">
                            <span className="mono-pill">{entry.changeKind}</span>
                            <span className="mono-pill">
                              {`${String(entry.beforeLineCount ?? 0)} -> ${String(entry.afterLineCount ?? 0)}`}
                            </span>
                          </div>
                        </div>
                        <div className="button-row">
                          <button type="button" className="button-secondary" onClick={() => copyDiff(entry)}>
                            Copiar trecho
                          </button>
                        </div>
                        {entry.preview ? <pre className="raw-diff">{entry.preview}</pre> : null}
                      </article>
                    ))}
                  </div>
                ) : null}
              </section>
            );
          })}
          {warnings.length ? (
            <div className="notice notice--info">
              Warnings relacionados: {warnings.join(" | ")}
            </div>
          ) : null}
        </div>
      ) : null}
      <div className="diff-workbench__content">
        {activeTab?.content ?? <p className="muted-text">Nenhum conteudo disponivel.</p>}
      </div>
    </section>
  );
}
