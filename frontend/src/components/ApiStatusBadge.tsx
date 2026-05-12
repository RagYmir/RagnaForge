interface ApiStatusBadgeProps {
  label: string;
  tone?: "neutral" | "good" | "warning" | "danger";
}

export function ApiStatusBadge({ label, tone = "neutral" }: ApiStatusBadgeProps) {
  return <span className={`badge badge-${tone}`}>{label}</span>;
}
