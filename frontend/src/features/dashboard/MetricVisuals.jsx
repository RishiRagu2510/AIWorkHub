import { label } from '../../utils/format';
const colors = ['#f59e0b', '#38bdf8', '#a78bfa', '#2dd4bf', '#fb7185', '#4ade80', '#94a3b8'];
export function MiniBreakdown({ entries, caption }) {
  const visible = entries.filter((entry) => entry.count > 0);
  const total = visible.reduce((sum, entry) => sum + entry.count, 0);
  return (
    <div className="metric-visual" role="group" aria-label={caption}>
      <div className="metric-segments" aria-hidden="true">
        {visible.map((entry, index) => (
          <span
            key={entry.name}
            style={{ flex: entry.count, background: colors[index % colors.length] }}
          />
        ))}
      </div>
      <div className="metric-mini-caption">{total ? caption : 'No data to chart yet'}</div>
      {total > 0 && (
        <ul className="metric-mini-legend">
          {visible.map((entry, index) => (
            <li key={entry.name}>
              <i aria-hidden="true" style={{ background: colors[index % colors.length] }} />
              {label(entry.name)} <span>{entry.count}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
export function MiniProgress({ count, total }) {
  const percent = total > 0 ? Math.min(100, Math.round((count / total) * 100)) : 0;
  return (
    <div className="metric-visual">
      <div
        className="metric-segments"
        role="progressbar"
        aria-label="Issues in progress"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={percent}
        aria-valuetext={`${count} of ${total} issues`}
      >
        <span style={{ width: `${percent}%`, background: '#38bdf8', flex: 'none' }} />
      </div>
      <div className="metric-mini-caption">
        <b>{percent}%</b> of tracked issues
      </div>
      <p className="metric-mini-detail">
        {count} in progress / {total} total
      </p>
    </div>
  );
}
