import type { DiffEntry } from "../../api/types";

export interface DiffEntryGroup {
  key: string;
  label: string;
  entries: DiffEntry[];
}

const groups = [
  { key: "server", label: "Server-side" },
  { key: "client", label: "Client-side" },
  { key: "assets", label: "Assets" },
  { key: "config", label: "Config" },
  { key: "map-cache", label: "Map cache" },
  { key: "other", label: "Unknown/Other" },
] satisfies Array<{ key: string; label: string }>;

function classifyEntry(targetPath: string) {
  const path = targetPath.toLowerCase();

  if (path.includes("map_cache") || path.endsWith("map_cache.dat")) {
    return "map-cache";
  }

  if (
    path.includes("iteminfo") ||
    path.includes("accname") ||
    path.includes("accessory") ||
    path.includes("jobname") ||
    path.includes("identity") ||
    path.includes("spriterobe") ||
    path.includes("weapontable") ||
    path.includes("idnum2") ||
    path.includes("num2item") ||
    path.endsWith(".lua") ||
    path.endsWith(".lub") ||
    path.endsWith(".txt")
  ) {
    return "client";
  }

  if (
    path.endsWith(".spr") ||
    path.endsWith(".act") ||
    path.endsWith(".bmp") ||
    path.endsWith(".tga") ||
    path.endsWith(".gat") ||
    path.endsWith(".gnd") ||
    path.endsWith(".rsw") ||
    path.endsWith(".rsm") ||
    path.endsWith(".wav")
  ) {
    return "assets";
  }

  if (
    path.includes("map_index") ||
    path.includes("maps_athena") ||
    path.endsWith(".conf") ||
    path.includes("/conf/") ||
    path.includes("\\conf\\")
  ) {
    return "config";
  }

  if (
    path.includes("db/") ||
    path.includes("\\db\\") ||
    path.includes("npc/") ||
    path.includes("\\npc\\") ||
    path.includes("mob_")
  ) {
    return "server";
  }

  return "other";
}

export function groupDiffEntries(entries?: DiffEntry[]): DiffEntryGroup[] {
  const grouped = new Map<string, DiffEntry[]>();

  (entries ?? []).forEach((entry) => {
    const key = classifyEntry(entry.targetPath);
    const current = grouped.get(key) ?? [];
    current.push(entry);
    grouped.set(key, current);
  });

  return groups
    .map((group) => ({
      ...group,
      entries: grouped.get(group.key) ?? [],
    }))
    .filter((group) => group.entries.length > 0);
}
