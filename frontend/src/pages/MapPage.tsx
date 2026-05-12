import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import type { ApiClientError } from "../api/client";
import { DiffViewer } from "../components/DiffViewer";
import { PageHeader } from "../components/PageHeader";
import { PipelineReportView } from "../components/PipelineReportView";
import { ResultShell } from "../components/ResultShell";
import { useApiConfig } from "../features/connection/ApiConfigContext";
import { defaultConfigPath } from "../features/shared/requestBuilders";

export function MapPage() {
  const { client } = useApiConfig();
  const [form, setForm] = useState({
    configPath: defaultConfigPath,
    mapName: "prontera",
    assetGrfContainer: "data_0.grf",
    scanGrfAssets: true
  });

  const payload = {
    configPath: form.configPath,
    mapName: form.mapName,
    assetGrfContainer: form.assetGrfContainer || undefined,
    scanGrfAssets: form.scanGrfAssets
  };

  const dryRun = useMutation({ mutationFn: () => client.mapDryRun(payload) });
  const diff = useMutation({ mutationFn: () => client.mapDiffPreview(payload) });

  return (
    <div className="stack-xl">
      <PageHeader
        title="Mapas"
        description="Dry-run e diff-preview de mapa, com destaque forte para ambiguidades, dependências ausentes e bloqueio de rename binário."
        actions={
          <div className="button-row">
            <button className="button-primary" type="button" onClick={() => dryRun.mutate()} disabled={dryRun.isPending}>
              {dryRun.isPending ? "Executando..." : "Rodar dry-run"}
            </button>
            <button className="button-secondary" type="button" onClick={() => diff.mutate()} disabled={diff.isPending}>
              {diff.isPending ? "Gerando..." : "Ver diff-preview"}
            </button>
          </div>
        }
      />
      <section className="panel">
        <div className="form-grid">
          <label><span>Config path</span><input value={form.configPath} onChange={(event) => setForm((current) => ({ ...current, configPath: event.target.value }))} /></label>
          <label><span>MapName</span><input value={form.mapName} onChange={(event) => setForm((current) => ({ ...current, mapName: event.target.value }))} /></label>
          <label><span>asset-grf-container</span><input value={form.assetGrfContainer} onChange={(event) => setForm((current) => ({ ...current, assetGrfContainer: event.target.value }))} /></label>
          <label className="checkbox-field">
            <input checked={form.scanGrfAssets} onChange={(event) => setForm((current) => ({ ...current, scanGrfAssets: event.target.checked }))} type="checkbox" />
            <span>scan-grf-assets</span>
          </label>
        </div>
        <p className="muted-text">
          Map apply não está disponível na API. Esta tela mostra apenas `AssetPlans`, `MapCachePlan`, riscos e hunks de planejamento.
        </p>
      </section>
      <ResultShell response={dryRun.data} problem={(dryRun.error as ApiClientError | undefined)?.problem}>
        <PipelineReportView report={dryRun.data?.data} />
      </ResultShell>
      <ResultShell response={diff.data} problem={(diff.error as ApiClientError | undefined)?.problem}>
        <DiffViewer diff={diff.data?.data ?? undefined} />
      </ResultShell>
    </div>
  );
}
