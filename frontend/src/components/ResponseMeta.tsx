import type { ApiResponse } from "../api/types";
import { CorrelationIdBadge } from "./CorrelationIdBadge";

interface ResponseMetaProps {
  response?: Pick<
    ApiResponse<unknown>,
    "correlationId" | "generatedAt" | "durationMs" | "operationKind"
  > | null;
}

export function ResponseMeta({ response }: ResponseMetaProps) {
  if (!response) {
    return null;
  }

  return (
    <div className="response-meta">
      <CorrelationIdBadge correlationId={response.correlationId} />
      <span className="response-meta__pill">
        Operacao: {response.operationKind}
      </span>
      <span className="response-meta__pill">
        Duracao: {response.durationMs} ms
      </span>
      <span className="response-meta__pill">
        Gerado em: {new Date(response.generatedAt).toLocaleString("pt-BR")}
      </span>
    </div>
  );
}
