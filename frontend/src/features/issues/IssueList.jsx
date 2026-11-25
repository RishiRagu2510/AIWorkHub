import UserIdentity from '../../components/UserIdentity';
import { Badge, EmptyState } from '../../components/common';
import { dateTime, label, formatDisplayKey } from '../../utils/format';
export function IssueList({ issues, selected, select }) {
  if (!issues?.length)
    return <EmptyState title="No issues found">Try changing your filters.</EmptyState>;
  return (
    <ul className="issue-card-list">
      {issues.map((issue) => (
        <li key={issue.id}>
          <button
            className={String(issue.id) === String(selected) ? 'issue-card selected' : 'issue-card'}
            aria-label={`${issue.issueKey}: ${issue.summary}`}
            onClick={() => select(issue.id)}
            aria-pressed={String(issue.id) === String(selected)}
          >
            <span className="issue-card-top">
              <span className="issue-key" title={issue.issueKey}>
                {formatDisplayKey(issue.issueKey)}
              </span>
              <span className="issue-type">{label(issue.issueType)}</span>
              <Badge value={issue.statusCode} />
            </span>
            <span className="issue-card-summary" style={{ fontWeight: 400 }}>{issue.summary}</span>
            <span className="issue-card-bottom">
              <UserIdentity id={issue.assigneeUserId} />
              <span className="issue-card-priority">{label(issue.priority)}</span>
            </span>
            <time>{dateTime(issue.updatedAt)}</time>
          </button>
        </li>
      ))}
    </ul>
  );
}
