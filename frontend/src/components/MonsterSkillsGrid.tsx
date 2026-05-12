import type { PipelineReport } from "../api/types";

export function MonsterSkillsGrid({ skills }: { skills?: PipelineReport["skills"] }) {
  if (!skills?.length) {
    return (
      <section className="panel">
        <div className="panel-header">
          <h3>Skills</h3>
        </div>
        <p className="muted-text">Nenhuma skill planejada.</p>
      </section>
    );
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <h3>Skills</h3>
      </div>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>ID</th>
              <th>Level</th>
              <th>State</th>
              <th>Target</th>
              <th>Condition</th>
              <th>Anchor</th>
              <th>Supported</th>
            </tr>
          </thead>
          <tbody>
            {skills.map((skill, index) => (
              <tr key={`skill-${index}`}>
                <td>{String(skill.skillId ?? "-")}</td>
                <td>{String(skill.skillLevel ?? "-")}</td>
                <td>{String(skill.state ?? "-")}</td>
                <td>{String(skill.target ?? "-")}</td>
                <td>{String(skill.conditionType ?? "-")}</td>
                <td>{String(skill.anchor ?? "-")}</td>
                <td>{String(skill.supported ?? false)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
