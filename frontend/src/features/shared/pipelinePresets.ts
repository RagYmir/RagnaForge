import { defaultConfigPath } from "./requestBuilders";

export interface FormPreset<T> {
  key: string;
  label: string;
  description: string;
  value: T;
}

export interface ItemFormValues {
  configPath: string;
  aegisName: string;
  displayName: string;
  resourceName: string;
  type: string;
  buy: string;
  sell: string;
  weight: string;
  description: string;
  assetGrfContainer: string;
  scanGrfAssets: boolean;
}

export interface EquipmentFormValues extends ItemFormValues {
  locations: string;
  viewId: string;
  visualCategory: string;
  clientSymbolName: string;
  clientSpriteName: string;
  weaponBaseType: string;
  visualTheme: string;
}

export interface NpcFormValues {
  configPath: string;
  name: string;
  mapName: string;
  x: string;
  y: string;
  dir: string;
  sprite: string;
  scriptBody: string;
  assetGrfContainer: string;
  scanGrfAssets: boolean;
}

export interface MonsterFormValues {
  configPath: string;
  aegisName: string;
  displayName: string;
  spriteName: string;
  level: string;
  hp: string;
  mapName: string;
  amount: string;
  respawn: string;
  dropsText: string;
  skillsText: string;
  spawnsText: string;
}

export interface MapFormValues {
  configPath: string;
  mapName: string;
  assetGrfContainer: string;
  scanGrfAssets: boolean;
}

export const defaultItemFormValues: ItemFormValues = {
  configPath: defaultConfigPath,
  aegisName: "",
  displayName: "",
  resourceName: "",
  type: "Etc",
  buy: "0",
  sell: "0",
  weight: "0",
  description: "",
  assetGrfContainer: "",
  scanGrfAssets: false,
};

export const defaultEquipmentFormValues: EquipmentFormValues = {
  ...defaultItemFormValues,
  type: "Armor",
  locations: "Head_Top",
  viewId: "0",
  visualCategory: "Headgear",
  clientSymbolName: "",
  clientSpriteName: "",
  weaponBaseType: "",
  visualTheme: "",
};

export const defaultNpcFormValues: NpcFormValues = {
  configPath: defaultConfigPath,
  name: "",
  mapName: "",
  x: "150",
  y: "150",
  dir: "2",
  sprite: "",
  scriptBody: 'mes "Ola";\nclose;',
  assetGrfContainer: "",
  scanGrfAssets: false,
};

export const defaultMonsterFormValues: MonsterFormValues = {
  configPath: defaultConfigPath,
  aegisName: "",
  displayName: "",
  spriteName: "",
  level: "1",
  hp: "100",
  mapName: "",
  amount: "1",
  respawn: "5000",
  dropsText: "",
  skillsText: "",
  spawnsText: "",
};

export const defaultMapFormValues: MapFormValues = {
  configPath: defaultConfigPath,
  mapName: "",
  assetGrfContainer: "",
  scanGrfAssets: false,
};

export const itemPresets: FormPreset<ItemFormValues>[] = [
  {
    key: "item-etc-simple",
    label: "Item Etc simples",
    description: "Preset seguro para item basico sem asset extra.",
    value: {
      ...defaultItemFormValues,
      aegisName: "RF_ETC_SIMPLE",
      displayName: "RagnaForge Etc Simple",
      resourceName: "RF_ETC_SIMPLE",
      description: "Linha 1\nLinha 2",
      buy: "10",
      sell: "5",
      weight: "10",
    },
  },
  {
    key: "item-with-resource",
    label: "Item com resource",
    description: "Preenche identificacao e resource para validar client-side basico.",
    value: {
      ...defaultItemFormValues,
      aegisName: "RF_RESOURCE_ITEM",
      displayName: "RagnaForge Resource Item",
      resourceName: "RF_RESOURCE_ITEM",
      description: "Resource explicitado para lookup no Patch.",
    },
  },
  {
    key: "item-grf-asset",
    label: "Item com asset GRF",
    description: "Exemplo para testar lookup por container GRF.",
    value: {
      ...defaultItemFormValues,
      aegisName: "RF_GRF_ITEM",
      displayName: "RagnaForge GRF Item",
      resourceName: "RF_GRF_ITEM",
      assetGrfContainer: "data.grf",
      scanGrfAssets: true,
      description: "Lookup de asset em GRF habilitado.",
    },
  },
  {
    key: "item-hybrid-client",
    label: "Item client-side hibrido",
    description: "Simula um caso onde itemInfo e legado podem coexistir.",
    value: {
      ...defaultItemFormValues,
      aegisName: "RF_HYBRID_ITEM",
      displayName: "RagnaForge Hybrid Item",
      resourceName: "RF_HYBRID_ITEM",
      type: "Etc",
      description: "Usar para auditar ClientSideMode e bloqueios hibridos.",
      scanGrfAssets: true,
    },
  },
];

export const equipmentPresets: FormPreset<EquipmentFormValues>[] = [
  {
    key: "equipment-headgear",
    label: "Headgear",
    description: "Preset de headgear simples com foco em ViewID.",
    value: {
      ...defaultEquipmentFormValues,
      aegisName: "RF_HEADGEAR",
      displayName: "RagnaForge Headgear",
      resourceName: "RF_HEADGEAR",
      viewId: "4500",
      visualCategory: "Headgear",
      clientSymbolName: "RF_HEADGEAR",
      clientSpriteName: "RF_HEADGEAR",
    },
  },
  {
    key: "equipment-robe",
    label: "Robe",
    description: "Preset de robe para validar planos visuais e asset lookup.",
    value: {
      ...defaultEquipmentFormValues,
      aegisName: "RF_ROBE",
      displayName: "RagnaForge Robe",
      resourceName: "RF_ROBE",
      type: "Armor",
      locations: "Garment",
      viewId: "4700",
      visualCategory: "Robe",
      clientSymbolName: "RF_ROBE",
      clientSpriteName: "RF_ROBE",
      visualTheme: "DarkRobe",
    },
  },
  {
    key: "equipment-weapon",
    label: "Weapon",
    description: "Preset de arma com base type explicitado.",
    value: {
      ...defaultEquipmentFormValues,
      aegisName: "RF_WEAPON",
      displayName: "RagnaForge Weapon",
      resourceName: "RF_WEAPON",
      type: "Weapon",
      locations: "Right_Hand",
      viewId: "4800",
      visualCategory: "Weapon",
      clientSymbolName: "RF_WEAPON",
      clientSpriteName: "RF_WEAPON",
      weaponBaseType: "Sword",
    },
  },
  {
    key: "equipment-shield-restricted",
    label: "Shield restrito",
    description: "Preset para testar restricoes visuais de shield custom.",
    value: {
      ...defaultEquipmentFormValues,
      aegisName: "RF_SHIELD",
      displayName: "RagnaForge Shield",
      resourceName: "RF_SHIELD",
      type: "Armor",
      locations: "Left_Hand",
      viewId: "4900",
      visualCategory: "Shield",
      clientSymbolName: "RF_SHIELD",
      clientSpriteName: "RF_SHIELD",
    },
  },
];

export const npcPresets: FormPreset<NpcFormValues>[] = [
  {
    key: "npc-standard-sprite",
    label: "NPC sprite padrao",
    description: "Preset para NPC com sprite padrao.",
    value: {
      ...defaultNpcFormValues,
      name: "RF_WARPER",
      mapName: "prontera",
      sprite: "4_M_MERCHANT",
      scriptBody: 'mes "Bem-vindo";\nclose;',
    },
  },
  {
    key: "npc-custom-sprite",
    label: "NPC sprite custom",
    description: "Preset para lookup de sprite custom em Patch/GRF.",
    value: {
      ...defaultNpcFormValues,
      name: "RF_CUSTOM_NPC",
      mapName: "prontera",
      sprite: "RF_CUSTOM_NPC",
      assetGrfContainer: "data.grf",
      scanGrfAssets: true,
      scriptBody: 'mes "Sprite custom";\nclose;',
    },
  },
  {
    key: "npc-identity-plan",
    label: "NPC com identity plan",
    description: "Preset para inspecionar jobname/jobidentity/npcidentity.",
    value: {
      ...defaultNpcFormValues,
      name: "RF_IDENTITY_NPC",
      mapName: "izlude",
      sprite: "RF_IDENTITY_NPC",
      scanGrfAssets: true,
      scriptBody: 'mes "Identity plan";\nclose;',
    },
  },
];

export const monsterPresets: FormPreset<MonsterFormValues>[] = [
  {
    key: "monster-simple",
    label: "Monstro simples",
    description: "Preset minimo para validar base e stats.",
    value: {
      ...defaultMonsterFormValues,
      aegisName: "RF_MONSTER_SIMPLE",
      displayName: "RagnaForge Simple",
      spriteName: "PORING",
      mapName: "prontera",
    },
  },
  {
    key: "monster-drops",
    label: "Monstro com drops",
    description: "Preenche drops comuns e MVP para auditar grids.",
    value: {
      ...defaultMonsterFormValues,
      aegisName: "RF_MONSTER_DROP",
      displayName: "RagnaForge Drop",
      spriteName: "PORING",
      mapName: "pay_fild01",
      dropsText: "item=Red_Potion,chance=2500,quantity=1;\nitem=Golden_Apple,chance=500,mvp=true",
    },
  },
  {
    key: "monster-skills",
    label: "Monstro com skills",
    description: "Preset com multiplas skills para validar parsing.",
    value: {
      ...defaultMonsterFormValues,
      aegisName: "RF_MONSTER_SKILL",
      displayName: "RagnaForge Skill",
      spriteName: "PORING",
      mapName: "pay_fild02",
      skillsText: "id=1,level=5,state=any,rate=8000,target=target;\nid=28,level=10,state=idle,rate=3000,target=self",
    },
  },
  {
    key: "monster-advanced-spawns",
    label: "Monstro com spawns avancados",
    description: "Preset com spawns adicionais e labels.",
    value: {
      ...defaultMonsterFormValues,
      aegisName: "RF_MONSTER_SPAWN",
      displayName: "RagnaForge Spawn",
      spriteName: "PORING",
      mapName: "gef_fild01",
      spawnsText:
        "map=gef_fild01,amount=5,x=120,y=90,areaX=20,areaY=20,respawn=6000,label=FieldA;\nmap=gef_fild02,amount=2,x=150,y=180,event=OnSpawn,respawn=9000",
    },
  },
];

export const mapPresets: FormPreset<MapFormValues>[] = [
  {
    key: "map-existing-block",
    label: "Mapa existente",
    description: "Preset para verificar bloqueio por existencia de mapa.",
    value: {
      ...defaultMapFormValues,
      mapName: "prontera",
    },
  },
  {
    key: "map-new-grf",
    label: "Mapa novo por GRF",
    description: "Preset para lookup seguro em GRF.",
    value: {
      ...defaultMapFormValues,
      mapName: "rf_testmap",
      assetGrfContainer: "data.grf",
      scanGrfAssets: true,
    },
  },
  {
    key: "map-dependencies",
    label: "Mapa com dependencias",
    description: "Preset para inspecionar assets, ambiguidades e map_cache.",
    value: {
      ...defaultMapFormValues,
      mapName: "rf_dependencymap",
      scanGrfAssets: true,
    },
  },
];
