export type PipelineCategory =
  | "items"
  | "equipment"
  | "npcs"
  | "monsters"
  | "maps"
  | "grf"
  | "validation";

export type PipelineHistoryKind = "dry-run" | "diff-preview" | "report";

export interface PipelineHistoryEntry {
  id: string;
  category: PipelineCategory;
  kind: PipelineHistoryKind;
  createdAt: string;
  payload: Record<string, unknown>;
  responseData: unknown;
  success: boolean;
  warningsCount: number;
  errorsCount: number;
  summary: string;
  correlationId?: string;
  readiness?: string;
  canApply?: boolean;
  diffFileCount?: number;
}

export interface PipelineComparisonRow {
  key: string;
  label: string;
  left: string;
  right: string;
  changed: boolean;
}

export interface PipelineComparison {
  left?: PipelineHistoryEntry;
  right?: PipelineHistoryEntry;
  rows: PipelineComparisonRow[];
}

const STORAGE_KEY = "ragnaforge-admin-ui.pipeline-history.v1";
const STORAGE_LIMIT = 80;

function safeJsonParse<T>(value: string | null, fallback: T) {
  if (!value) {
    return fallback;
  }

  try {
    return JSON.parse(value) as T;
  } catch {
    return fallback;
  }
}

function toRecord(value: unknown) {
  return value && typeof value === "object" ? (value as Record<string, unknown>) : undefined;
}

function toArrayLength(value: unknown) {
  return Array.isArray(value) ? value.length : 0;
}

function stringifyValue(value: unknown) {
  if (value == null || value === "") {
    return "-";
  }

  if (typeof value === "string" || typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  if (Array.isArray(value)) {
    return `${value.length} item(ns)`;
  }

  return "Objeto";
}

export function readPipelineHistory(): PipelineHistoryEntry[] {
  const entries = safeJsonParse<PipelineHistoryEntry[]>(localStorage.getItem(STORAGE_KEY), []);
  return Array.isArray(entries) ? entries : [];
}

export function listPipelineHistory(category?: PipelineCategory) {
  const entries = readPipelineHistory();
  return category ? entries.filter((entry) => entry.category === category) : entries;
}

export function savePipelineHistoryEntry(
  entry: Omit<PipelineHistoryEntry, "id" | "createdAt">,
) {
  const next: PipelineHistoryEntry = {
    ...entry,
    id: crypto.randomUUID(),
    createdAt: new Date().toISOString(),
  };

  const current = readPipelineHistory();
  const updated = [next, ...current].slice(0, STORAGE_LIMIT);
  localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
  return next;
}

export function clearPipelineHistory(category?: PipelineCategory) {
  if (!category) {
    localStorage.removeItem(STORAGE_KEY);
    return;
  }

  const updated = readPipelineHistory().filter((entry) => entry.category !== category);
  localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
}

export function summarizeHistoryForComparison(
  left?: PipelineHistoryEntry,
  right?: PipelineHistoryEntry,
): PipelineComparison {
  const leftData = toRecord(left?.responseData);
  const rightData = toRecord(right?.responseData);

  const rows: PipelineComparisonRow[] = [
    {
      key: "kind",
      label: "Tipo",
      left: left?.kind ?? "-",
      right: right?.kind ?? "-",
      changed: left?.kind !== right?.kind,
    },
    {
      key: "readiness",
      label: "Readiness",
      left: left?.readiness ?? "-",
      right: right?.readiness ?? "-",
      changed: left?.readiness !== right?.readiness,
    },
    {
      key: "can-apply",
      label: "CanApply",
      left: stringifyValue(left?.canApply),
      right: stringifyValue(right?.canApply),
      changed: left?.canApply !== right?.canApply,
    },
    {
      key: "warnings",
      label: "Warnings",
      left: String(left?.warningsCount ?? 0),
      right: String(right?.warningsCount ?? 0),
      changed: left?.warningsCount !== right?.warningsCount,
    },
    {
      key: "errors",
      label: "Errors",
      left: String(left?.errorsCount ?? 0),
      right: String(right?.errorsCount ?? 0),
      changed: left?.errorsCount !== right?.errorsCount,
    },
    {
      key: "diff-count",
      label: "Diff file count",
      left: String(left?.diffFileCount ?? 0),
      right: String(right?.diffFileCount ?? 0),
      changed: left?.diffFileCount !== right?.diffFileCount,
    },
    {
      key: "client-plan",
      label: "ClientSidePlan",
      left: stringifyValue(toRecord(leftData?.clientSidePlan)?.applyReadiness ?? toRecord(leftData?.clientSidePlan)?.clientSideMode),
      right: stringifyValue(toRecord(rightData?.clientSidePlan)?.applyReadiness ?? toRecord(rightData?.clientSidePlan)?.clientSideMode),
      changed:
        stringifyValue(toRecord(leftData?.clientSidePlan)?.applyReadiness ?? toRecord(leftData?.clientSidePlan)?.clientSideMode) !==
        stringifyValue(toRecord(rightData?.clientSidePlan)?.applyReadiness ?? toRecord(rightData?.clientSidePlan)?.clientSideMode),
    },
    {
      key: "visual-plan",
      label: "VisualClientSidePlan",
      left: stringifyValue(toRecord(leftData?.visualClientSidePlan)?.applyReadiness),
      right: stringifyValue(toRecord(rightData?.visualClientSidePlan)?.applyReadiness),
      changed:
        stringifyValue(toRecord(leftData?.visualClientSidePlan)?.applyReadiness) !==
        stringifyValue(toRecord(rightData?.visualClientSidePlan)?.applyReadiness),
    },
    {
      key: "identity-plan",
      label: "ClientIdentityPlan",
      left: stringifyValue(toRecord(leftData?.clientIdentityPlan)?.applyReadiness),
      right: stringifyValue(toRecord(rightData?.clientIdentityPlan)?.applyReadiness),
      changed:
        stringifyValue(toRecord(leftData?.clientIdentityPlan)?.applyReadiness) !==
        stringifyValue(toRecord(rightData?.clientIdentityPlan)?.applyReadiness),
    },
    {
      key: "asset-plan-count",
      label: "AssetPlans",
      left: String(toArrayLength(leftData?.assetPlans)),
      right: String(toArrayLength(rightData?.assetPlans)),
      changed: toArrayLength(leftData?.assetPlans) !== toArrayLength(rightData?.assetPlans),
    },
    {
      key: "map-cache",
      label: "MapCachePlan",
      left: stringifyValue(Boolean(leftData?.mapCachePlan)),
      right: stringifyValue(Boolean(rightData?.mapCachePlan)),
      changed: Boolean(leftData?.mapCachePlan) !== Boolean(rightData?.mapCachePlan),
    },
  ];

  return { left, right, rows };
}
