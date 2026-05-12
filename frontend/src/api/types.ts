export interface ApiResponse<T> {
  success: boolean;
  data: T;
  warnings: string[];
  errors: string[];
  generatedAt: string;
  correlationId: string;
  operationKind: string;
  readOnlyMode: boolean;
  durationMs: number;
}

export interface ApiProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
  instance?: string;
  errorCode?: string;
  correlationId?: string;
  path?: string;
  timestamp?: string;
  validationErrors?: Record<string, string[]>;
}

export interface ApiCapability {
  category: string;
  readOnly: boolean;
  dryRun: boolean;
  diffPreview: boolean;
  apply: boolean;
  rollback: boolean;
  notes: string;
}

export interface StatusData {
  service: string;
  mode: string;
  workspaceRoot: string;
  readOnlyMode: boolean;
  applyEndpointsEnabled: boolean;
  rollbackEndpointsEnabled: boolean;
  requireApiKey: boolean;
  apiKeyHeaderName: string;
  maxRequestBodyBytes: number;
  maxGrfContainersPerRequest: number;
  maxDiffHunksPerResponse: number;
  generatedAtUtc: string;
  disabledWriteOperations: string[];
  capabilities: ApiCapability[];
  [key: string]: unknown;
}

export interface ManifestValidationIssue {
  severity: string;
  code: string;
  message: string;
}

export interface ConfigValidateData {
  manifestPath: string;
  validation: {
    isValid: boolean;
    issues: ManifestValidationIssue[];
  };
  manifest: {
    schemaVersion: string;
    createdAtUtc: string;
    updatedAtUtc: string;
    paths: Record<string, string>;
    episodeProfile: Record<string, unknown>;
    isProgressive: boolean;
    clientDateStatus: string;
    notes: string[];
  };
  [key: string]: unknown;
}

export interface DiffEntry {
  targetPath: string;
  changeKind: string;
  exists: boolean;
  beforeLineCount?: number;
  afterLineCount?: number;
  unifiedDiff?: string;
  preview?: string;
}

export interface DiffPreview {
  fileCount: number;
  createdCount?: number;
  updatedCount?: number;
  entries: DiffEntry[];
}

export interface FileDetection {
  logicalName: string;
  path: string;
  format: string;
  exists: boolean;
  selected?: boolean;
  supportedForApply?: boolean;
}

export interface ClientPlan {
  required?: boolean;
  canApply?: boolean;
  blockReasons?: string[];
  clientSideMode?: string;
  filesDetected?: FileDetection[];
  fileFormats?: string[];
  itemInfoDetected?: boolean;
  legacyTablesDetected?: boolean;
  hybridClientDetected?: boolean;
  supportedTargets?: string[];
  unsupportedTargets?: string[];
  bytecodeBlockedFiles?: string[];
  proposedRegistrations?: string[];
  existingRegistrations?: string[];
  proposedChanges?: DiffEntry[];
  diffHunks?: string[];
  applyTargets?: string[];
  rollbackTargets?: string[];
  postWriteValidationPlan?: string[];
  validationWarnings?: string[];
  validationErrors?: string[];
  applyReadiness?: string;
  [key: string]: unknown;
}

export interface AssetLookupMatch {
  containerPath?: string;
  relativePath?: string;
  extension?: string;
  score?: number;
}

export interface AssetLookupReport {
  source?: string;
  localIndexesLoaded?: number;
  liveContainersScanned?: number;
  totalMatches?: number;
  matches?: AssetLookupMatch[];
  warnings?: string[];
  [key: string]: unknown;
}

export interface PipelineReport {
  generatedAtUtc?: string;
  canApply?: boolean;
  applyReadiness?: string;
  resolvedId?: number;
  warnings?: string[];
  errors?: string[];
  validationWarnings?: string[];
  validationErrors?: string[];
  dependencies?: Array<Record<string, unknown>>;
  proposedChanges?: DiffEntry[];
  diffPreview?: DiffPreview;
  clientSidePlan?: ClientPlan;
  visualClientSidePlan?: ClientPlan;
  clientIdentityPlan?: ClientPlan;
  itemAssetLookup?: AssetLookupReport;
  visualAssetLookup?: AssetLookupReport;
  assetLookup?: AssetLookupReport;
  spriteResolution?: Record<string, unknown>;
  assetPlans?: Array<Record<string, unknown>>;
  mapCachePlan?: Record<string, unknown>;
  postWriteValidationPlan?: string[];
  bytecodeBlocks?: string[];
  existingClientRegistration?: string[];
  proposedClientRegistration?: string[];
  requiredClientFiles?: string[];
  detectionSource?: string;
  serverCanApply?: boolean;
  canApplyClientIdentity?: boolean;
  shieldRestriction?: string;
  clientSideMode?: string;
  visualTheme?: Record<string, unknown>;
  drops?: Array<Record<string, unknown>>;
  skills?: Array<Record<string, unknown>>;
  spawns?: Array<Record<string, unknown>>;
  unsupportedFields?: Array<Record<string, unknown>> | string[];
  needsCopy?: boolean;
  viewId?: number;
  viewSprite?: string;
  accessoryPlan?: unknown;
  robePlan?: unknown;
  weaponPlan?: unknown;
  [key: string]: unknown;
}

export type DiscoveryData = Record<string, unknown>;
export type GrfIndexData = Record<string, unknown>;
export type GrfInspectData = Record<string, unknown>;
