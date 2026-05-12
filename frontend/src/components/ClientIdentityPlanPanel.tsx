import type { PipelineReport } from "../api/types";
import { ApiStatusBadge } from "./ApiStatusBadge";
import { BytecodeBlockPanel } from "./BytecodeBlockPanel";
import { ClientSidePlanPanel } from "./ClientSidePlanPanel";
import { DependencyTree } from "./DependencyTree";

export function ClientIdentityPlanPanel({ report }: { report?: PipelineReport }) {
  if (!report?.clientIdentityPlan) {
    return null;
  }

  const plan = report.clientIdentityPlan;
  const spriteResolution = report.spriteResolution as Record<string, unknown> | undefined;
  const spriteSource = String(
    plan.spriteSource ?? report.detectionSource ?? spriteResolution?.source ?? "-",
  );
  const spritePath = String(
    plan.spritePath ?? spriteResolution?.path ?? "-",
  );
  const ambiguous = Boolean(spriteResolution?.ambiguous);
  const needsAssetCopy = Boolean(spriteResolution?.needsAssetCopyPlan);

  return (
    <div className="stack-lg">
      <section className="panel">
        <div className="panel-header">
          <h3>Sprite resolution</h3>
        </div>
        <div className="badge-grid">
          <ApiStatusBadge
            label={`SpriteResolved = ${String(plan.spriteResolved ?? false)}`}
            tone={plan.spriteResolved ? "good" : "warning"}
          />
          <ApiStatusBadge
            label={`ServerCanApply = ${String(report.serverCanApply ?? false)}`}
            tone={report.serverCanApply ? "good" : "warning"}
          />
          <ApiStatusBadge
            label={`CanApplyClientIdentity = ${String(report.canApplyClientIdentity ?? false)}`}
            tone={report.canApplyClientIdentity ? "good" : "warning"}
          />
          <ApiStatusBadge
            label={`DetectionSource = ${spriteSource}`}
            tone={ambiguous ? "danger" : needsAssetCopy ? "warning" : "neutral"}
          />
        </div>
        <dl className="detail-grid">
          <div>
            <dt>SpriteName</dt>
            <dd>{String(plan.spriteName ?? "-")}</dd>
          </div>
          <div>
            <dt>SpritePath</dt>
            <dd className="mono-text">{spritePath}</dd>
          </div>
          <div>
            <dt>Ambiguous</dt>
            <dd>{String(ambiguous)}</dd>
          </div>
          <div>
            <dt>NeedsAssetCopyPlan</dt>
            <dd>{String(needsAssetCopy)}</dd>
          </div>
        </dl>
      </section>
      <ClientSidePlanPanel title="ClientIdentityPlan" plan={plan} />
      <DependencyTree
        title="Identity registrations"
        groups={[
          {
            title: "Required client files",
            items: report.requiredClientFiles ?? [],
          },
          {
            title: "Existing client registration",
            items:
              report.existingClientRegistration ??
              plan.existingRegistrations ??
              [],
          },
          {
            title: "Proposed client registration",
            items:
              report.proposedClientRegistration ??
              plan.proposedRegistrations ??
              [],
          },
          {
            title: "Post-write validation",
            items:
              report.postWriteValidationPlan ??
              plan.postWriteValidationPlan ??
              [],
          },
        ]}
      />
      <BytecodeBlockPanel files={plan.bytecodeBlockedFiles} />
    </div>
  );
}
