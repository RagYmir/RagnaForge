import { useState, useEffect } from "react";
import { useApiConfig } from "../features/connection/ApiConfigContext";
import { AssetPreviewResponse } from "../api/types";

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

function AssetVisualPreview({ item }: { item: PassiveAssetPreviewItem }) {
  const { client } = useApiConfig();
  const [data, setData] = useState<AssetPreviewResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [correlationId, setCorrelationId] = useState<string | null>(null);
  const [frameIndex] = useState(0); // v1: fixo em 0

  useEffect(() => {
    let active = true;

    async function load() {
      if ((item.status !== "resolved" && item.status !== "needs-copy-future") || !item.origin) {
         return;
      }

      // Normalize path to forward slashes before extension extraction
      const normalizedPath = item.path.replace(/\\/g, '/');
      const extension = "." + (normalizedPath.split(".").pop()?.toLowerCase() ?? "");
      const attemptableExtensions = [".bmp", ".png", ".jpg", ".jpeg", ".webp", ".tga", ".spr", ".act", ".gat", ".gnd", ".rsw", ".rsm"];
      
      if (!attemptableExtensions.includes(extension)) {
          return;
      }

      try {
        // Normalize companion path as well
        const companionEntryPath = extension === ".act" 
          ? (normalizedPath.substring(0, normalizedPath.length - 4) + ".spr").replace(/\\/g, '/')
          : undefined;

        const response = await client.assetPreview({
          source: item.origin!,
          container: item.provenance ?? "Loose",
          entryPath: normalizedPath,
          expectedExtension: extension,
          frameIndex: extension === ".spr" ? frameIndex : undefined,
          companionEntryPath
        });

        if (active && response.success) {
          setData(response.data);
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
  }, [item, client, frameIndex]);

  if (error) {
    return (
      <div className="asset-preview-placeholder">
        <span className="error-text">Erro ao carregar preview: {error}</span>
        {correlationId && <div className="muted-text text-xs mt-1">Correlation: {correlationId}</div>}
      </div>
    );
  }

  if (data) {
    const isVisual = (data.previewKind === "Image" || data.previewKind === "SpriteFrame" || data.previewKind === "ActFrame") && data.dataUrl;
    
    if (isVisual) {
      return (
        <div className="visual-preview-container">
          <img 
            src={data.dataUrl!} 
            alt={data.assetName} 
            className="visual-preview-image" 
            title={`${data.assetName} (${data.width}x${data.height})`} 
          />
          {data.metadata && (
            <div className="visual-preview-meta text-xs mt-1">
              {data.metadata.frameCount !== undefined && (
                <div>Frame: {data.metadata.selectedFrame ?? 0} / {data.metadata.frameCount}</div>
              )}
              {data.metadata.actionCount !== undefined && (
                <div>Action: {data.metadata.selectedAction ?? 0} / {data.metadata.actionCount}</div>
              )}
              {data.metadata.renderMode && <div>Mode: {data.metadata.renderMode}</div>}
              {data.metadata.formatVersion && <div>Version: {data.metadata.formatVersion}</div>}
            </div>
          )}
          {(data.warnings?.length ?? 0) > 0 && <div className="warning-text text-xs mt-1">{data.warnings[0]}</div>}
        </div>
      );
    }
    
    if (data.previewKind === "Unsupported" || data.previewKind?.includes("Metadata") || !data.dataUrl) {
      return (
        <div className="asset-preview-placeholder">
          <span className="muted-text">
            {data.previewKind === "ActMetadata" 
              ? "Dados da Ação (Metadata-Only v1)" 
              : `Formato ${data.extension} suportado apenas como metadados.`}
          </span>
          {data.metadata && (
             <div className="text-xs mt-1">
               {data.metadata.frameCount !== undefined && <div>Frames: {data.metadata.frameCount}</div>}
               {data.metadata.actionCount !== undefined && <div>Actions: {data.metadata.actionCount}</div>}
             </div>
          )}
          {(data.warnings?.length ?? 0) > 0 && <div className="warning-text text-xs mt-1">{data.warnings[0]}</div>}
        </div>
      );
    }
    
    if (data.previewKind === "TooLarge") {
      return (
        <div className="asset-preview-placeholder">
          <span className="muted-text">Arquivo muito grande para preview web seguro (Limite 10MB).</span>
        </div>
      );
    }

    return (
      <div className="asset-preview-placeholder">
        <span className="muted-text">Preview: {data.previewKind}</span>
        {(data.errors?.length ?? 0) > 0 && <div className="error-text text-xs mt-1">{data.errors[0]}</div>}
      </div>
    );
  }

  return (
    <span className="asset-preview-placeholder">
      Carregando preview...
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
