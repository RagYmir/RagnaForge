import type { PipelineReport } from "../api/types";
import { ApplyReadinessPanel } from "./ApplyReadinessPanel";
import { AssetLookupPanel } from "./AssetLookupPanel";
import { BytecodeBlockPanel } from "./BytecodeBlockPanel";
import { ClientSidePlanPanel } from "./ClientSidePlanPanel";
import { DiffViewer } from "./DiffViewer";
import { ErrorList } from "./ErrorList";
import { JsonInspector } from "./JsonInspector";
import { ReportOverview } from "./ReportOverview";
import { WarningList } from "./WarningList";

export function PipelineReportView({ report }: { report?: PipelineReport }) {
  if (!report) {
    return null;
  }

  return (
    <div className="stack-lg">
      <ReportOverview report={report} />
      <WarningList warnings={[...(report.warnings ?? []), ...(report.validationWarnings ?? [])]} />
      <ErrorList errors={[...(report.errors ?? []), ...(report.validationErrors ?? [])]} />
      <ApplyReadinessPanel
        label="Apply readiness"
        readiness={String(report.applyReadiness ?? "")}
        blockReasons={(report.clientSidePlan?.blockReasons as string[] | undefined) ?? (report.clientIdentityPlan?.blockReasons as string[] | undefined)}
      />
      <ClientSidePlanPanel title="Client-side plan" plan={report.clientSidePlan} />
      <ClientSidePlanPanel title="Visual client-side plan" plan={report.visualClientSidePlan} />
      <ClientSidePlanPanel title="NPC client identity plan" plan={report.clientIdentityPlan} />
      <AssetLookupPanel title="Asset lookup" report={report.assetLookup} />
      <AssetLookupPanel title="Item asset lookup" report={report.itemAssetLookup} />
      <AssetLookupPanel title="Visual asset lookup" report={report.visualAssetLookup} />
      <BytecodeBlockPanel files={report.bytecodeBlocks} />
      <DiffViewer diff={report.diffPreview} />
      <JsonInspector title="Relatório bruto" value={report} />
    </div>
  );
}
