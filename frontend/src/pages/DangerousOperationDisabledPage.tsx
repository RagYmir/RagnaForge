import { useParams } from "react-router-dom";
import { PageHeader } from "../components/PageHeader";

export function DangerousOperationDisabledPage() {
  const params = useParams();

  return (
    <div className="stack-lg">
      <PageHeader
        title="Operação desabilitada"
        description="Apply/Rollback via interface está desabilitado nesta fase."
      />
      <section className="panel">
        <p>
          Rota solicitada: <span className="mono-text">/{params.entity}/{params.operation}</span>
        </p>
        <p>
          Esta interface consome apenas endpoints seguros: status, config, discover, GRF, dry-run e diff-preview.
        </p>
      </section>
    </div>
  );
}
