import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import type { ApiClientError } from "../api/client";
import { DiffViewer } from "../components/DiffViewer";
import { PageHeader } from "../components/PageHeader";
import { PipelineReportView } from "../components/PipelineReportView";
import { ResultShell } from "../components/ResultShell";
import { useApiConfig } from "../features/connection/ApiConfigContext";
import {
  buildMonsterDrops,
  buildMonsterSkills,
  buildMonsterSpawns,
  defaultConfigPath
} from "../features/shared/requestBuilders";

export function MonsterPage() {
  const { client } = useApiConfig();
  const [form, setForm] = useState({
    configPath: defaultConfigPath,
    aegisName: "RF_UI_MONSTER",
    displayName: "RagnaForge UI Monster",
    mapName: "prontera",
    level: "10",
    hp: "10000",
    amount: "5",
    respawnMilliseconds: "60000",
    drops: "item=Apple,chance=5000;item=Jellopy,chance=1000,mvp=true",
    skills: "id=175,level=1,state=any,rate=5000;id=176,level=3,state=any,rate=2500",
    spawns: "map=prontera,x=120,y=140,areaX=8,areaY=6,amount=5,respawn=60000,label=RF_UI"
  });

  const payload = {
    configPath: form.configPath,
    aegisName: form.aegisName,
    displayName: form.displayName,
    mapName: form.mapName,
    level: Number(form.level),
    hp: Number(form.hp),
    amount: Number(form.amount),
    respawnMilliseconds: Number(form.respawnMilliseconds),
    drops: buildMonsterDrops(form.drops),
    skills: buildMonsterSkills(form.skills),
    spawns: buildMonsterSpawns(form.spawns)
  };

  const dryRun = useMutation({ mutationFn: () => client.monsterDryRun(payload) });
  const diff = useMutation({ mutationFn: () => client.monsterDiffPreview(payload) });

  return (
    <div className="stack-xl">
      <PageHeader
        title="Monstros"
        description="Dry-run e diff-preview de monstro, com drops, skills e spawns avançados em modo seguro."
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
          <label><span>AegisName</span><input value={form.aegisName} onChange={(event) => setForm((current) => ({ ...current, aegisName: event.target.value }))} /></label>
          <label><span>Name</span><input value={form.displayName} onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))} /></label>
          <label><span>Map</span><input value={form.mapName} onChange={(event) => setForm((current) => ({ ...current, mapName: event.target.value }))} /></label>
          <label><span>Level</span><input value={form.level} onChange={(event) => setForm((current) => ({ ...current, level: event.target.value }))} /></label>
          <label><span>HP</span><input value={form.hp} onChange={(event) => setForm((current) => ({ ...current, hp: event.target.value }))} /></label>
          <label><span>Amount</span><input value={form.amount} onChange={(event) => setForm((current) => ({ ...current, amount: event.target.value }))} /></label>
          <label><span>Respawn</span><input value={form.respawnMilliseconds} onChange={(event) => setForm((current) => ({ ...current, respawnMilliseconds: event.target.value }))} /></label>
        </div>
        <label><span>Drops</span><textarea rows={4} value={form.drops} onChange={(event) => setForm((current) => ({ ...current, drops: event.target.value }))} /></label>
        <label><span>Skills</span><textarea rows={4} value={form.skills} onChange={(event) => setForm((current) => ({ ...current, skills: event.target.value }))} /></label>
        <label><span>Spawns</span><textarea rows={4} value={form.spawns} onChange={(event) => setForm((current) => ({ ...current, spawns: event.target.value }))} /></label>
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
