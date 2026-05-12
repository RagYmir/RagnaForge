export interface GrfAssetEntry {
  relativePath: string;
  directoryPath?: string;
  fileName?: string;
  extension?: string;
  sizeCompressed?: number;
  sizeDecompressed?: number;
  encrypted?: boolean;
}

interface GrfAssetTableProps {
  entries: GrfAssetEntry[];
  search: string;
  onSearchChange: (value: string) => void;
  selectedExtensions: string[];
  onExtensionToggle: (extension: string) => void;
  availableExtensions: string[];
}

export function GrfAssetTable({
  entries,
  search,
  onSearchChange,
  selectedExtensions,
  onExtensionToggle,
  availableExtensions,
}: GrfAssetTableProps) {
  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <h3>Assets</h3>
          <p className="muted-text">Busca e filtros locais sobre o resultado da inspecao read-only.</p>
        </div>
      </div>
      <div className="grf-asset-table__filters">
        <label className="grf-asset-table__search">
          <span>Buscar por nome/path</span>
          <input
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder="sprite, map, sound..."
          />
        </label>
        <div className="token-wrap" role="group" aria-label="Filtros por extensao">
          {availableExtensions.map((extension) => {
            const active = selectedExtensions.includes(extension);
            return (
              <button
                key={extension}
                type="button"
                className={`diff-workbench__tab ${active ? "diff-workbench__tab--active" : ""}`}
                onClick={() => onExtensionToggle(extension)}
              >
                {extension}
              </button>
            );
          })}
        </div>
      </div>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>Path</th>
              <th>Extension</th>
              <th>Compressed</th>
              <th>Decompressed</th>
              <th>Encrypted</th>
            </tr>
          </thead>
          <tbody>
            {entries.length ? (
              entries.map((entry) => (
                <tr key={entry.relativePath}>
                  <td className="mono-text">{entry.relativePath}</td>
                  <td>{entry.extension ?? "-"}</td>
                  <td>{String(entry.sizeCompressed ?? "-")}</td>
                  <td>{String(entry.sizeDecompressed ?? "-")}</td>
                  <td>{String(entry.encrypted ?? false)}</td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={5} className="muted-text">
                  Nenhum asset corresponde ao filtro atual.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}
