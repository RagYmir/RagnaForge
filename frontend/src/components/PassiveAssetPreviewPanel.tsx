import { useState, useEffect } from "react";
import { useApiConfig } from "../features/connection/ApiConfigContext";

export interface PassiveAssetPreviewItem {
  key: string;
  name?: string;
  path: string;
  expectedPath?: string;
  type?: string;
  category?: string;
  origin?: string;
  provenance?: string;
  status: "resolved" | "missing" | "ambiguous" | "blocked" | "read-only" | "needs-copy-future";
  note?: string;
}

interface AssetPreviewResponse {
  assetName: string;
  entryPath: string;
  extension: string;
  contentType: string | null;
  previewKind: "Image" | "Placeholder" | "Unsupported" | "Blocked" | "Missing" | "Ambiguous" | "TooLarge";
  dataUrl: string | null;
  width: number | null;
  height: number | null;
  source: string;
  provenance: string;
  warnings: string[];
  errors: string[];
}

function AssetVisualPreview({ item }: { item: PassiveAssetPreviewItem }) {
  const { client } = useApiConfig();
  const [data, setData] = useState<AssetPreviewResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [correlationId, setCorrelationId] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    async function load() {
      if ((item.status !== "resolved" && item.status !== "needs-copy-future") || !item.origin) {
         return;
      }

      const extension = "." + (item.path.split(".").pop()?.toLowerCase() ?? "");
      const attemptableExtensions = [".bmp", ".png", ".jpg", ".jpeg", ".webp", ".tga", ".spr", ".act", ".gat", ".gnd", ".rsw", ".rsm"];
      
      if (!attemptableExtensions.includes(extension)) {
          return;
      }

      try {
        const response = await client.assetPreview({
          source: item.origin,
          container: item.provenance ?? "Loose",
          entryPath: item.path,
          expectedExtension: extension,
        });

        if (active && response.success) {
          setData(response.data as AssetPreviewResponse);
        }
      } catch (err: any) {
        if (active) {
          setError(err.message || "Failed to load preview");
          setCorrelationId(err.problem?.extensions?.correlationId || null);
        }
      }
    }

    load();

    return () => {
      active = false;
    };
  }, [item, client]);

  if (error) {
    return (
      <div className="asset-preview-placeholder">
        <span className="error-text">Erro ao carregar preview: {error}</span>
        {correlationId && <div className="muted-text text-xs mt-1">Correlation: {correlationId}</div>}
      </div>
    );
  }

  if (data) {
    if (data.previewKind === "Image" && data.dataUrl) {
      return (
        <div className="visual-preview-container">
          <img 
            src={data.dataUrl} 
            alt={data.assetName} 
            className="visual-preview-image" 
            title={data.assetName} 
          />
        </div>
      );
    }
    
    if (data.previewKind === "Unsupported") {
      return (
        <div className="asset-preview-placeholder">
          <span className="muted-text">Formato {data.extension} suportado apenas como placeholder.</span>
          {(data.warnings?.length ?? 0) > 0 && <div className="warning-text text-xs mt-1">{data.warnings[0]}</div>}
        </div>
      );
    }

    if (data.previewKind === "TooLarge") {
      return (
        <div className="asset-preview-placeholder">
          <span className="muted-text">Arquivo muito grande para preview web seguro.</span>
        </div>
      );
    }

    return (
      <div className="asset-preview-placeholder">
        <span className="muted-text">Preview retornado como: {data.previewKind}</span>
        {(data.errors?.length ?? 0) > 0 && <div className="error-text text-xs mt-1">{data.errors[0]}</div>}
      </div>
    );
  }

  return (
    <span className="asset-preview-placeholder">
      Preview visual real pendente de endpoint seguro de leitura.
    </span>
  );
}

export function PassiveAssetPreviewPanel({
  title,
  items,
}: {
  title: string;
  items: PassiveAssetPreviewItem[];
}) {
  if (!items.length) {
    return null;
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <h3>{title}</h3>
          <p className="muted-text">
            Preview visual read-only seguro via API. Nenhuma alteracao sera persistida.
          </p>
        </div>
      </div>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>Asset</th>
              <th>Category / Type</th>
              <th>Origin / Provenance</th>
              <th>Preview</th>
              <th>Status</th>
              <th>Note</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.key}>
                <td>
                  <div className="asset-preview-cell">
                    <strong>{item.name ?? item.path.split(/[\\/]/).pop() ?? item.path}</strong>
                    <div className="mono-text">{item.path}</div>
                    {item.expectedPath && item.expectedPath !== item.path ? (
                      <div className="muted-text">Esperado: {item.expectedPath}</div>
                    ) : null}
                  </div>
                </td>
                <td>
                  <div>{item.category ?? "-"}</div>
                  <div className="muted-text">{item.type ?? "-"}</div>
                </td>
                <td>
                  <div>{item.origin ?? "-"}</div>
                  <div className="muted-text">{item.provenance ?? "-"}</div>
                </td>
                <td>
                  <AssetVisualPreview item={item} />
                </td>
                <td>
                  <span className={`mono-pill dependency-status dependency-status--${item.status}`}>
                    {item.status}
                  </span>
                </td>
                <td>{item.note ?? "-"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
