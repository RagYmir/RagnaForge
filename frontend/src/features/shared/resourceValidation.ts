import type { ClientPlan, PipelineReport } from "../../api/types";
import type { DependencyTreeItem } from "../../components/DependencyTree";
import type { PassiveAssetPreviewItem } from "../../components/PassiveAssetPreviewPanel";
import type { ValidationIssueRow } from "../../components/ValidationIssueTable";
import type { PipelineCategory, PipelineHistoryEntry } from "./localHistory";
import { asRecord, asRecordArray, asStringArray, toBoolean, toStringValue } from "./viewData";

export interface ValidationDependencyGroup {
  title: string;
  items: DependencyTreeItem[];
}

export interface ValidationCenterData {
  issues: ValidationIssueRow[];
  previewItems: PassiveAssetPreviewItem[];
  dependencyGroups: ValidationDependencyGroup[];
}

const issueCategoryTitles = [
  "Server DB",
  "Client-side",
  "Assets",
  "GRF",
  "Mapas",
  "Bytecode",
  "Conflitos",
  "Unknown Item/Apple",
  "ViewID",
  "AegisName/ID",
  "Map cache",
] as const;

function historyLabel(category: PipelineCategory) {
  switch (category) {
    case "items":
      return "Item";
    case "equipment":
      return "Equipamento";
    case "npcs":
      return "NPC";
    case "monsters":
      return "Monstro";
    case "maps":
      return "Mapa";
    case "grf":
      return "GRF";
    default:
      return "Validation";
  }
}

function categoryFromMessage(message: string, fallback = "Server DB") {
  const normalized = message.toLowerCase();

  if (normalized.includes("bytecode") || normalized.includes(".lub")) {
    return "Bytecode";
  }

  if (normalized.includes("viewid")) {
    return "ViewID";
  }

  if (
    normalized.includes("aegis") ||
    normalized.includes("duplicate id") ||
    normalized.includes("id collision") ||
    normalized.includes("duplicate visual")
  ) {
    return "AegisName/ID";
  }

  if (normalized.includes("unknown item") || normalized.includes("apple")) {
    return "Unknown Item/Apple";
  }

  if (normalized.includes("map_cache")) {
    return "Map cache";
  }

  if (
    normalized.includes("asset") ||
    normalized.includes("sprite") ||
    normalized.includes("texture") ||
    normalized.includes("model") ||
    normalized.includes("sound") ||
    normalized.includes("effect") ||
    normalized.includes(".bmp") ||
    normalized.includes(".spr") ||
    normalized.includes(".act")
  ) {
    return "Assets";
  }

  if (
    normalized.includes("jobname") ||
    normalized.includes("identity") ||
    normalized.includes("iteminfo") ||
    normalized.includes("client-side") ||
    normalized.includes("legacytxt") ||
    normalized.includes("iteminfo")
  ) {
    return "Client-side";
  }

  if (
    normalized.includes(".grf") ||
    normalized.includes("localscan") ||
    normalized.includes("livescan") ||
    normalized.includes("container")
  ) {
    return "GRF";
  }

  if (
    normalized.includes(".rsw") ||
    normalized.includes(".gnd") ||
    normalized.includes(".gat") ||
    normalized.includes("mapa") ||
    normalized.includes("map ")
  ) {
    return "Mapas";
  }

  if (normalized.includes("conflict") || normalized.includes("duplicate") || normalized.includes("blocked")) {
    return "Conflitos";
  }

  return fallback;
}

function tagsFromMessage(message: string) {
  const normalized = message.toLowerCase();
  const tags: string[] = [];

  if (normalized.includes("bytecode") || normalized.includes(".lub")) {
    tags.push("bytecode", "blocked");
  }
  if (normalized.includes("asset") || normalized.includes("sprite") || normalized.includes(".bmp") || normalized.includes(".spr") || normalized.includes(".act")) {
    tags.push("missing-asset");
  }
  if (normalized.includes("conflict") || normalized.includes("duplicate")) {
    tags.push("conflict");
  }
  if (normalized.includes("blocked") || normalized.includes("unsafe") || normalized.includes("missing") || normalized.includes("ambiguous")) {
    tags.push("blocked");
  }

  return Array.from(new Set(tags));
}

function blocksFutureApply(issue: Pick<ValidationIssueRow, "severity" | "tags">) {
  return issue.severity === "danger" || (issue.tags ?? []).includes("blocked");
}

function pushIssue(
  issues: ValidationIssueRow[],
  issue: ValidationIssueRow,
) {
  const nextIssue = {
    ...issue,
    blocksFutureApply: issue.blocksFutureApply ?? blocksFutureApply(issue),
  };

  if (issues.some((entry) => entry.key === nextIssue.key)) {
    return;
  }

  issues.push(nextIssue);
}

function statusFromPlanFormat(format: string) {
  const normalized = format.toLowerCase();

  if (normalized.includes("binary")) {
    return "blocked" as const;
  }
  if (normalized.includes("unknown")) {
    return "blocked" as const;
  }
  if (normalized.includes("missing")) {
    return "missing" as const;
  }

  return "read-only" as const;
}

function pushPreviewItem(items: PassiveAssetPreviewItem[], item: PassiveAssetPreviewItem) {
  if (items.some((entry) => entry.key === item.key)) {
    return;
  }

  items.push(item);
}

function addArrayMessages(
  issues: ValidationIssueRow[],
  entry: PipelineHistoryEntry,
  values: string[],
  severity: ValidationIssueRow["severity"],
  origin: string,
  fallbackCategory: string,
  recommendedAction: string,
) {
  values.forEach((message, index) =>
    pushIssue(issues, {
      key: `${entry.id}-${origin}-${index}-${message}`,
      severity,
      category: categoryFromMessage(message, fallbackCategory),
      entity: historyLabel(entry.category),
      file: String(entry.payload.configPath ?? "-"),
      origin,
      message,
      recommendedAction,
      tags: tagsFromMessage(message),
      raw: { historyId: entry.id, message },
    }),
  );
}

function addClientPlanIssues(
  issues: ValidationIssueRow[],
  previews: PassiveAssetPreviewItem[],
  entry: PipelineHistoryEntry,
  plan: ClientPlan | undefined,
  planName: string,
) {
  if (!plan) {
    return;
  }

  addArrayMessages(
    issues,
    entry,
    plan.blockReasons ?? [],
    "danger",
    `${planName}.block`,
    "Client-side",
    "Revisar o plano client-side antes de considerar o resultado pronto.",
  );

  addArrayMessages(
    issues,
    entry,
    plan.validationErrors ?? [],
    "danger",
    `${planName}.validation`,
    "Client-side",
    "Corrigir a validacao client-side antes de seguir.",
  );

  addArrayMessages(
    issues,
    entry,
    plan.validationWarnings ?? [],
    "warning",
    `${planName}.validation`,
    "Client-side",
    "Inspecionar os avisos de validacao client-side.",
  );

  (plan.bytecodeBlockedFiles ?? []).forEach((file, index) => {
    pushIssue(issues, {
      key: `${entry.id}-${planName}-bytecode-${index}-${file}`,
      severity: "danger",
      category: "Bytecode",
      entity: historyLabel(entry.category),
      file,
      origin: `${planName}.bytecode`,
      message: `${file} bloqueado por bytecode ou formato inseguro.`,
      recommendedAction: "Manter em modo somente leitura e resolver manualmente fora desta UI.",
      tags: ["bytecode", "blocked"],
      raw: plan,
      blocksFutureApply: true,
    });

    pushPreviewItem(previews, {
      key: `${entry.id}-${planName}-bytecode-preview-${file}`,
      name: file.split(/[\\/]/).pop() ?? file,
      path: file,
      expectedPath: file,
      category: planName,
      type: "client identity",
      origin: "Client-side plan",
      provenance: "ReadOnly",
      status: "blocked",
      note: "Bytecode bloqueado; nenhum contorno e permitido pela UI.",
    });
  });

  (plan.filesDetected ?? []).forEach((file) => {
    const status = statusFromPlanFormat(String(file.format));
    pushPreviewItem(previews, {
      key: `${entry.id}-${planName}-file-${file.logicalName}-${file.path}`,
      name: file.logicalName,
      path: file.path,
      expectedPath: file.path,
      category: planName,
      type: file.format,
      origin: "Patch",
      provenance: file.selected ? "Selected target" : "Detected file",
      status,
      note: file.supportedForApply === false ? "Somente leitura nesta fase." : "Arquivo detectado sem escrita.",
    });
  });
}

function inferPreviewStatus(value: Record<string, unknown>) {
  const sourceKind = toStringValue(value.sourceKind ?? value.origin ?? value.provenance, "Unknown");
  const normalized = sourceKind.toLowerCase();

  if (normalized.includes("ambiguous")) {
    return "ambiguous" as const;
  }

  if (normalized.includes("blocked")) {
    return "blocked" as const;
  }

  if (toBoolean(value.resolved, false) === false || normalized.includes("missing")) {
    return "missing" as const;
  }

  if (toBoolean(value.needsCopy, false) || normalized.includes("copy")) {
    return "needs-copy-future" as const;
  }

  return "resolved" as const;
}

function inferResourceCategory(label: string) {
  const normalized = label.toLowerCase();

  if (normalized.includes(".bmp") || normalized.includes("inventory") || normalized.includes("collection") || normalized.includes("card")) {
    return "Item visuals";
  }
  if (normalized.includes(".spr") || normalized.includes(".act") || normalized.includes("npc") || normalized.includes("monster")) {
    return "Sprite/ACT";
  }
  if (normalized.includes(".rsw") || normalized.includes(".gnd") || normalized.includes(".gat")) {
    return "Map core";
  }
  if (normalized.includes(".rsm")) {
    return "Map models";
  }
  if (normalized.includes(".wav")) {
    return "Map sounds";
  }

  return "Resource";
}

function addLookupPreviewItems(
  previews: PassiveAssetPreviewItem[],
  entry: PipelineHistoryEntry,
  label: string,
  lookup: Record<string, unknown> | undefined,
  expectedPath?: string,
) {
  if (!lookup) {
    return;
  }

  const matches = asRecordArray(lookup.matches);
  if (!matches.length && expectedPath) {
    pushPreviewItem(previews, {
      key: `${entry.id}-${label}-missing-${expectedPath}`,
      name: expectedPath.split(/[\\/]/).pop() ?? expectedPath,
      path: expectedPath,
      expectedPath,
      category: inferResourceCategory(expectedPath),
      type: "lookup",
      origin: toStringValue(lookup.source, "Unknown"),
      provenance: toStringValue(lookup.source, "Unknown"),
      status: "missing",
      note: `${label} sem match confirmado nesta coleta read-only.`,
    });
    return;
  }

  matches.slice(0, 8).forEach((match, index) => {
    const relativePath = toStringValue(match.relativePath, expectedPath ?? "-");
    pushPreviewItem(previews, {
      key: `${entry.id}-${label}-${index}-${relativePath}`,
      name: relativePath.split(/[\\/]/).pop() ?? relativePath,
      path: relativePath,
      expectedPath: expectedPath ?? relativePath,
      category: inferResourceCategory(relativePath),
      type: toStringValue(match.extension, "lookup"),
      origin: toStringValue(match.containerPath, toStringValue(lookup.source, "Unknown")),
      provenance: toStringValue(lookup.source, "Unknown"),
      status: matches.length > 1 ? "ambiguous" : "resolved",
      note: matches.length > 1 ? "Mais de um match retornado." : "Origem somente leitura.",
    });
  });
}

function addAssetPlanData(
  issues: ValidationIssueRow[],
  previews: PassiveAssetPreviewItem[],
  entry: PipelineHistoryEntry,
  assetPlans: Record<string, unknown>[],
) {
  assetPlans.forEach((plan, index) => {
    const path = toStringValue(
      plan.referencePath ?? plan.assetPath ?? plan.targetPath ?? plan.relativePath ?? plan.sourcePath,
      `asset-plan-${index}`,
    );
    const status = inferPreviewStatus(plan);
    const origin = toStringValue(plan.sourceKind ?? plan.origin, "Unknown");

    pushPreviewItem(previews, {
      key: `${entry.id}-asset-plan-${index}-${path}`,
      name: path.split(/[\\/]/).pop() ?? path,
      path,
      expectedPath: toStringValue(plan.targetPath ?? plan.referencePath, path),
      category: inferResourceCategory(path),
      type: toStringValue(plan.category, "asset-plan"),
      origin,
      provenance: origin,
      status,
      note: toStringValue(plan.note ?? plan.reason, "Plano de asset em modo read-only."),
    });

    if (status !== "resolved") {
      pushIssue(issues, {
        key: `${entry.id}-asset-plan-issue-${index}-${path}`,
        severity: status === "ambiguous" ? "warning" : "danger",
        category: status === "ambiguous" ? "Assets" : status === "blocked" ? "Conflitos" : "Mapas",
        entity: historyLabel(entry.category),
        file: path,
        origin,
        message:
          status === "ambiguous"
            ? `${path} tem resolucao ambigua.`
            : status === "needs-copy-future"
              ? `${path} depende de copia futura segura.`
              : `${path} nao esta pronto para uso seguro.`,
        recommendedAction: "Revisar a proveniencia e manter o fluxo somente leitura ate a dependencia ficar clara.",
        tags: status === "ambiguous" ? ["missing-asset"] : ["blocked", "missing-asset"],
        raw: plan,
        blocksFutureApply: status !== "needs-copy-future" ? true : false,
      });
    }
  });
}

function addGenericCategoryIssues(
  issues: ValidationIssueRow[],
  entry: PipelineHistoryEntry,
  report: Record<string, unknown>,
) {
  const itemPayload = asRecord(entry.payload);

  if (entry.category === "items" && itemPayload?.resourceName) {
    const clientPlan = report.clientSidePlan as ClientPlan | undefined;
    if (clientPlan?.canApply === false || (clientPlan?.blockReasons?.length ?? 0) > 0) {
      pushIssue(issues, {
        key: `${entry.id}-unknown-item-risk`,
        severity: "warning",
        category: "Unknown Item/Apple",
        entity: "Item",
        file: toStringValue(itemPayload.resourceName),
        origin: "clientSidePlan",
        message: "Client-side incompleto: o item pode aparecer como Unknown Item ou Apple no client.",
        recommendedAction: "Resolver ItemInfo/TXT legado, lookup de resource e bloqueios antes de considerar o item funcional.",
        tags: ["missing-asset", "blocked"],
        raw: report.clientSidePlan,
        blocksFutureApply: true,
      });
    }
  }

  if (entry.category === "equipment" && report.shieldRestriction) {
    pushIssue(issues, {
      key: `${entry.id}-shield-restriction`,
      severity: "warning",
      category: "Assets",
      entity: "Equipamento",
      file: toStringValue(itemPayload?.clientSpriteName ?? itemPayload?.resourceName, "-"),
      origin: "shieldRestriction",
      message: `Shield restriction: ${toStringValue(report.shieldRestriction)}.`,
      recommendedAction: "Manter restricoes visuais visiveis e nao tentar forcar shield custom pela UI.",
      tags: ["blocked"],
      raw: report.shieldRestriction,
      blocksFutureApply: true,
    });
  }

  if (entry.category === "npcs" && report.serverCanApply === false) {
    pushIssue(issues, {
      key: `${entry.id}-npc-server-block`,
      severity: "danger",
      category: "Server DB",
      entity: "NPC",
      file: toStringValue(itemPayload?.mapName, "-"),
      origin: "serverCanApply",
      message: "Server-side do NPC nao ficou apto no dry-run.",
      recommendedAction: "Corrigir a validacao server-side antes de considerar a proposta pronta.",
      tags: ["blocked"],
      raw: report,
      blocksFutureApply: true,
    });
  }

  if (entry.category === "maps" && report.mapCachePlan) {
    pushIssue(issues, {
      key: `${entry.id}-map-cache-plan`,
      severity: "info",
      category: "Map cache",
      entity: "Mapa",
      file: "map_cache.dat",
      origin: "mapCachePlan",
      message: "Map cache plan detectado; qualquer etapa futura continua dependendo de staging seguro.",
      recommendedAction: "Validar map_cache, map_index e dependencias antes de qualquer politica futura de escrita.",
      tags: [],
      raw: report.mapCachePlan,
      blocksFutureApply: false,
    });
  }
}

export function collectValidationCenterData(entries: PipelineHistoryEntry[]): ValidationCenterData {
  const issues: ValidationIssueRow[] = [];
  const previewItems: PassiveAssetPreviewItem[] = [];

  entries.forEach((entry) => {
    const report = asRecord(entry.responseData) as PipelineReport | undefined;
    if (!report) {
      return;
    }

    addArrayMessages(
      issues,
      entry,
      asStringArray(report.errors),
      "danger",
      "response.errors",
      "Server DB",
      "Resolver o erro antes de confiar no resultado.",
    );
    addArrayMessages(
      issues,
      entry,
      asStringArray(report.warnings),
      "warning",
      "response.warnings",
      "Server DB",
      "Revisar o warning antes de usar o resultado como base.",
    );
    addArrayMessages(
      issues,
      entry,
      asStringArray(report.validationErrors),
      "danger",
      "postWriteValidation",
      "Client-side",
      "Ajustar a proposta ate a validacao final ficar limpa.",
    );
    addArrayMessages(
      issues,
      entry,
      asStringArray(report.validationWarnings),
      "warning",
      "postWriteValidation",
      "Client-side",
      "Inspecionar a validacao final antes de aceitar o resultado.",
    );
    addArrayMessages(
      issues,
      entry,
      asStringArray(report.postWriteValidationPlan),
      "info",
      "postWriteValidationPlan",
      "Client-side",
      "Usar este plano apenas como orientacao read-only.",
    );
    addArrayMessages(
      issues,
      entry,
      asStringArray(report.bytecodeBlocks),
      "danger",
      "bytecodeBlocks",
      "Bytecode",
      "Manter bytecode bloqueado fora de qualquer fluxo de escrita.",
    );

    addClientPlanIssues(issues, previewItems, entry, report.clientSidePlan, "ClientSidePlan");
    addClientPlanIssues(issues, previewItems, entry, report.visualClientSidePlan, "VisualClientSidePlan");
    addClientPlanIssues(issues, previewItems, entry, report.clientIdentityPlan, "ClientIdentityPlan");

    addLookupPreviewItems(
      previewItems,
      entry,
      "ItemAssetLookup",
      asRecord(report.itemAssetLookup),
      toStringValue(asRecord(entry.payload)?.resourceName, ""),
    );
    addLookupPreviewItems(
      previewItems,
      entry,
      "VisualAssetLookup",
      asRecord(report.visualAssetLookup),
      toStringValue(asRecord(entry.payload)?.clientSpriteName ?? asRecord(entry.payload)?.resourceName, ""),
    );
    addLookupPreviewItems(
      previewItems,
      entry,
      "AssetLookup",
      asRecord(report.assetLookup),
      toStringValue(asRecord(entry.payload)?.resourceName ?? asRecord(entry.payload)?.sprite, ""),
    );

    const spriteValidation = asRecord(asRecord(report)?.spriteValidation);
    addLookupPreviewItems(
      previewItems,
      entry,
      "NpcSpriteLookup",
      asRecord(spriteValidation?.assetLookup),
      toStringValue(asRecord(entry.payload)?.sprite, ""),
    );

    addAssetPlanData(issues, previewItems, entry, asRecordArray(report.assetPlans));
    addAssetPlanData(
      issues,
      previewItems,
      entry,
      asRecordArray(asRecord(report.dependencyScan)?.referencedAssets),
    );

    addGenericCategoryIssues(issues, entry, report as Record<string, unknown>);
  });

  const groupedByStatus: Array<{ title: string; status: PassiveAssetPreviewItem["status"] }> = [
    { title: "Resolved assets", status: "resolved" },
    { title: "Missing assets", status: "missing" },
    { title: "Ambiguous assets", status: "ambiguous" },
    { title: "Blocked resources", status: "blocked" },
    { title: "Read-only findings", status: "read-only" },
    { title: "Needs copy future", status: "needs-copy-future" },
  ];

  const dependencyGroups = groupedByStatus.map((group) => ({
    title: group.title,
    items: previewItems
      .filter((item) => item.status === group.status)
      .slice(0, 12)
      .map<DependencyTreeItem>((item) => ({
        label: item.name ?? item.path,
        hint: item.expectedPath && item.expectedPath !== item.path ? `Esperado: ${item.expectedPath}` : item.path,
        status: item.status,
        origin: item.origin,
        note: item.note ?? item.provenance,
      })),
  }));

  return {
    issues,
    previewItems,
    dependencyGroups,
  };
}

export function buildIssueCategorySummary(issues: ValidationIssueRow[]) {
  return issueCategoryTitles.map((title) => {
    const matching = issues.filter((issue) => issue.category === title);
    const hasDanger = matching.some((issue) => issue.severity === "danger");
    const hasWarning = matching.some((issue) => issue.severity === "warning");

    return {
      key: title.toLowerCase().replace(/[^a-z0-9]+/gi, "-"),
      label: title,
      subtitle: matching.length ? `${matching.length} item(ns)` : "Sem ocorrencias",
      status: hasDanger ? ("danger" as const) : hasWarning ? ("warning" as const) : matching.length ? ("good" as const) : ("neutral" as const),
      href: "#validation-center",
    };
  });
}

export interface ResourceValidationBadgeData {
  key: string;
  label: string;
  category: string;
  tone: "good" | "warning" | "danger" | "neutral";
}

export function buildResourceValidationBadges(
  issues: ValidationIssueRow[],
  previews: PassiveAssetPreviewItem[]
): ResourceValidationBadgeData[] {
  const badges: ResourceValidationBadgeData[] = [];

  const hasIssuesWith = (keywords: string[]) => issues.some(issue => keywords.some(k => (issue.message + issue.file + issue.category).toLowerCase().includes(k.toLowerCase())));
  const hasPreviewsWith = (keywords: string[]) => previews.some(p => keywords.some(k => (p.name + p.path + p.category).toLowerCase().includes(k.toLowerCase())));
  const previewStatusFor = (keywords: string[]) => {
    const matching = previews.filter(p => keywords.some(k => (p.name + p.path + p.category).toLowerCase().includes(k.toLowerCase())));
    if (!matching.length) return null;
    if (matching.some(p => p.status === "blocked")) return "danger";
    if (matching.some(p => p.status === "missing" || p.status === "ambiguous")) return "warning";
    return "good";
  };

  if (hasPreviewsWith(["item", "inventory", "collection", "card"]) || hasIssuesWith(["item", "apple"])) {
    badges.push({ key: "item-inv", category: "Itens", label: "inventory BMP", tone: previewStatusFor(["inventory"]) ?? "neutral" });
    badges.push({ key: "item-coll", category: "Itens", label: "collection BMP", tone: previewStatusFor(["collection"]) ?? "neutral" });
    badges.push({ key: "item-drag", category: "Itens", label: "drag .act/.spr", tone: previewStatusFor(["drag", "Item visuals"]) ?? "neutral" });
    badges.push({ key: "item-card", category: "Itens", label: "card illustration", tone: previewStatusFor(["card", "illustration"]) ?? "neutral" });
    badges.push({ key: "item-name", category: "Itens", label: "item resource name", tone: hasIssuesWith(["resource name"]) ? "warning" : "good" });
    badges.push({ key: "item-apple", category: "Itens", label: "risco de Unknown Item/Apple", tone: hasIssuesWith(["unknown item", "apple"]) ? "danger" : "good" });
  }

  if (hasPreviewsWith(["equipment", "headgear", "robe", "weapon", "shield", "accessory"]) || hasIssuesWith(["equipment", "viewid", "shield"])) {
    badges.push({ key: "eq-headgear", category: "Equipamentos", label: "headgear sprite", tone: previewStatusFor(["headgear"]) ?? "neutral" });
    badges.push({ key: "eq-acc", category: "Equipamentos", label: "accessoryid/accname", tone: previewStatusFor(["accessoryid", "accname", "accessory"]) ?? "neutral" });
    badges.push({ key: "eq-robe", category: "Equipamentos", label: "robe sprite", tone: previewStatusFor(["robe"]) ?? "neutral" });
    badges.push({ key: "eq-weap", category: "Equipamentos", label: "weapon sprite", tone: previewStatusFor(["weapon"]) ?? "neutral" });
    badges.push({ key: "eq-shield", category: "Equipamentos", label: "shield restriction", tone: hasIssuesWith(["shield restriction"]) ? "warning" : "good" });
    badges.push({ key: "eq-viewid", category: "Equipamentos", label: "ViewID duplicado", tone: hasIssuesWith(["viewid"]) ? "danger" : "good" });
    badges.push({ key: "eq-symbol", category: "Equipamentos", label: "símbolo inseguro", tone: hasIssuesWith(["símbolo inseguro", "unsafe symbol"]) ? "warning" : "good" });
    badges.push({ key: "eq-missing", category: "Equipamentos", label: "asset ausente", tone: hasIssuesWith(["asset ausente", "missing asset", "missing", "ausente"]) && hasIssuesWith(["equipment", "equipamento"]) ? "warning" : "good" });
    badges.push({ key: "eq-ambig", category: "Equipamentos", label: "asset ambíguo", tone: hasIssuesWith(["ambiguous", "ambíguo"]) && hasIssuesWith(["equipment", "equipamento"]) ? "warning" : "good" });
  }

  if (hasPreviewsWith(["npc", "sprite", "jobname", "jobidentity", "npcidentity"]) || hasIssuesWith(["npc"])) {
    badges.push({ key: "npc-std", category: "NPCs", label: "sprite padrão", tone: hasPreviewsWith(["sprite padrão", "standard sprite", "jobname"]) ? "good" : "neutral" });
    badges.push({ key: "npc-patch", category: "NPCs", label: "sprite custom no Patch", tone: hasPreviewsWith(["custom", "Patch"]) ? "good" : "neutral" });
    badges.push({ key: "npc-grf", category: "NPCs", label: "sprite custom em GRF", tone: hasPreviewsWith(["custom", "GRF", "LiveScan", "LocalIndex"]) ? "good" : "neutral" });
    badges.push({ key: "npc-ambig", category: "NPCs", label: "sprite ambíguo", tone: hasPreviewsWith(["ambiguous"]) && hasPreviewsWith(["npc", "sprite"]) ? "warning" : "neutral" });
    badges.push({ key: "npc-ident", category: "NPCs", label: "jobname/jobidentity/npcidentity", tone: hasIssuesWith(["jobname", "jobidentity", "npcidentity"]) ? "warning" : "good" });
    badges.push({ key: "npc-bytecode", category: "NPCs", label: "bytecode bloqueado", tone: hasIssuesWith(["bytecode"]) && hasIssuesWith(["npc"]) ? "danger" : "good" });
  }

  if (hasPreviewsWith(["monster", "mob"]) || hasIssuesWith(["monster", "mob", "drop", "skill", "spawn"])) {
    badges.push({ key: "mob-sprite", category: "Monstros", label: "sprite/class", tone: previewStatusFor(["monster", "sprite"]) ?? "neutral" });
    badges.push({ key: "mob-avail", category: "Monstros", label: "mob_avail", tone: hasIssuesWith(["mob_avail"]) ? "warning" : "good" });
    badges.push({ key: "mob-drop", category: "Monstros", label: "drops com item inexistente", tone: hasIssuesWith(["drop", "inexistente", "missing item"]) ? "danger" : "good" });
    badges.push({ key: "mob-skill", category: "Monstros", label: "skills inválidas", tone: hasIssuesWith(["skill", "inválida", "invalid skill"]) ? "danger" : "good" });
    badges.push({ key: "mob-spawn", category: "Monstros", label: "spawn em mapa inexistente", tone: hasIssuesWith(["spawn", "inexistente", "invalid map"]) ? "danger" : "good" });
    badges.push({ key: "mob-map", category: "Monstros", label: "mapa não registrado", tone: hasIssuesWith(["mapa", "não registrado", "unregistered map"]) ? "warning" : "good" });
  }

  if (hasPreviewsWith(["map", "rsw", "gnd", "gat", "texture", "model", "sound", "effect"]) || hasIssuesWith(["map"])) {
    badges.push({ key: "map-core", category: "Mapas", label: ".rsw/.gnd/.gat", tone: previewStatusFor(["rsw", "gnd", "gat"]) ?? "neutral" });
    badges.push({ key: "map-tex", category: "Mapas", label: "texturas", tone: previewStatusFor(["texture", "bmp", "tga"]) ?? "neutral" });
    badges.push({ key: "map-mod", category: "Mapas", label: "modelos", tone: previewStatusFor(["model", "rsm"]) ?? "neutral" });
    badges.push({ key: "map-snd", category: "Mapas", label: "sons", tone: previewStatusFor(["sound", "wav"]) ?? "neutral" });
    badges.push({ key: "map-eff", category: "Mapas", label: "efeitos", tone: previewStatusFor(["effect", "str"]) ?? "neutral" });
    badges.push({ key: "map-cache", category: "Mapas", label: "map_cache.dat", tone: hasIssuesWith(["map_cache"]) ? "warning" : "good" });
    badges.push({ key: "map-index", category: "Mapas", label: "map_index", tone: hasIssuesWith(["map_index"]) ? "warning" : "good" });
    badges.push({ key: "map-missing", category: "Mapas", label: "assets ausentes", tone: hasIssuesWith(["asset ausente", "missing", "ausente"]) && hasIssuesWith(["map", "texture", "model"]) ? "warning" : "good" });
    badges.push({ key: "map-ambig", category: "Mapas", label: "assets ambíguos", tone: hasIssuesWith(["asset ambíguo", "ambiguous", "ambíguo"]) && hasIssuesWith(["map"]) ? "warning" : "good" });
    badges.push({ key: "map-rename", category: "Mapas", label: "rename binário bloqueado", tone: hasIssuesWith(["rename binário", "binary rename"]) ? "danger" : "good" });
  }

  return badges;
}
