import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { ApiStatusBadge } from "../components/ApiStatusBadge";
import { ConnectionPanel } from "../components/ConnectionPanel";
import { useApiConfig } from "../features/connection/ApiConfigContext";

const navigation = [
  { to: "/", label: "Dashboard" },
  { to: "/configuracao", label: "Configuracao" },
  { to: "/discovery", label: "Discovery" },
  { to: "/grf", label: "GRF" },
  { to: "/itens", label: "Itens" },
  { to: "/equipamentos", label: "Equipamentos" },
  { to: "/npcs", label: "NPCs" },
  { to: "/monstros", label: "Monstros" },
  { to: "/mapas", label: "Mapas" },
  { to: "/validacao", label: "Validacao" },
  { to: "/auditoria", label: "Historico/Relatorios" },
  { to: "/agente", label: "Agent Health" },
  { to: "/pipeline-api", label: "Pipeline API" },
  { to: "/seguranca", label: "Seguranca/API" },
];

export function AppShell() {
  const { client, ready, connection } = useApiConfig();
  const [showConnection, setShowConnection] = useState(false);

  const statusQuery = useQuery({
    queryKey: ["status-header", connection.baseUrl, connection.apiKey],
    queryFn: () => client.status(),
    enabled: ready,
  });

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand-block">
          <p className="eyebrow">RagnaForge</p>
          <h1>Admin UI</h1>
          <p className="muted-text">Somente leitura, dry-run e diff-preview.</p>
        </div>
        <nav className="sidebar-nav">
          {navigation.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) => `nav-link ${isActive ? "nav-link-active" : ""}`}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <main className="content-shell">
        <header className="topbar">
          <div className="topbar-status">
            <ApiStatusBadge
              label={
                statusQuery.data?.data.readOnlyMode
                  ? "ReadOnlyMode = true"
                  : "ReadOnlyMode = false"
              }
              tone={statusQuery.data?.data.readOnlyMode ? "good" : "danger"}
            />
            <ApiStatusBadge
              label={ready ? "API key carregada" : "API key ausente"}
              tone={ready ? "warning" : "danger"}
            />
            <span className="mono-pill">{connection.baseUrl}</span>
          </div>
          <button
            className="button-secondary"
            type="button"
            onClick={() => setShowConnection((current) => !current)}
          >
            {showConnection ? "Fechar conexao" : "Conexao"}
          </button>
        </header>
        {showConnection ? <ConnectionPanel compact onClose={() => setShowConnection(false)} /> : null}
        <Outlet />
      </main>
    </div>
  );
}
