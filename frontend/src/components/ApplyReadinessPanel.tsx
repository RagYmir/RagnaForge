import { ReadinessBadge } from "./ReadinessBadge";

export function ApplyReadinessPanel({
  label,
  readiness,
  blockReasons
}: {
  label: string;
  readiness?: string;
  blockReasons?: string[];
}) {
  if (!readiness && !blockReasons?.length) {
    return null;
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <h3>{label}</h3>
        <ReadinessBadge value={readiness} />
      </div>
      {blockReasons?.length ? (
        <ul className="flat-list">
          {blockReasons.map((reason) => (
            <li key={reason}>{reason}</li>
          ))}
        </ul>
      ) : (
        <p>Nenhum bloqueio adicional foi reportado.</p>
      )}
    </section>
  );
}
