import { CorrelationIdBadge } from "./CorrelationIdBadge";
import { ReadinessBadge } from "./ReadinessBadge";
import { ApiStatusBadge } from "./ApiStatusBadge";

interface ReadinessRibbonProps {
  readiness?: string;
  canApply?: boolean;
  warningsCount?: number;
  errorsCount?: number;
  correlationId?: string;
  modeLabel?: string;
}

export function ReadinessRibbon({
  readiness,
  canApply,
  warningsCount = 0,
  errorsCount = 0,
  correlationId,
  modeLabel,
}: ReadinessRibbonProps) {
  return (
    <section className="panel readiness-ribbon">
      <div className="panel-header">
        <h3>Readiness</h3>
      </div>
      <div className="readiness-ribbon__badges">
        {readiness ? <ReadinessBadge value={readiness} /> : null}
        {canApply != null ? (
          <ApiStatusBadge
            label={`CanApply = ${String(canApply)}`}
            tone={canApply ? "good" : "danger"}
          />
        ) : null}
        <ApiStatusBadge
          label={`Warnings = ${warningsCount}`}
          tone={warningsCount > 0 ? "warning" : "good"}
        />
        <ApiStatusBadge
          label={`Errors = ${errorsCount}`}
          tone={errorsCount > 0 ? "danger" : "good"}
        />
        {modeLabel ? <ApiStatusBadge label={modeLabel} tone="neutral" /> : null}
        {correlationId ? <CorrelationIdBadge correlationId={correlationId} /> : null}
      </div>
    </section>
  );
}
