import type { ApiProblemDetails, ApiResponse } from "../api/types";
import { CorrelationIdBadge } from "./CorrelationIdBadge";
import { ErrorList } from "./ErrorList";
import { ProblemDetailsView } from "./ProblemDetailsView";
import { WarningList } from "./WarningList";

export function ResultShell<T>({
  response,
  problem,
  children
}: {
  response?: ApiResponse<T>;
  problem?: ApiProblemDetails;
  children?: React.ReactNode;
}) {
  return (
    <div className="stack-lg">
      {response ? (
        <section className="panel">
          <div className="panel-header">
            <h3>Resposta da API</h3>
            <CorrelationIdBadge correlationId={response.correlationId} />
          </div>
          <dl className="detail-grid compact">
            <div>
              <dt>OperationKind</dt>
              <dd>{response.operationKind}</dd>
            </div>
            <div>
              <dt>ReadOnlyMode</dt>
              <dd>{String(response.readOnlyMode)}</dd>
            </div>
            <div>
              <dt>Duration</dt>
              <dd>{response.durationMs} ms</dd>
            </div>
            <div>
              <dt>GeneratedAt</dt>
              <dd>{response.generatedAt}</dd>
            </div>
          </dl>
        </section>
      ) : null}
      <WarningList warnings={response?.warnings} />
      <ErrorList errors={response?.errors} />
      <ProblemDetailsView problem={problem} />
      {children}
    </div>
  );
}
