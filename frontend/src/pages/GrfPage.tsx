import { useMutation } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { ApiClientError } from "../api/client";
import { DependencyTree } from "../components/DependencyTree";
import { DiffWorkbench } from "../components/DiffWorkbench";
import { EntityGrid } from "../components/EntityGrid";
import { FieldGroup } from "../components/FieldGroup";
import { GrfAssetEntry, GrfAssetTable } from "../components/GrfAssetTable";
import { JsonInspector } from "../components/JsonInspector";
import { PageHeader } from "../components/PageHeader";
import { PassiveAssetPreviewPanel } from "../components/PassiveAssetPreviewPanel";
import { PipelineWorkspaceLayout } from "../components/PipelineWorkspaceLayout";
import { ProblemDetailsView } from "../components/ProblemDetailsView";
import { ReadinessRibbon } from "../components/ReadinessRibbon";
import { ResponseMeta } from "../components/ResponseMeta";
import { ValidationMatrix } from "../components/ValidationMatrix";
import { useApiConfig } from "../features/connection/ApiConfigContext";
import { defaultConfigPath } from "../features/shared/requestBuilders";
import { asRecord, asRecordArray, asStringArray, toStringValue } from "../features/shared/viewData";

const initialExtensions = [".spr", ".act", ".bmp", ".tga", ".rsw", ".gnd", ".gat", ".rsm", ".wav"];

export function GrfPage() {
  const { client } = useApiConfig();
  const [configPath, setConfigPath] = useState(defaultConfigPath);
  const [repositoryRoot, setRepositoryRoot] = useState("");
  const [maxContainers, setMaxContainers] = useState("25");
  const [saveCache, setSaveCache] = useState(true);
  const [forceRefresh, setForceRefresh] = useState(false);
  const [container, setContainer] = useState("");
  const [inspectLimit, setInspectLimit] = useState("250");
  const [search, setSearch] = useState("");
  const [selectedExtensions, setSelectedExtensions] = useState<string[]>(initialExtensions);

  const indexMutation = useMutation({
    mutationFn: () =>
      client.grfIndex({
        configPath,
        grfRepositoryPath: repositoryRoot || undefined,
        maxContainers: Number(maxContainers) || 25,
        saveCache,
        forceRefresh,
      }),
  });

  const inspectMutation = useMutation({
    mutationFn: () =>
      client.grfInspect({
        configPath,
        container,
        limit: Number(inspectLimit) || 250,
        saveCache,
      }),
  });

  const indexData = asRecord(indexMutation.data?.data);
  const inspectData = asRecord(inspectMutation.data?.data);
  const indexDocument = asRecord(indexData?.index);
  const inspectDocument = asRecord(inspectData?.index);

  const containerSnapshots = asRecordArray(indexDocument?.containers);
  const containerOptions = containerSnapshots
    .map((item) => toStringValue(item.fullPath || item.path || item.containerPath, ""))
    .filter(Boolean);

  const extensionCounts = asRecordArray(inspectDocument?.extensionCounts);
  const entries = asRecordArray(inspectDocument?.entries).map<GrfAssetEntry>((entry) => ({
    relativePath: toStringValue(entry.relativePath),
    directoryPath: toStringValue(entry.directoryPath, ""),
    fileName: toStringValue(entry.fileName, ""),
    extension: toStringValue(entry.extension, ""),
    sizeCompressed: typeof entry.sizeCompressed === "number" ? entry.sizeCompressed : undefined,
    sizeDecompressed:
      typeof entry.sizeDecompressed === "number" ? entry.sizeDecompressed : undefined,
    encrypted: Boolean(entry.encrypted),
  }));

  const filteredEntries = entries.filter((entry) => {
    const searchValue = search.trim().toLowerCase();
    const matchesSearch =
      !searchValue ||
      entry.relativePath.toLowerCase().includes(searchValue) ||
      (entry.fileName ?? "").toLowerCase().includes(searchValue);
    const matchesExtension =
      !selectedExtensions.length || selectedExtensions.includes(entry.extension ?? "");

    return matchesSearch && matchesExtension;
  });

  const availableExtensions = useMemo(() => {
    const fromInspect = extensionCounts
      .map((item) => toStringValue(item.extension, ""))
      .filter(Boolean);
    const merged = new Set([...initialExtensions, ...fromInspect]);
    return Array.from(merged);
  }, [extensionCounts]);

  const indexError = indexMutation.error as ApiClientError | null;
  const inspectError = inspectMutation.error as ApiClientError | null;
  const combinedWarnings = [
    ...(indexMutation.data?.warnings ?? []),
    ...(inspectMutation.data?.warnings ?? []),
    ...asStringArray(indexData?.summary ? asRecord(indexData.summary)?.warnings : []),
    ...asStringArray(inspectData?.warnings),
  ];
  const combinedErrors = [
    ...(indexMutation.data?.errors ?? []),
    ...(inspectMutation.data?.errors ?? []),
  ];

  const selectedContainerCard = containerSnapshots.find(
    (item) => toStringValue(item.fullPath || item.path || item.containerPath, "") === container,
  );

  const previewItems = filteredEntries.slice(0, 8).map((entry) => ({
    key: entry.relativePath,
    path: entry.relativePath,
    type: entry.extension ?? "-",
    origin: selectedContainerCard
      ? toStringValue(selectedContainerCard.fullPath || selectedContainerCard.path)
      : "LocalIndex",
    status: "read-only" as const,
    note: entry.encrypted ? "Entrada marcada como encrypted." : "Inspecao local do indice.",
  }));

  const navigatorItems = containerSnapshots.slice(0, 12).map((snapshot) => ({
    key: toStringValue(snapshot.fullPath || snapshot.path || snapshot.containerPath),
    label: toStringValue(snapshot.name, "container"),
    subtitle: toStringValue(snapshot.extension, "-"),
    status:
      toStringValue(snapshot.fullPath || snapshot.path || snapshot.containerPath) === container
        ? ("good" as const)
        : ("neutral" as const),
    href: "#grf-inspect",
  }));

  function toggleExtension(extension: string) {
    setSelectedExtensions((current) =>
      current.includes(extension)
        ? current.filter((item) => item !== extension)
        : [...current, extension],
    );
  }

  return (
    <section className="page">
      <PageHeader
        title="GRF / Assets"
        description="Workspace de containers, filtros por extensao, proveniencia e inspecao read-only, sem extrair ou copiar assets."
      />
      <PipelineWorkspaceLayout
        sidebar={
          <div className="stack-lg">
            <EntityGrid
              title="Containers"
              description="Amostra dos containers indexados no resultado atual."
              items={
                navigatorItems.length
                  ? navigatorItems
                  : [
                      {
                        key: "no-containers",
                        label: "Nenhum container indexado ainda",
                        subtitle: "Execute GRF Index para preencher a lista",
                        status: "warning" as const,
                      },
                    ]
              }
              activeKey={container}
            />
            <section className="panel panel--danger">
              <div className="panel-header">
                <h3>Somente leitura</h3>
              </div>
              <p>
                Esta aba nao extrai, nao copia e nao altera GRFs. Ela apenas indexa e inspeciona
                containers com cache local seguro.
              </p>
            </section>
          </div>
        }
        primary={
          <div className="stack-lg">
            <FieldGroup title="GRF Index" description="Indexacao segura dos containers conhecidos.">
              <div className="form-grid">
                <label>
                  <span>Config path</span>
                  <input value={configPath} onChange={(event) => setConfigPath(event.target.value)} />
                </label>
                <label>
                  <span>Repository root opcional</span>
                  <input
                    placeholder="E:\\Ragnarok\\Conteudo Ragnarok\\GRF'S"
                    value={repositoryRoot}
                    onChange={(event) => setRepositoryRoot(event.target.value)}
                  />
                </label>
                <label>
                  <span>Max containers</span>
                  <input value={maxContainers} onChange={(event) => setMaxContainers(event.target.value)} />
                </label>
                <label className="checkbox-field">
                  <input checked={saveCache} onChange={(event) => setSaveCache(event.target.checked)} type="checkbox" />
                  <span>Salvar cache local seguro</span>
                </label>
                <label className="checkbox-field">
                  <input checked={forceRefresh} onChange={(event) => setForceRefresh(event.target.checked)} type="checkbox" />
                  <span>Forcar refresh do indice</span>
                </label>
              </div>
              <div className="form-actions">
                <button onClick={() => indexMutation.mutate()} type="button">
                  Indexar GRFs
                </button>
              </div>
            </FieldGroup>
            <FieldGroup
              id="grf-inspect"
              title="GRF Inspect"
              description="Inspecao do container selecionado com filtro local por extensao e busca."
            >
              <div className="form-grid">
                <label>
                  <span>Container</span>
                  <input list="grf-containers" value={container} onChange={(event) => setContainer(event.target.value)} placeholder="Selecione um container indexado" />
                  <datalist id="grf-containers">
                    {containerOptions.map((option) => (
                      <option key={option} value={option} />
                    ))}
                  </datalist>
                </label>
                <label>
                  <span>Entry limit</span>
                  <input value={inspectLimit} onChange={(event) => setInspectLimit(event.target.value)} />
                </label>
              </div>
              <div className="form-actions">
                <button onClick={() => inspectMutation.mutate()} type="button" disabled={!container}>
                  Inspecionar container
                </button>
              </div>
            </FieldGroup>
          </div>
        }
        inspector={
          <div className="stack-lg">
            <ReadinessRibbon
              readiness={inspectDocument ? "Container inspecionado" : indexDocument ? "Indice carregado" : "Aguardando leitura"}
              canApply={false}
              warningsCount={combinedWarnings.length}
              errorsCount={combinedErrors.length}
              correlationId={inspectMutation.data?.correlationId ?? indexMutation.data?.correlationId}
              modeLabel={inspectData ? `Engine = ${toStringValue(inspectData.engine, "-")}` : undefined}
            />
            <DiffWorkbench
              title="Resultado de GRF / Assets"
              tabs={[
                {
                  key: "assets",
                  label: "Assets",
                  content: (
                    <div className="stack-lg">
                      <GrfAssetTable
                        entries={filteredEntries}
                        search={search}
                        onSearchChange={setSearch}
                        selectedExtensions={selectedExtensions}
                        onExtensionToggle={toggleExtension}
                        availableExtensions={availableExtensions}
                      />
                      <PassiveAssetPreviewPanel title="Preview passivo" items={previewItems} />
                    </div>
                  ),
                },
                {
                  key: "provenance",
                  label: "Proveniencia",
                  content: (
                    <div className="stack-lg">
                      <DependencyTree
                        title="Resumo de proveniencia"
                        groups={[
                          {
                            title: "Container selecionado",
                            items: selectedContainerCard
                              ? [
                                  {
                                    label: toStringValue(selectedContainerCard.fullPath || selectedContainerCard.path),
                                    hint: `(${toStringValue(selectedContainerCard.extension, "-")})`,
                                    status: "read-only" as const,
                                    origin: "LocalIndex",
                                  },
                                ]
                              : [],
                          },
                          {
                            title: "Extensoes principais",
                            items: extensionCounts.map((item) => ({
                              label: `${toStringValue(item.extension)} (${String(item.count ?? 0)})`,
                              status: "read-only" as const,
                              origin: "Inspect",
                            })),
                          },
                          {
                            title: "Top level directories",
                            items: asRecordArray(inspectDocument?.topLevelDirectories).map((item) => ({
                              label: `${toStringValue(item.directoryName)} (${String(item.count ?? 0)})`,
                              status: "read-only" as const,
                              origin: "Inspect",
                            })),
                          },
                          {
                            title: "Warnings",
                            items: combinedWarnings.map((warning) => ({
                              label: warning,
                              status: "blocked" as const,
                              origin: "Inspect",
                            })),
                          },
                        ]}
                      />
                    </div>
                  ),
                },
                {
                  key: "validation",
                  label: "Validacao",
                  content: (
                    <div className="stack-lg">
                      <ProblemDetailsView problem={inspectError?.problem ?? indexError?.problem} />
                      <ResponseMeta response={inspectMutation.data ?? indexMutation.data ?? null} />
                      <ValidationMatrix
                        warnings={combinedWarnings}
                        errors={combinedErrors}
                        blockReasons={["Nenhuma extracao, copia ou escrita em GRF e permitida nesta fase."]}
                      />
                    </div>
                  ),
                },
                {
                  key: "json",
                  label: "JSON",
                  content: (
                    <div className="stack-lg">
                      <JsonInspector title="Resultado do indice" value={indexMutation.data?.data} />
                      <JsonInspector title="Inspecao do container" value={inspectMutation.data?.data} />
                    </div>
                  ),
                },
              ]}
            />
          </div>
        }
      />
    </section>
  );
}
