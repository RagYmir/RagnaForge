export const defaultConfigPath = "data/manifests/repositories.local.json";

export function splitLines(value: string) {
  return value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);
}

export function splitTokens(value: string) {
  return value
    .split(/[\n,;]/)
    .map((token) => token.trim())
    .filter(Boolean);
}

export function parseBoolean(value: string | undefined) {
  return value?.trim().toLowerCase() === "true";
}

export function parseNumber(value: string | undefined) {
  if (!value || !value.trim()) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function parseRecord(value: string) {
  return value
    .split(",")
    .map((pair) => pair.trim())
    .filter(Boolean)
    .reduce<Record<string, string>>((accumulator, pair) => {
      const separatorIndex = pair.indexOf("=");
      if (separatorIndex === -1) {
        return accumulator;
      }

      const key = pair.slice(0, separatorIndex).trim();
      const rawValue = pair.slice(separatorIndex + 1).trim();
      if (key) {
        accumulator[key] = rawValue;
      }

      return accumulator;
    }, {});
}

export function parseSequence(value: string) {
  return value
    .split(/[;\n]+/)
    .map((entry) => entry.trim())
    .filter(Boolean)
    .map(parseRecord);
}

export function buildMonsterDrops(value: string) {
  return parseSequence(value).map((entry) => {
    const itemToken = entry.item ?? entry.aegis ?? entry.itemAegisName ?? entry.id;
    const numericId = parseNumber(itemToken);
    return {
      itemId: numericId,
      itemAegisName: numericId ? undefined : itemToken,
      chance: parseNumber(entry.chance) ?? 100,
      quantity: parseNumber(entry.quantity),
      isMvp: parseBoolean(entry.mvp ?? entry.isMvp) ?? false,
      kind: entry.kind
    };
  });
}

export function buildMonsterSkills(value: string) {
  return parseSequence(value).map((entry) => ({
    skillId: parseNumber(entry.id ?? entry.skillId) ?? 0,
    skillLevel: parseNumber(entry.level ?? entry.skillLevel) ?? 1,
    state: entry.state ?? "any",
    rate: parseNumber(entry.rate) ?? 10000,
    castTimeMilliseconds: parseNumber(entry.castTime ?? entry.castTimeMilliseconds) ?? 0,
    delayMilliseconds: parseNumber(entry.delay ?? entry.delayMilliseconds) ?? 5000,
    cancelable: parseBoolean(entry.cancelable) ?? false,
    target: entry.target ?? "target",
    conditionType: entry.conditionType ?? "always",
    conditionValue: parseNumber(entry.conditionValue) ?? 0,
    value1: parseNumber(entry.value1),
    value2: parseNumber(entry.value2),
    value3: parseNumber(entry.value3),
    value4: parseNumber(entry.value4),
    value5: parseNumber(entry.value5),
    emotion: parseNumber(entry.emotion),
    chat: entry.chat,
    anchor: entry.anchor
  }));
}

export function buildMonsterSpawns(value: string) {
  return parseSequence(value).map((entry) => ({
    mapName: entry.map ?? entry.mapName,
    x: parseNumber(entry.x) ?? 0,
    y: parseNumber(entry.y) ?? 0,
    areaX: parseNumber(entry.areaX) ?? 0,
    areaY: parseNumber(entry.areaY) ?? 0,
    amount: parseNumber(entry.amount),
    respawnMilliseconds: parseNumber(entry.respawn ?? entry.respawnMilliseconds),
    label: entry.label,
    eventLabel: entry.event ?? entry.eventLabel,
    randomize: parseBoolean(entry.randomize) ?? false
  }));
}
