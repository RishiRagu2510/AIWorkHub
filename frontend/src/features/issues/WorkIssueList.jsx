import Link from '../../components/PortalLink';
import { Badge } from '../../components/common';
import { Icon } from '../../components/Icon';

function formatDueRelative(dueDateStr, todayStr) {
  if (!dueDateStr) return null;
  const due = new Date(dueDateStr.slice(0, 10));
  const today = todayStr ? new Date(todayStr) : new Date();
  // Normalize both dates to midnight UTC for day difference
  const dueUtc = Date.UTC(due.getFullYear(), due.getMonth(), due.getDate());
  const todayUtc = Date.UTC(today.getFullYear(), today.getMonth(), today.getDate());
  const diffDays = Math.round((dueUtc - todayUtc) / (1000 * 60 * 60 * 24));

  if (diffDays < 0) {
    const days = Math.abs(diffDays);
    return {
      type: 'overdue',
      label: `Overdue by ${days} day${days === 1 ? '' : 's'}`,
      tone: 'critical',
    };
  } else if (diffDays === 0) {
    return {
      type: 'today',
      label: 'Due today',
      tone: 'attention',
    };
  } else if (diffDays <= 7) {
    return {
      type: 'soon',
      label: `Due in ${diffDays} day${diffDays === 1 ? '' : 's'}`,
      tone: 'neutral',
    };
  }
  return {
    type: 'future',
    label: `Due ${dueDateStr.slice(0, 10)}`,
    tone: 'neutral',
  };
}

export default function WorkIssueList({ issues = [], group = 'OTHER', today }) {
  if (!issues.length) {
    const emptyConfigs = {
      OVERDUE: {
        icon: 'check',
        color: '#10b981',
        title: 'All caught up!',
        message: 'No overdue assignments on your plate. Great job staying ahead of deadlines!',
      },
      DUE_SOON: {
        icon: 'clock',
        color: '#3b82f6',
        title: 'No urgent deadlines',
        message: 'You have no assignments due today or within the next 7 days.',
      },
      IN_PROGRESS: {
        icon: 'dashboard',
        color: '#6366f1',
        title: 'No active tasks',
        message: 'No work currently in progress. Pick up an issue from your queue when ready.',
      },
      OTHER: {
        icon: 'folder',
        color: '#64748b',
        title: 'Queue is clear',
        message: 'No other open work items currently assigned to you.',
      },
    };

    const cfg = emptyConfigs[group] || emptyConfigs.OTHER;

    return (
      <div className="work-empty-state" style={{ padding: '24px 20px', textAlign: 'center' }}>
        <div
          style={{
            width: '42px',
            height: '42px',
            borderRadius: '50%',
            background: `${cfg.color}15`,
            color: cfg.color,
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            marginBottom: '10px',
          }}
        >
          <Icon name={cfg.icon} />
        </div>
        <div style={{ fontSize: '14px', fontWeight: 500, marginBottom: '4px', color: 'var(--theme-text, #1e293b)' }}>
          {cfg.title}
        </div>
        <p style={{ margin: 0, fontSize: '12px', color: 'var(--theme-muted, #64748b)', maxWidth: '38ch', marginInline: 'auto' }}>
          {cfg.message}
        </p>
      </div>
    );
  }

  return (
    <ul className="work-issue-list">
      {issues.map((issue) => {
        const dueInfo = formatDueRelative(issue.dueDate, today);

        return (
          <li key={issue.id} className="work-issue-item">
            <Link to={`/issues/${issue.id}`} className="work-issue-main">
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '2px' }}>
                <span className="issue-key" style={{ fontWeight: 500 }}>{issue.issueKey}</span>
                {issue.storyPoints != null && (
                  <span className="badge" style={{ fontSize: '10px' }}>{issue.storyPoints} pts</span>
                )}
              </div>
              <span className="work-issue-title" style={{ fontWeight: 400 }}>{issue.summary}</span>
            </Link>

            <div className="work-issue-meta" style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
              <Badge value={issue.statusCode} />
              <Badge value={issue.priority} />

              {dueInfo && (
                <span
                  className={`due-pill is-${dueInfo.type}`}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                    fontSize: '11px',
                    fontWeight: 500,
                    padding: '2px 8px',
                    borderRadius: '4px',
                    background:
                      dueInfo.type === 'overdue'
                        ? '#fee2e2'
                        : dueInfo.type === 'today'
                          ? '#fef3c7'
                          : '#f1f5f9',
                    color:
                      dueInfo.type === 'overdue'
                        ? '#dc2626'
                        : dueInfo.type === 'today'
                          ? '#b45309'
                          : '#475569',
                    border:
                      dueInfo.type === 'overdue'
                        ? '1px solid #fca5a5'
                        : dueInfo.type === 'today'
                          ? '1px solid #fde68a'
                          : '1px solid #cbd5e1',
                  }}
                >
                  <Icon name="clock" />
                  {dueInfo.label}
                </span>
              )}
            </div>
          </li>
        );
      })}
    </ul>
  );
}
