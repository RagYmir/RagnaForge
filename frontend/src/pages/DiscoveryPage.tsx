import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { ApiClientError } from "../api/client";
import { DiffWorkbench } from "../components/DiffWorkbench";
import { EntityGrid } from "../components/EntityGrid";
import { FieldGroup } from "../components/FieldGroup";
import { JsonInspector } from "../components/JsonInspector";
import { PageHeader } from "../components/PageHeader";
import { PipelineWorkspaceLayout } from "../components/PipelineWorkspaceLayout";
import { ProblemDetailsView } from "../components/ProblemDetailsView";
import { ReadinessRibbon } from "../components/ReadinessRibbon";
import { ResponseMeta } from "../components/ResponseMeta";
import { ValidationMatrix } from "../components/ValidationMatrix";
import { useApiConfig } from "../features/connection/ApiConfigContext";
import { defaultConfigPath } from "../features/shared/requestBuilders";

export function DiscoveryPage() {
  const { client } = useApiConfig();
  const [configPath, setConfigPath] = useState(defaultConfigPath);
  const [maxGrfContainers, setMaxGrfContainers] = useState("25");
  const [saveCache, setSaveCache] = useState(true);

  const discoveryMutation = useMutation({
    mutationFn: () =>
      client.discover({
        configPath,
        maxGrfContainers: Number(maxGrfContainers) || 25,
        saveCache,
      }),
  });

  const payload = discoveryMutation.data?.data;
  const error = discoveryMutation.error as ApiClientError | null;
  const patch = (payload?.patch as Record<string, unknown> | undefined) ?? {};
  const rAthena = (payload?.rAthena as Record<string, unknown> | undefined) ?? {};
  const grf = (payload?.grf as Record<string, unknown> | undefined) ?? {};
  const grfEditor = (payload?.grfEditor as Record<string, unknown> | undefined) ?? {};

  return (
    <section className="page">
      <PageHeader
        title="Discovery"
        description="Executa o discovery read-only sobre rAthena, Patch, GRFs e GRF Editor usando o manifest local."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <EntityGrid
            title="Discovery"
            description="Componentes detectados no ultimo resultado."
            activeKey="discovery-base"
            items={[
              {
                key: "discovery-base",
                label: "Entrada",
                subtitle: configPath,
                status: "neutral" as const,
                href: "#discovery-base",
              },
              {
                key: "discovery-paths",
                label: "Repositorios",
                subtitle: rAthena.repositoryRoot ? "Discovery carregado" : "Aguardando discovery",
                status: rAthena.repositoryRoot ? ("good" as const) : ("warning" as const),
                href: "#discovery-paths",
              },
              {
                key: "discovery-validation",
                label: "Warnings / Errors",
                subtitle: `${discoveryMutation.data?.warnings?.length ?? 0} / ${discoveryMutation.data?.errors?.length ?? 0}`,
                status: (discoveryMutation.data?.errors?.length ?? 0) > 0 ? ("danger" as const) : (discoveryMutation.data?.warnings?.length ?? 0) > 0 ? ("warning" as const) : ("good" as const),
                href: "#discovery-validation",
              },
            ]}
          />
        }
        primary={
          <div className="stack-lg">
            <FieldGroup id="discovery-base" title="Discovery read-only" description="Parametros minimos para discovery seguro.">
              <div className="form-grid">
                <label>
                  <span>Config path</span>
                  <input value={configPath} onChange={(event) => setConfigPath(event.target.value)} />
                </label>
                <label>
                  <span>Max GRF containers</span>
                  <input value={maxGrfContainers} onChange={(event) => setMaxGrfContainers(event.target.value)} />
                </label>
                <label className="checkbox-field">
                  <input checked={saveCache} onChange={(event) => setSaveCache(event.target.checked)} type="checkbox" />
                  <span>Permitir cache local seguro</span>
                </label>
              </div>
              <div className="form-actions">
                <button onClick={() => discoveryMutation.mutate()} type="button">
                  Executar discovery
                </button>
              </div>
            </FieldGroup>
            <FieldGroup id="discovery-paths" title="Resumo" description="rAthena, Patch, GRF Editor e client date detectados.">
              <div className="detail-grid">
                <div>
                  <dt>rAthena detectado</dt>
                  <dd>{String(rAthena.repositoryRoot ?? "Nao informado")}</dd>
                </div>
                <div>
                  <dt>Patch detectado</dt>
                  <dd>{String(patch.repositoryRoot ?? "Nao informado")}</dd>
                </div>
                <div>
                  <dt>GRF Editor</dt>
                  <dd>{String(grfEditor.rootPath ?? "Nao informado")}</dd>
                </div>
                <div>
                  <dt>Client date</dt>
                  <dd>{String(patch.clientDate ?? "Desconhecido")}</dd>
                </div>
                <div>
                  <dt>GRFs</dt>
                  <dd>{String((grf.containers as unknown[] | undefined)?.length ?? 0)}</dd>
                </div>
              </div>
            </FieldGroup>
          </div>
        }
        inspector={
          <div className="stack-lg">
            <ReadinessRibbon
              readiness={payload ? "Discovery carregado" : "Aguardando discovery"}
              canApply={false}
              warningsCount={discoveryMutation.data?.warnings?.length ?? 0}
              errorsCount={discoveryMutation.data?.errors?.length ?? 0}
              correlationId={discoveryMutation.data?.correlationId}
              modeLabel={payload ? "ReadOnly discovery" : undefined}
            />
            <DiffWorkbench
              title="Resultado do discovery"
              tabs={[
                {
                  key: "summary",
                  label: "Resumo",
                  content: (
                    <div id="discovery-validation" className="stack-lg">
                      <ProblemDetailsView problem={error?.problem} />
                      <ResponseMeta response={discoveryMutation.data ?? null} />
                      <ValidationMatrix
                        warnings={discoveryMutation.data?.warnings ?? []}
                        errors={discoveryMutation.data?.errors ?? []}
                      />
                    </div>
                  ),
                },
                {
                  key: "json",
                  label: "JSON",
                  content: <JsonInspector title="Discovery completo" value={payload} />,
                },
              ]}
            />
          </div>
        }
      />
    </section>
  );
}
