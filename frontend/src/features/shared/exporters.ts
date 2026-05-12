import type { PipelineComparison, PipelineHistoryEntry } from "./localHistory";

function triggerDownload(fileName: string, content: string, mimeType: string) {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

function slug(value: string) {
  return value.toLowerCase().replace(/[^a-z0-9]+/gi, "-").replace(/^-+|-+$/g, "");
}

export function exportHistoryEntryJson(entry: PipelineHistoryEntry) {
  triggerDownload(
    `${entry.category}-${entry.kind}-${slug(entry.summary || entry.id)}.json`,
    JSON.stringify(entry, null, 2),
    "application/json",
  );
}

export function exportHistoryEntryMarkdown(entry: PipelineHistoryEntry) {
  const content = [
    `# ${entry.category} ${entry.kind}`,
    "",
    `- Summary: ${entry.summary}`,
    `- Success: ${String(entry.success)}`,
    `- Readiness: ${entry.readiness ?? "-"}`,
    `- CanApply: ${String(entry.canApply ?? "-")}`,
    `- Warnings: ${entry.warningsCount}`,
    `- Errors: ${entry.errorsCount}`,
    `- CorrelationId: ${entry.correlationId ?? "-"}`,
    `- CreatedAt: ${entry.createdAt}`,
    "",
    "## Payload",
    "```json",
    JSON.stringify(entry.payload, null, 2),
    "```",
    "",
    "## Response",
    "```json",
    JSON.stringify(entry.responseData, null, 2),
    "```",
  ].join("\n");

  triggerDownload(
    `${entry.category}-${entry.kind}-${slug(entry.summary || entry.id)}.md`,
    content,
    "text/markdown",
  );
}

export function exportComparisonJson(comparison: PipelineComparison) {
  triggerDownload(
    `comparison-${comparison.left?.category ?? "pipeline"}-${comparison.left?.id ?? "left"}-${comparison.right?.id ?? "right"}.json`,
    JSON.stringify(comparison, null, 2),
    "application/json",
  );
}

export function exportComparisonMarkdown(comparison: PipelineComparison) {
  const lines = [
    `# Comparison ${comparison.left?.category ?? "pipeline"}`,
    "",
    `- Left: ${comparison.left?.summary ?? "-"}`,
    `- Right: ${comparison.right?.summary ?? "-"}`,
    "",
    "## Summary",
    "",
    "| Field | Left | Right | Changed |",
    "| --- | --- | --- | --- |",
    ...comparison.rows.map(
      (row) =>
        `| ${row.label} | ${row.left.replace(/\|/g, "\\|")} | ${row.right.replace(/\|/g, "\\|")} | ${row.changed ? "Yes" : "No"} |`,
    ),
    "",
    "## Left JSON",
    "```json",
    JSON.stringify(comparison.left?.responseData ?? null, null, 2),
    "```",
    "",
    "## Right JSON",
    "```json",
    JSON.stringify(comparison.right?.responseData ?? null, null, 2),
    "```",
  ];

  triggerDownload(
    `comparison-${comparison.left?.category ?? "pipeline"}.md`,
    lines.join("\n"),
    "text/markdown",
  );
}
