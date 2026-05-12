export function BytecodeBlockPanel({ files }: { files?: string[] }) {
  if (!files?.length) {
    return null;
  }

  return (
    <section className="panel panel--danger">
      <div className="panel-header">
        <h3>Bytecode blocks</h3>
      </div>
      <p className="muted-text">
        Estes arquivos foram classificados como bytecode ou formato inseguro para escrita. O
        frontend nao tenta editar, converter nem contornar esse bloqueio.
      </p>
      <ul className="flat-list">
        {files.map((file) => (
          <li key={file} className="mono-text">
            {file}
          </li>
        ))}
      </ul>
    </section>
  );
}
