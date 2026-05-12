import { useParams } from "react-router-dom";
import { PageHeader } from "../components/PageHeader";

export function DiffBlockedPage() {
  const { entity } = useParams();

  return (
    <section className="page">
      <PageHeader
        title="Operacao desabilitada"
        description="Esta interface consome apenas endpoints seguros da API."
      />
      <div className="panel">
        <p>Apply/Rollback via interface esta desabilitado nesta fase.</p>
        <p>
          Rota solicitada: <code>{entity ?? "desconhecida"}</code>.
        </p>
      </div>
    </section>
  );
}
