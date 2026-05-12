import { useMemo, useState } from "react";
import { ValidationIssueTable, type ValidationIssueRow } from "./ValidationIssueTable";

export interface ValidationMatrixIssue extends ValidationIssueRow {}

interface ValidationMatrixProps {
  warnings?: string[];
  errors?: string[];
  blockReasons?: string[];
  validationWarnings?: string[];
  validationErrors?: string[];
  issues?: ValidationMatrixIssue[];
}

interface ValidationColumnProps {
  title: string;
  tone: "good" | "warning" | "danger" | "neutral";
  items: string[];
  emptyLabel: string;
}

function ValidationColumn({
  title,
  tone,
  items,
  emptyLabel,
}: ValidationColumnProps) {
  return (
    <article className={`validation-column validation-column--${tone}`}>
      <div className="validation-column__header">
        <h4>{title}</h4>
        <span className="mono-pill">{items.length}</span>
      </div>
      {items.length ? (
        <ul className="flat-list">
          {items.map((item) => (
            <li key={`${title}-${item}`}>{item}</li>
          ))}
        </ul>
      ) : (
        <p className="muted-text">{emptyLabel}</p>
      )}
    </article>
  );
}

export function ValidationMatrix({
  warnings = [],
  errors = [],
  blockReasons = [],
  validationWarnings = [],
  validationErrors = [],
  issues = [],
}: ValidationMatrixProps) {
  const [severityFilter, setSeverityFilter] = useState("all");
  const [tagFilter, setTagFilter] = useState("all");
  const [categoryFilter, setCategoryFilter] = useState("all");
  const [originFilter, setOriginFilter] = useState("all");
  const [entityFilter, setEntityFilter] = useState("");

  const derivedIssues = useMemo<ValidationMatrixIssue[]>(
    () => [
      ...warnings.map((message, index) => ({
        key: `warning-${index}-${message}`,
        severity: "warning" as const,
        category: "Warnings",
        entity: "Pipeline",
        file: "-",
        origin: "warning",
        message,
        recommendedAction: "Revisar o warning antes de confiar no resultado.",
        tags: message.toLowerCase().includes("bytecode")
          ? ["bytecode", "blocked"]
          : message.toLowerCase().includes("asset")
            ? ["missing-asset"]
            : [],
        blocksFutureApply: false,
      })),
      ...errors.map((message, index) => ({
        key: `error-${index}-${message}`,
        severity: "danger" as const,
        category: "Errors",
        entity: "Pipeline",
        file: "-",
        origin: "error",
        message,
        recommendedAction: "Resolver o erro antes de seguir com o planejamento.",
        tags: message.toLowerCase().includes("conflict")
          ? ["conflict", "blocked"]
          : message.toLowerCase().includes("bytecode")
            ? ["bytecode", "blocked"]
            : ["blocked"],
        blocksFutureApply: true,
      })),
      ...blockReasons.map((message, index) => ({
        key: `block-${index}-${message}`,
        severity: "danger" as const,
        category: "Block reasons",
        entity: "Pipeline",
        file: "-",
        origin: "blocked",
        message,
        recommendedAction: "Tratar o bloqueio antes de considerar qualquer proximo passo.",
        tags: message.toLowerCase().includes("bytecode")
          ? ["bytecode", "blocked"]
          : message.toLowerCase().includes("asset")
            ? ["missing-asset", "blocked"]
            : ["blocked"],
        blocksFutureApply: true,
      })),
      ...validationErrors.map((message, index) => ({
        key: `validation-error-${index}-${message}`,
        severity: "danger" as const,
        category: "Post-write validation",
        entity: "Validation",
        file: "-",
        origin: "validation",
        message,
        recommendedAction: "Ajustar a proposta para que a validacao final fique limpa.",
        tags: ["blocked"],
        blocksFutureApply: true,
      })),
      ...validationWarnings.map((message, index) => ({
        key: `validation-warning-${index}-${message}`,
        severity: "warning" as const,
        category: "Post-write validation",
        entity: "Validation",
        file: "-",
        origin: "validation",
        message,
        recommendedAction: "Inspecionar a validacao final antes de seguir.",
        tags: [],
        blocksFutureApply: false,
      })),
      ...issues,
    ],
    [warnings, errors, blockReasons, validationWarnings, validationErrors, issues],
  );

  const filteredIssues = derivedIssues.filter((issue) => {
    if (severityFilter !== "all" && issue.severity !== severityFilter) {
      return false;
    }

    if (tagFilter !== "all" && !(issue.tags ?? []).includes(tagFilter)) {
      return false;
    }

    if (categoryFilter !== "all" && issue.category !== categoryFilter) {
      return false;
    }

    if (originFilter !== "all" && issue.origin !== originFilter) {
      return false;
    }

    if (
      entityFilter.trim() &&
      !issue.entity.toLowerCase().includes(entityFilter.trim().toLowerCase())
    ) {
      return false;
    }

    return true;
  });

  const categoryOptions = Array.from(
    new Set(
      derivedIssues
        .map((issue) => issue.category)
        .filter((value): value is string => Boolean(value)),
    ),
  ).sort();

  const originOptions = Array.from(new Set(derivedIssues.map((issue) => issue.origin))).sort();

  return (
    <section className="panel">
      <div className="panel-header">
        <h3>Validation matrix</h3>
      </div>
      <div className="validation-matrix__filters">
        <label>
          <span>Severidade</span>
          <select value={severityFilter} onChange={(event) => setSeverityFilter(event.target.value)}>
            <option value="all">Todas</option>
            <option value="info">Info</option>
            <option value="warning">Warning</option>
            <option value="danger">Danger</option>
          </select>
        </label>
        <label>
          <span>Tag</span>
          <select value={tagFilter} onChange={(event) => setTagFilter(event.target.value)}>
            <option value="all">Todas</option>
            <option value="bytecode">Bytecode</option>
            <option value="missing-asset">Missing asset</option>
            <option value="conflict">Conflict</option>
            <option value="blocked">Blocked</option>
          </select>
        </label>
        <label>
          <span>Categoria</span>
          <select value={categoryFilter} onChange={(event) => setCategoryFilter(event.target.value)}>
            <option value="all">Todas</option>
            {categoryOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </label>
        <label>
          <span>Origem</span>
          <select value={originFilter} onChange={(event) => setOriginFilter(event.target.value)}>
            <option value="all">Todas</option>
            {originOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </label>
        <label>
          <span>Entidade</span>
          <input
            value={entityFilter}
            onChange={(event) => setEntityFilter(event.target.value)}
            placeholder="Item, NPC, Mapa..."
          />
        </label>
      </div>
      <div className="validation-matrix">
        <ValidationColumn
          title="Warnings"
          tone="warning"
          items={warnings}
          emptyLabel="Nenhum warning retornado."
        />
        <ValidationColumn
          title="Errors"
          tone="danger"
          items={errors}
          emptyLabel="Nenhum erro retornado."
        />
        <ValidationColumn
          title="Block reasons"
          tone="danger"
          items={blockReasons}
          emptyLabel="Nenhum bloqueio estrutural."
        />
        <ValidationColumn
          title="Post-write validation"
          tone={validationErrors.length > 0 ? "danger" : validationWarnings.length > 0 ? "warning" : "good"}
          items={[...validationErrors, ...validationWarnings]}
          emptyLabel="Nenhuma restricao adicional informada."
        />
      </div>
      <ValidationIssueTable rows={filteredIssues} />
    </section>
  );
}
