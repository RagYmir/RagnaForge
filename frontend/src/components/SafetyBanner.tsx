import type { StatusData } from "../api/types";
import { ApiStatusBadge } from "./ApiStatusBadge";

export function SafetyBanner({ status }: { status?: StatusData }) {
  if (!status) {
    return null;
  }

  const allowedOperations = ["ReadOnly", "DryRun", "DiffPreview"];
  const blockedOperations = [
    "Apply",
    "Rollback",
    "FileWrite",
    "ExternalRepoWrite",
    "GrfWrite",
  ];

  return (
    <section className="hero-banner">
      <div className="stack-sm">
        <div>
          <p className="eyebrow">Modo da interface</p>
          <h1>Planejamento e analise apenas</h1>
        </div>
        <p className="hero-copy">
          Apply e rollback nao existem nesta interface nesta fase. Esta UI e apenas para analise,
          dry-run e diff-preview.
        </p>
        <div className="token-wrap">
          {allowedOperations.map((operation) => (
            <span key={operation} className="token">
              Permitido: {operation}
            </span>
          ))}
          {blockedOperations.map((operation) => (
            <span key={operation} className="token token--danger">
              Bloqueado: {operation}
            </span>
          ))}
        </div>
      </div>
      <div className="badge-grid">
        <ApiStatusBadge label={`ReadOnlyMode = ${String(status.readOnlyMode)}`} tone="good" />
        <ApiStatusBadge
          label={`ApplyEnabled = ${String(status.applyEndpointsEnabled)}`}
          tone="danger"
        />
        <ApiStatusBadge
          label={`RollbackEnabled = ${String(status.rollbackEndpointsEnabled)}`}
          tone="danger"
        />
        <ApiStatusBadge
          label={`API Key = ${status.requireApiKey ? "required" : "optional"}`}
          tone="warning"
        />
        <ApiStatusBadge label={`Mode = ${status.mode}`} tone="neutral" />
      </div>
    </section>
  );
}
