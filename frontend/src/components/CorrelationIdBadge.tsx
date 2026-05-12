export function CorrelationIdBadge({ correlationId }: { correlationId?: string }) {
  if (!correlationId) {
    return null;
  }

  return <span className="mono-pill">CorrelationId: {correlationId}</span>;
}
