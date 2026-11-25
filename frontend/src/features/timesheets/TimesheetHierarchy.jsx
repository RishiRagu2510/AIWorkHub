import { useState } from 'react';
import Link from '../../components/PortalLink';
import { useTimesheetHierarchyQuery } from '../../services/api/timesheets';
import { liveQueryOptions } from '../../services/api/live';
import { QueryState, Pagination } from '../../components/common';
import UserIdentity from '../../components/UserIdentity';
import { durationLabel } from './time';

function IssueBranch({ node, nodes, visited = [] }) {
  if (visited.includes(node.issueId)) return null;
  const children = nodes.filter((n) => n.parentIssueId === node.issueId);
  const typeLower = (node.issueType || 'task').toLowerCase();

  return (
    <li className={`time-branch-item type-${typeLower}`}>
      <div className="time-tree-issue">
        <span className={`time-status issue-type-badge type-${typeLower}`}>
          {node.issueType === 'SUBTASK' ? 'SUBTASK' : node.issueType}
        </span>
        <Link to={`/issues/${node.issueId}`} className="time-tree-issue-link">
          {node.issueKey} · {node.summary}
        </Link>
        <strong className="time-tree-hours">{durationLabel(node.minutes)}</strong>
        <small className="time-tree-context">
          {node.minutes ? `${durationLabel(node.billableMinutes)} billable` : 'Parent context'}
        </small>
      </div>
      {children.length > 0 && (
        <ul className="time-tree-sublist">
          {children.map((child) => (
            <IssueBranch
              key={child.issueId}
              node={child}
              nodes={nodes}
              visited={[...visited, node.issueId]}
            />
          ))}
        </ul>
      )}
    </li>
  );
}
export default function TimesheetHierarchy({ filters, team }) {
  const [page, setPage] = useState(1);
  const query = useTimesheetHierarchyQuery(
    { ...filters, team, page, pageSize: 20 },
    liveQueryOptions,
  );
  return (
    <section className="panel time-section time-hierarchy">
      <div className="time-section-header">
        <div className="time-submissions-title-row">
          <span className="time-submissions-icon">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="6" y1="3" x2="6" y2="15" />
              <circle cx="18" cy="6" r="3" />
              <circle cx="6" cy="18" r="3" />
              <path d="M18 9a9 9 0 0 1-9 9" />
            </svg>
          </span>
          <div>
            <h2>Time by user & project</h2>
            <p className="time-section-subtext">
              Hierarchical breakdown of time logged across users, projects, and parent-child issues.
            </p>
          </div>
        </div>
      </div>

      <QueryState query={query}>
        {query.data?.items.map((group) => (
          <details key={`${group.userId}:${group.projectId}`} open className="time-hierarchy-group">
            <summary className="time-hierarchy-summary">
              <span className="summary-user">
                <UserIdentity id={group.userId} />
              </span>
              <span className="summary-project-pill">
                <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
                </svg>
                {group.projectName}
              </span>
              <strong className="summary-total-hours">{durationLabel(group.minutes)}</strong>
              <small className="summary-billable-hours">{durationLabel(group.billableMinutes)} billable</small>
            </summary>
            <ul className="time-tree-root-list">
              {group.issues
                .filter(
                  (n) =>
                    !n.parentIssueId || !group.issues.some((p) => p.issueId === n.parentIssueId),
                )
                .map((node) => (
                  <IssueBranch key={node.issueId} node={node} nodes={group.issues} />
                ))}
            </ul>
          </details>
        ))}
        {query.data?.items.length === 0 && (
          <div className="timesheet-empty-card">
            <div className="timesheet-empty-icon" aria-hidden="true">
              <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="10" />
                <polyline points="12 6 12 12 16 14" />
              </svg>
            </div>
            <p>No time logged for this range and these filters.</p>
            <small>Log work or adjust your date range and filters to display entries.</small>
          </div>
        )}
        {query.data?.totalPages > 1 && (
          <Pagination data={query.data} onPage={setPage} busy={query.isFetching} />
        )}
      </QueryState>
    </section>
  );
}
