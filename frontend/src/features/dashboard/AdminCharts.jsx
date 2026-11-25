import Link from '../../components/PortalLink';
import { QueryState, EmptyState } from '../../components/common';
import { label } from '../../utils/format';

const donutColors = ['#f59e0b', '#38bdf8', '#a78bfa', '#2dd4bf', '#fb7185', '#4ade80', '#94a3b8'];

const gaugeColors = [
  '#e11d48', // Crimson / Magenta
  '#3b82f6', // Bright Blue
  '#c084fc', // Orchid / Purple
  '#2dd4bf', // Teal / Cyan
  '#93c5fd', // Soft Light Blue
  '#fb7185', // Coral / Salmon
  '#4ade80', // Emerald Green
  '#f59e0b', // Amber
  '#94a3b8', // Slate
];

function getCoords(cx, cy, r, deg) {
  const rad = (deg * Math.PI) / 180;
  return {
    x: cx + r * Math.cos(rad),
    y: cy + r * Math.sin(rad),
  };
}

function describeArc(cx, cy, r, startDeg, endDeg) {
  const p1 = getCoords(cx, cy, r, startDeg);
  const p2 = getCoords(cx, cy, r, endDeg);
  const sweepAngle = endDeg >= startDeg ? endDeg - startDeg : endDeg + 360 - startDeg;
  const largeArc = sweepAngle > 180 ? 1 : 0;
  return `M ${p1.x.toFixed(2)} ${p1.y.toFixed(2)} A ${r} ${r} 0 ${largeArc} 1 ${p2.x.toFixed(2)} ${p2.y.toFixed(2)}`;
}

function IssueGaugeChart({ data = [] }) {
  const total = data.reduce((sum, item) => sum + item.count, 0);

  if (!total)
    return (
      <EmptyState title="No issues to chart">
        Issue status counts will appear as work is added.
      </EmptyState>
    );

  const cx = 210;
  const cy = 180;
  const startDeg = 145;
  const totalSweep = 250;
  const baseRadius = 118;
  const strokeWidth = 9;
  const gap = 6;

  // Maximum scale for tracks
  const maxCount = data.reduce((m, item) => Math.max(m, item.count), 0);
  const maxScale = Math.max(Math.ceil(total * 1.25), Math.ceil(maxCount * 1.3), 10);

  return (
    <div className="admin-chart-content project-gauge-content">
      {/* Multi-Ring Radial Bar Gauge */}
      <div className="gauge-svg-wrap" role="img" aria-label={`${total} issues across all statuses`}>
        <svg viewBox="0 0 420 240" className="project-gauge-svg" aria-hidden="true">
          {/* Concentric track rings */}
          {data.map((item, index) => {
            const r = baseRadius - index * (strokeWidth + gap);
            if (r < 25) return null;
            const fraction = Math.min(1, Math.max(0, item.count / maxScale));
            const bgPath = describeArc(cx, cy, r, startDeg, startDeg + totalSweep);
            const activePath =
              fraction > 0.005 ? describeArc(cx, cy, r, startDeg, startDeg + fraction * totalSweep) : null;
            const color = gaugeColors[index % gaugeColors.length];

            return (
              <g key={item.status} className="gauge-ring">
                <path
                  d={bgPath}
                  fill="none"
                  stroke="currentColor"
                  strokeWidth={strokeWidth}
                  strokeLinecap="round"
                  className="gauge-track-bg"
                />
                {activePath && (
                  <path
                    d={activePath}
                    fill="none"
                    stroke={color}
                    strokeWidth={strokeWidth}
                    strokeLinecap="round"
                    className="gauge-track-fill"
                  />
                )}
              </g>
            );
          })}

          {/* Center count & label */}
          <g className="gauge-center-badge">
            <text x={cx} y={cy - 4} textAnchor="middle" className="gauge-center-val">
              {total.toLocaleString()}
            </text>
            <text x={cx} y={cy + 13} textAnchor="middle" className="gauge-center-sub">
              Total
            </text>
          </g>
        </svg>
      </div>

      {/* Right Legend Card (Matching Mockup) */}
      <aside className="gauge-legend-card">
        <div className="gauge-legend-title">Legend</div>
        <ul className="chart-legend" aria-label="Issue counts by status">
          {data.map((item, index) => {
            const color = gaugeColors[index % gaugeColors.length];
            const percent = total > 0 ? Math.round((item.count / total) * 100) : 0;
            return (
              <li key={item.status} className="gauge-legend-row">
                <span
                  className="gauge-swatch"
                  style={{ backgroundColor: color }}
                  aria-hidden="true"
                />
                <Link to={`/issues?Status=${item.status}`} className="gauge-legend-link">
                  <span className="gauge-type-name">{label(item.status)}</span>
                  <strong className="gauge-type-count">{item.count.toLocaleString()}</strong>
                  <small className="gauge-type-pct">({percent}%)</small>
                </Link>
              </li>
            );
          })}
        </ul>
      </aside>
    </div>
  );
}

function ProjectDonutChart({ data = [] }) {
  if (!data.length)
    return (
      <EmptyState title="No projects to chart">
        Your project mix will appear here once projects are created.
      </EmptyState>
    );

  const grouped = data.reduce((groups, project) => {
    const type = project.projectType || 'UNSPECIFIED';
    groups[type] = (groups[type] || 0) + 1;
    return groups;
  }, {});
  const groups = Object.entries(grouped).sort(([a], [b]) => a.localeCompare(b));
  const total = data.length;

  return (
    <div className="admin-chart-content status-donut-content">
      <div className="status-donut" role="img" aria-label={`${total} projects across all types`}>
        <svg viewBox="0 0 120 120" aria-hidden="true">
          {groups.map(([type, count], index) => (
            <circle
              key={type}
              cx="60"
              cy="60"
              r="48"
              fill="none"
              stroke={donutColors[index % donutColors.length]}
              strokeWidth="16"
              pathLength="100"
              strokeDasharray={`${(count / total) * 100} ${100 - (count / total) * 100}`}
              strokeDashoffset={
                (-groups.slice(0, index).reduce((sum, [, x]) => sum + x, 0) / total) * 100
              }
              transform="rotate(-90 60 60)"
            />
          ))}
        </svg>
        <div>
          <strong>{total.toLocaleString()}</strong>
          <span>projects</span>
        </div>
      </div>
      <ul className="status-chart" aria-label="Project counts by type">
        {groups.map(([type, count], index) => (
          <li key={type}>
            <span className="legend-dot" style={{ background: donutColors[index % donutColors.length] }} />
            <Link to={`/projects?Type=${encodeURIComponent(type)}`}>
              <span className="chart-label">{label(type)}</span>
            </Link>
            <strong>{count.toLocaleString()}</strong>
            <small>{Math.round((count / total) * 100)}%</small>
          </li>
        ))}
      </ul>
    </div>
  );
}

export default function AdminCharts({ issues, projects }) {
  if (!issues && !projects) return null;
  return (
    <div className="admin-charts">
      {issues && (
        <section className="panel gauge-panel" aria-labelledby="issue-chart-title">
          <div className="section-header">
            <div className="project-title-row">
              <h2 id="issue-chart-title">Issue Overview</h2>
              <span className="gauge-view-badge">(Gauge View)</span>
            </div>
            <p>Current issue counts by status · select a status to explore</p>
          </div>
          <QueryState query={issues}>
            <IssueGaugeChart data={issues.data} />
          </QueryState>
        </section>
      )}
      {projects && (
        <section className="panel" aria-labelledby="project-chart-title">
          <div className="section-header">
            <div>
              <h2 id="project-chart-title">Project mix</h2>
              <p>Accessible projects grouped by type</p>
            </div>
            <Link to="/projects">View projects →</Link>
          </div>
          <QueryState query={projects}>
            <ProjectDonutChart data={projects.data} />
          </QueryState>
        </section>
      )}
    </div>
  );
}
