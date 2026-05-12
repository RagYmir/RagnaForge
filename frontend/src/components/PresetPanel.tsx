import type { FormPreset } from "../features/shared/pipelinePresets";

export function PresetPanel<T>({
  title,
  presets,
  onApply,
}: {
  title: string;
  presets: FormPreset<T>[];
  onApply: (value: T) => void;
}) {
  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <h3>{title}</h3>
          <p className="muted-text">Preset seguro: apenas preenche o formulario.</p>
        </div>
      </div>
      <div className="stack-sm">
        {presets.map((preset) => (
          <article key={preset.key} className="preset-card">
            <div>
              <h4>{preset.label}</h4>
              <p className="muted-text">{preset.description}</p>
            </div>
            <button type="button" onClick={() => onApply(preset.value)}>
              Usar preset
            </button>
          </article>
        ))}
      </div>
    </section>
  );
}
