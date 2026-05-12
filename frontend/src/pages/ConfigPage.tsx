import { useMutation } from "@tanstack/react-query";
import { useMemo, useState } from "react";
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

export function ConfigPage() {
  const { client } = useApiConfig();
  const [configPath, setConfigPath] = useState(defaultConfigPath);

  const validateMutation = useMutation({
    mutationFn: () => client.validateConfig({ configPath }),
  });

  const payload = validateMutation.data?.data;
  const error = validateMutation.error as ApiClientError | null;
  const warnings = useMemo(
    () => [
      ...(validateMutation.data?.warnings ?? []),
      ...((payload?.validation?.issues ?? [])
        .filter((issue) => issue.severity.toLowerCase() !== "error")
        .map((issue) => `${issue.code}: ${issue.message}`)),
    ],
    [payload, validateMutation.data],
  );
  const errors = useMemo(
    () => [
      ...(validateMutation.data?.errors ?? []),
      ...((payload?.validation?.issues ?? [])
        .filter((issue) => issue.severity.toLowerCase() === "error")
        .map((issue) => `${issue.code}: ${issue.message}`)),
    ],
    [payload, validateMutation.data],
  );

  return (
    <section className="page">
      <PageHeader
        title="Configuracao"
        description="Valida o manifest local e mostra caminhos, avisos e pendencias conhecidas sem editar configuracao persistente."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <EntityGrid
            title="Configuracao"
            description="Resumo da validacao atual."
            activeKey="config-base"
            items={[
              {
                key: "config-base",
                label: "Manifest",
                subtitle: payload?.validation?.isValid ? "Validado" : "Pendente",
                status: payload?.validation?.isValid ? ("good" as const) : ("warning" as const),
                href: "#config-base",
              },
              {
                key: "config-paths",
                label: "Paths",
                subtitle: payload?.manifestPath ?? defaultConfigPath,
                status: "neutral" as const,
                href: "#config-paths",
              },
              {
                key: "config-validation",
                label: "Validacao",
                subtitle: `${warnings.length} warnings / ${errors.length} errors`,
                status: errors.length ? ("danger" as const) : warnings.length ? ("warning" as const) : ("good" as const),
                href: "#config-validation",
              },
            ]}
          />
        }
        primary={
          <div className="stack-lg">
            <FieldGroup id="config-base" title="Manifest" description="Caminho do manifest a ser validado.">
              <div className="form-grid">
                <label className="form-grid__wide">
                  <span>Config path</span>
                  <input value={configPath} onChange={(event) => setConfigPath(event.target.value)} />
                </label>
              </div>
              <div className="form-actions">
                <button onClick={() => validateMutation.mutate()} type="button">
                  Validar configuracao
                </button>
              </div>
            </FieldGroup>
            <FieldGroup
              id="config-paths"
              title="Pendencia conhecida"
              description="A UI apenas destaca divergencias entre o manifest e o discovery."
            >
              <div className="notice notice--warning">
                O manifest ainda pode manter ClientDate desconhecido enquanto o discovery detecta outra data.
              </div>
            </FieldGroup>
          </div>
        }
        inspector={
          <div className="stack-lg">
            <ReadinessRibbon
              readiness={payload?.validation.isValid ? "Manifest valido" : "Aguardando validacao"}
              canApply={false}
              warningsCount={warnings.length}
              errorsCount={errors.length}
              correlationId={validateMutation.data?.correlationId}
              modeLabel={payload?.manifest?.clientDateStatus}
            />
            <DiffWorkbench
              title="Resultado da configuracao"
              tabs={[
                {
                  key: "summary",
                  label: "Resumo",
                  content: (
                    <div className="stack-lg">
                      <ProblemDetailsView problem={error?.problem} />
                      <ResponseMeta response={validateMutation.data ?? null} />
                      <ValidationMatrix warnings={warnings} errors={errors} />
                      {payload ? (
                        <section className="panel">
                          <h3>Resumo</h3>
                          <dl className="definition-list">
                            <div>
                              <dt>Valido</dt>
                              <dd>{payload.validation.isValid ? "Sim" : "Nao"}</dd>
                            </div>
                            <div>
                              <dt>Config Path</dt>
                              <dd>{payload.manifestPath}</dd>
                            </div>
                            <div>
                              <dt>rAthena</dt>
                              <dd>{payload.manifest.paths.rAthenaPath}</dd>
                            </div>
                            <div>
                              <dt>Patch</dt>
                              <dd>{payload.manifest.paths.patchPath}</dd>
                            </div>
                            <div>
                              <dt>GRF Editor</dt>
                              <dd>{payload.manifest.paths.grfEditorPath}</dd>
                            </div>
                            <div>
                              <dt>Client date</dt>
                              <dd>{String(payload.manifest.episodeProfile.clientDate ?? "Desconhecido no manifest")}</dd>
                            </div>
                          </dl>
                        </section>
                      ) : null}
                    </div>
                  ),
                },
                {
                  key: "json",
                  label: "JSON",
                  content: <JsonInspector title="Payload bruto" value={payload} />,
                },
              ]}
            />
          </div>
        }
      />
    </section>
  );
}
