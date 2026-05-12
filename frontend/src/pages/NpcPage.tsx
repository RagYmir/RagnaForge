import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import type { ApiClientError } from "../api/client";
import { DiffViewer } from "../components/DiffViewer";
import { PageHeader } from "../components/PageHeader";
import { PipelineReportView } from "../components/PipelineReportView";
import { ResultShell } from "../components/ResultShell";
import { useApiConfig } from "../features/connection/ApiConfigContext";
import { defaultConfigPath } from "../features/shared/requestBuilders";

export function NpcPage() {
  const { client } = useApiConfig();
  const [form, setForm] = useState({
    configPath: defaultConfigPath,
    name: "RagnaForge Guide",
    mapName: "prontera",
    x: "150",
    y: "180",
    direction: "2",
    sprite: "4_M_JOB_BLACKSMITH",
    scriptBody: "mes \"Hello from RagnaForge UI\";\nclose;"
  });

  const payload = {
    configPath: form.configPath,
    name: form.name,
    mapName: form.mapName,
    x: Number(form.x),
    y: Number(form.y),
    direction: Number(form.direction),
    sprite: form.sprite,
    scriptBody: form.scriptBody
  };

  const dryRun = useMutation({ mutationFn: () => client.npcDryRun(payload) });
  const diff = useMutation({ mutationFn: () => client.npcDiffPreview(payload) });

  return (
    <div className="stack-xl">
      <PageHeader
        title="NPCs"
        description="Dry-run e diff-preview de NPC, com foco em sprite resolution, client identity textual segura e bloqueios de bytecode."
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
          <label><span>Name</span><input value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} /></label>
          <label><span>Map</span><input value={form.mapName} onChange={(event) => setForm((current) => ({ ...current, mapName: event.target.value }))} /></label>
          <label><span>X</span><input value={form.x} onChange={(event) => setForm((current) => ({ ...current, x: event.target.value }))} /></label>
          <label><span>Y</span><input value={form.y} onChange={(event) => setForm((current) => ({ ...current, y: event.target.value }))} /></label>
          <label><span>Dir</span><input value={form.direction} onChange={(event) => setForm((current) => ({ ...current, direction: event.target.value }))} /></label>
          <label><span>Sprite</span><input value={form.sprite} onChange={(event) => setForm((current) => ({ ...current, sprite: event.target.value }))} /></label>
        </div>
        <label>
          <span>Dialog/script básico</span>
          <textarea rows={6} value={form.scriptBody} onChange={(event) => setForm((current) => ({ ...current, scriptBody: event.target.value }))} />
        </label>
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
