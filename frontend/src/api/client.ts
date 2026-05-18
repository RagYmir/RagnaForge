import type {
  AgentHealthSummary,
  ApiProblemDetails,
  ApiResponse,
  ConfigValidateData,
  DiscoveryData,
  GrfIndexData,
  GrfInspectData,
  PipelineReport,
  StatusData
} from "./types";

export interface ApiConnectionConfig {
  baseUrl: string;
  apiKey: string;
}

export class ApiClientError extends Error {
  status: number;
  problem?: ApiProblemDetails;

  constructor(message: string, status: number, problem?: ApiProblemDetails) {
    super(message);
    this.name = "ApiClientError";
    this.status = status;
    this.problem = problem;
  }
}

export class ApiClient {
  private getConfig: () => ApiConnectionConfig;

  constructor(getConfig: () => ApiConnectionConfig) {
    this.getConfig = getConfig;
  }

  async health() {
    const response = await fetch(this.resolveUrl("/health"));
    if (!response.ok) {
      throw await this.toError(response);
    }

    return response.json() as Promise<{ status: string; mode: string; generatedAtUtc: string }>;
  }

  status() {
    return this.request<StatusData>("/api/status", { method: "GET" });
  }

  safetyCapabilities() {
    return this.request<StatusData["capabilities"]>("/api/safety/capabilities", { method: "GET" });
  }

  agentHealth() {
    return this.request<AgentHealthSummary>("/api/agent/health", { method: "GET" });
  }

  validateConfig(payload: { configPath: string }) {
    return this.request<ConfigValidateData>("/api/config/validate", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  discover(payload: Record<string, unknown>) {
    return this.request<DiscoveryData>("/api/discover", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  grfIndex(payload: Record<string, unknown>) {
    return this.request<GrfIndexData>("/api/grf/index", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  grfInspect(payload: Record<string, unknown>) {
    return this.request<GrfInspectData>("/api/grf/inspect", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  itemDryRun(payload: Record<string, unknown>) {
    return this.request<PipelineReport>("/api/items/dry-run", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  itemDiffPreview(payload: Record<string, unknown>) {
    return this.request<PipelineReport["diffPreview"]>("/api/items/diff-preview", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  equipmentDryRun(payload: Record<string, unknown>) {
    return this.request<PipelineReport>("/api/equipment/dry-run", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  equipmentDiffPreview(payload: Record<string, unknown>) {
    return this.request<PipelineReport["diffPreview"]>("/api/equipment/diff-preview", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  npcDryRun(payload: Record<string, unknown>) {
    return this.request<PipelineReport>("/api/npcs/dry-run", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  npcDiffPreview(payload: Record<string, unknown>) {
    return this.request<PipelineReport["diffPreview"]>("/api/npcs/diff-preview", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  monsterDryRun(payload: Record<string, unknown>) {
    return this.request<PipelineReport>("/api/monsters/dry-run", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  monsterDiffPreview(payload: Record<string, unknown>) {
    return this.request<PipelineReport["diffPreview"]>("/api/monsters/diff-preview", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  mapDryRun(payload: Record<string, unknown>) {
    return this.request<PipelineReport>("/api/maps/dry-run", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  mapDiffPreview(payload: Record<string, unknown>) {
    return this.request<PipelineReport["diffPreview"]>("/api/maps/diff-preview", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  assetPreview(payload: Record<string, unknown>) {
    return this.request<any>("/api/assets/preview", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  async request<T>(path: string, init: RequestInit) {
    const response = await fetch(this.resolveUrl(path), {
      ...init,
      headers: this.buildHeaders(init.headers)
    });

    if (!response.ok) {
      throw await this.toError(response);
    }

    return response.json() as Promise<ApiResponse<T>>;
  }

  private buildHeaders(headers?: HeadersInit) {
    const config = this.getConfig();
    const resolved = new Headers(headers);
    resolved.set("Content-Type", "application/json");
    resolved.set("X-Correlation-Id", crypto.randomUUID());
    if (config.apiKey) {
      resolved.set("X-RagnaForge-Api-Key", config.apiKey);
    }

    return resolved;
  }

  private resolveUrl(path: string) {
    const config = this.getConfig();
    return new URL(path, `${config.baseUrl.replace(/\/+$/, "")}/`).toString();
  }

  private async toError(response: Response) {
    let problem: ApiProblemDetails | undefined;
    try {
      problem = (await response.json()) as ApiProblemDetails;
    } catch {
      problem = undefined;
    }

    return new ApiClientError(
      problem?.detail ?? problem?.title ?? `API request failed with status ${response.status}.`,
      response.status,
      problem
    );
  }
}
