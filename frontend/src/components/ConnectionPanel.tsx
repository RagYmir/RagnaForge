import { useState } from "react";
import { ApiClient, ApiClientError } from "../api/client";
import { useApiConfig } from "../features/connection/ApiConfigContext";
import { ProblemDetailsView } from "./ProblemDetailsView";

export function ConnectionPanel({
  compact = false,
  onClose,
}: {
  compact?: boolean;
  onClose?: () => void;
}) {
  const { connection, setConnection } = useApiConfig();
  const [draft, setDraft] = useState(connection);
  const [message, setMessage] = useState<string>();
  const [problem, setProblem] = useState<ApiClientError["problem"]>();
  const [busy, setBusy] = useState(false);

  async function testConnection() {
    setBusy(true);
    setProblem(undefined);
    setMessage(undefined);
    try {
      const client = new ApiClient(() => draft);
      await client.health();
      const status = await client.status();
      setMessage(`Conectado. ReadOnlyMode = ${String(status.data.readOnlyMode)}.`);
    } catch (error) {
      const apiError = error as ApiClientError;
      setProblem(apiError.problem);
      setMessage(undefined);
    } finally {
      setBusy(false);
    }
  }

  function save() {
    setConnection(draft);
    setMessage("Configuracao salva no localStorage deste navegador local.");
    if (onClose) {
      onClose();
    }
  }

  return (
    <section className={`panel ${compact ? "panel-compact" : ""}`}>
      <div className="panel-header">
        <h3>Conexao da API</h3>
      </div>
      <p className="muted-text">
        A chave fica armazenada apenas no navegador local durante esta fase de desenvolvimento.
      </p>
      <div className="form-grid">
        <label>
          <span>API Base URL</span>
          <input
            value={draft.baseUrl}
            onChange={(event) =>
              setDraft((current) => ({ ...current, baseUrl: event.target.value }))
            }
            placeholder="http://127.0.0.1:5099"
          />
        </label>
        <label>
          <span>API Key</span>
          <input
            type="password"
            value={draft.apiKey}
            onChange={(event) =>
              setDraft((current) => ({ ...current, apiKey: event.target.value }))
            }
            placeholder="X-RagnaForge-Api-Key"
          />
        </label>
      </div>
      <div className="button-row">
        <button className="button-primary" type="button" onClick={testConnection} disabled={busy}>
          {busy ? "Testando..." : "Testar conexao"}
        </button>
        <button className="button-secondary" type="button" onClick={save}>
          Salvar
        </button>
      </div>
      {message ? <p className="success-text">{message}</p> : null}
      <ProblemDetailsView problem={problem} />
    </section>
  );
}
