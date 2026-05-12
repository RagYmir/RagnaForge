import type { PipelineReport } from "../api/types";
import { ApiStatusBadge } from "./ApiStatusBadge";
import { ReadinessBadge } from "./ReadinessBadge";

export function ReportOverview({ report }: { report?: PipelineReport }) {
  if (!report) {
    return null;
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <h3>Resumo do relatório</h3>
      </div>
      <div className="badge-grid">
        <ApiStatusBadge label={`CanApply = ${String(report.canApply ?? false)}`} tone={report.canApply ? "good" : "danger"} />
        <ReadinessBadge value={String(report.applyReadiness ?? "")} />
        {report.clientSideMode ? <ApiStatusBadge label={`ClientSideMode = ${report.clientSideMode}`} /> : null}
        {report.serverCanApply != null ? (
          <ApiStatusBadge label={`ServerCanApply = ${String(report.serverCanApply)}`} tone={report.serverCanApply ? "good" : "warning"} />
        ) : null}
        {report.canApplyClientIdentity != null ? (
          <ApiStatusBadge
            label={`CanApplyClientIdentity = ${String(report.canApplyClientIdentity)}`}
            tone={report.canApplyClientIdentity ? "good" : "warning"}
          />
        ) : null}
      </div>
      {report.shieldRestriction ? <p>{report.shieldRestriction}</p> : null}
    </section>
  );
}
