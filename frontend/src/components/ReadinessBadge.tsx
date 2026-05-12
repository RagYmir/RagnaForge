export function ReadinessBadge({ value }: { value?: string }) {
  if (!value) {
    return null;
  }

  const tone = /blocked|error|unsafe|deny/i.test(value)
    ? "danger"
    : /warning|partial|manual/i.test(value)
      ? "warning"
      : "good";

  return <span className={`badge badge-${tone}`}>{value}</span>;
}
