import type { ClientPlan } from "../api/types";
import { BytecodeBlockPanel } from "./BytecodeBlockPanel";
import { DependencyTree } from "./DependencyTree";
import { ReadinessBadge } from "./ReadinessBadge";

export function ClientSidePlanPanel({ title, plan }: { title: string; plan?: ClientPlan }) {
  if (!plan) {
    return null;
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <h3>{title}</h3>
        <ReadinessBadge value={String(plan.applyReadiness ?? plan.clientSideMode ?? "")} />
      </div>
      <dl className="detail-grid compact">
        <div>
          <dt>Mode</dt>
          <dd>{String(plan.clientSideMode ?? "-")}</dd>
        </div>
        <div>
          <dt>Can apply</dt>
          <dd>{String(plan.canApply ?? "-")}</dd>
        </div>
        <div>
          <dt>ItemInfo</dt>
          <dd>{String(plan.itemInfoDetected ?? "-")}</dd>
        </div>
        <div>
          <dt>Hybrid</dt>
          <dd>{String(plan.hybridClientDetected ?? "-")}</dd>
        </div>
      </dl>
      {plan.filesDetected?.length ? (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Logical name</th>
                <th>Format</th>
                <th>Path</th>
                <th>Selected</th>
              </tr>
            </thead>
            <tbody>
              {plan.filesDetected.map((file) => (
                <tr key={`${file.logicalName}-${file.path}`}>
                  <td>{file.logicalName}</td>
                  <td>{file.format}</td>
                  <td className="mono-text">{file.path}</td>
                  <td>{String(file.selected ?? false)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
      {plan.fileFormats?.length ? (
        <div className="stack-sm">
          <h4>Formats</h4>
          <div className="token-wrap">
            {plan.fileFormats.map((entry) => (
              <span key={entry} className="token mono-text">
                {entry}
              </span>
            ))}
          </div>
        </div>
      ) : null}
      {plan.proposedRegistrations?.length ? (
        <div className="stack-sm">
          <h4>Proposed registrations</h4>
          <ul className="flat-list">
            {plan.proposedRegistrations.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </div>
      ) : null}
      {plan.existingRegistrations?.length ? (
        <div className="stack-sm">
          <h4>Existing registrations</h4>
          <ul className="flat-list">
            {plan.existingRegistrations.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </div>
      ) : null}
      <DependencyTree
        title="Targets and validations"
        groups={[
          { title: "Supported targets", items: plan.supportedTargets ?? [] },
          { title: "Unsupported targets", items: plan.unsupportedTargets ?? [] },
          { title: "Apply targets", items: plan.applyTargets ?? [] },
          { title: "Rollback targets", items: plan.rollbackTargets ?? [] },
          { title: "Post-write validation", items: plan.postWriteValidationPlan ?? [] },
        ]}
      />
      {plan.validationWarnings?.length ? (
        <div className="stack-sm">
          <h4>Validation warnings</h4>
          <ul className="flat-list">
            {plan.validationWarnings.map((warning) => (
              <li key={warning}>{warning}</li>
            ))}
          </ul>
        </div>
      ) : null}
      {plan.validationErrors?.length ? (
        <div className="stack-sm">
          <h4>Validation errors</h4>
          <ul className="flat-list">
            {plan.validationErrors.map((error) => (
              <li key={error}>{error}</li>
            ))}
          </ul>
        </div>
      ) : null}
      <BytecodeBlockPanel files={plan.bytecodeBlockedFiles} />
    </section>
  );
}
