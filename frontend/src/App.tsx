import { Route, Routes } from "react-router-dom";
import { ConnectionPanel } from "./components/ConnectionPanel";
import { useApiConfig } from "./features/connection/ApiConfigContext";
import { AppShell } from "./layouts/AppShell";
import { AuditPage } from "./pages/AuditPage";
import { AgentHealthPage } from "./pages/AgentHealthPage";
import { ConfigPage } from "./pages/ConfigPage";
import { DashboardPage } from "./pages/DashboardPage";
import { DiscoveryPage } from "./pages/DiscoveryPage";
import { EquipmentPage } from "./pages/EquipmentPage";
import { GrfPage } from "./pages/GrfPage";
import { ItemsPage } from "./pages/ItemsPage";
import { MapsPage } from "./pages/MapsPage";
import { MonstersPage } from "./pages/MonstersPage";
import { NotFoundPage } from "./pages/NotFoundPage";
import { NpcsPage } from "./pages/NpcsPage";
import { SecurityPage } from "./pages/SecurityPage";
import { ValidationPage } from "./pages/ValidationPage";

function ConnectionGate() {
  return (
    <div className="connection-gate">
      <div className="connection-gate__panel">
        <h1>RagnaForge Admin UI</h1>
        <p>
          Configure a conexao com a API local endurecida. Esta interface opera
          apenas em modo leitura, dry-run e diff-preview.
        </p>
        <ConnectionPanel />
      </div>
    </div>
  );
}

export default function App() {
  const { ready } = useApiConfig();

  if (!ready) {
    return <ConnectionGate />;
  }

  return (
    <Routes>
      <Route path="/" element={<AppShell />}>
        <Route index element={<DashboardPage />} />
        <Route path="configuracao" element={<ConfigPage />} />
        <Route path="discovery" element={<DiscoveryPage />} />
        <Route path="grf" element={<GrfPage />} />
        <Route path="itens" element={<ItemsPage />} />
        <Route path="equipamentos" element={<EquipmentPage />} />
        <Route path="npcs" element={<NpcsPage />} />
        <Route path="monstros" element={<MonstersPage />} />
        <Route path="mapas" element={<MapsPage />} />
        <Route path="validacao" element={<ValidationPage />} />
        <Route path="auditoria" element={<AuditPage />} />
        <Route path="agente" element={<AgentHealthPage />} />
        <Route path="seguranca" element={<SecurityPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}
