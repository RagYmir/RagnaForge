export function JsonInspector({ title, value }: { title: string; value: unknown }) {
  if (value == null) {
    return null;
  }

  return (
    <details className="panel inspector" open>
      <summary>{title}</summary>
      <pre>{JSON.stringify(value, null, 2)}</pre>
    </details>
  );
}
