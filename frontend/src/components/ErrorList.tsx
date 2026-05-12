export function ErrorList({ errors }: { errors?: string[] }) {
  if (!errors?.length) {
    return null;
  }

  return (
    <div className="message-card error-card">
      <h3>Errors</h3>
      <ul className="flat-list">
        {errors.map((error) => (
          <li key={error}>{error}</li>
        ))}
      </ul>
    </div>
  );
}
