import type { ApiProblemDetails } from "../api/types";
import { CorrelationIdBadge } from "./CorrelationIdBadge";

export function ProblemDetailsView({ problem }: { problem?: ApiProblemDetails }) {
  if (!problem) {
    return null;
  }

  return (
    <div className="message-card error-card">
      <div className="panel-header">
        <h3>{problem.title ?? "Erro de API"}</h3>
        <CorrelationIdBadge correlationId={problem.correlationId} />
      </div>
      <p>{problem.detail ?? "A API retornou um erro sem detalhe adicional."}</p>
      <dl className="detail-grid compact">
        <div>
          <dt>Status</dt>
          <dd>{problem.status ?? "?"}</dd>
        </div>
        <div>
          <dt>ErrorCode</dt>
          <dd>{problem.errorCode ?? "request.failed"}</dd>
        </div>
        <div>
          <dt>Path</dt>
          <dd className="mono-text">{problem.path ?? problem.instance ?? "-"}</dd>
        </div>
        <div>
          <dt>Timestamp</dt>
          <dd>{problem.timestamp ?? "-"}</dd>
        </div>
      </dl>
      {problem.validationErrors ? (
        <div className="stack-sm">
          <h4>Validation</h4>
          <ul className="flat-list">
            {Object.entries(problem.validationErrors).map(([field, messages]) => (
              <li key={field}>
                <strong>{field}:</strong> {messages.join(", ")}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}
