import { useState } from 'react';
import Link from '../../components/PortalLink';
import { useSearchIssuesQuery } from '../../services/api/api';
import { useAuth } from '../auth/context';
import { QueryState, Pagination } from '../../components/common';
import WorkIssueList from './WorkIssueList';
export default function Subtasks({ issue }) {
  const { can } = useAuth();
  const [page, setPage] = useState(1);
  const query = useSearchIssuesQuery({
    ProjectId: issue.projectId,
    ParentIssueId: issue.id,
    Page: page,
    PageSize: 20,
  });
  return (
    <section className="panel subtasks-panel" aria-label="Child issues">
      <div className="section-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <svg
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
            style={{ color: '#64748b' }}
          >
            <rect x="3" y="3" width="7" height="7" rx="1.5" />
            <rect x="14" y="3" width="7" height="7" rx="1.5" />
            <rect x="14" y="14" width="7" height="7" rx="1.5" />
            <path d="M6.5 10v7a2 2 0 0 0 2 2h5.5" />
          </svg>
          <h2>Child issues</h2>
        </div>
        {issue.issueType !== 'SUBTASK' && can('ISSUE_CREATE') && can('PROJECT_VIEW') && (
          <Link className="button" to={`/issues/create?parent=${issue.id}`}>
            <svg
              width="13"
              height="13"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2.5"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <line x1="12" y1="5" x2="12" y2="19" />
              <line x1="5" y1="12" x2="19" y2="12" />
            </svg>
            Add child issue
          </Link>
        )}
      </div>
      <QueryState query={query}>
        {query.data?.items?.length ? (
          <WorkIssueList issues={query.data.items} />
        ) : (
          <div className="child-issues-empty" style={{ textAlign: 'center', padding: '32px 20px' }}>
            <div
              style={{
                width: '44px',
                height: '44px',
                borderRadius: '50%',
                background: '#f1f5f9',
                color: '#64748b',
                display: 'grid',
                placeItems: 'center',
                margin: '0 auto 12px',
              }}
              aria-hidden="true"
            >
              <svg
                width="22"
                height="22"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.8"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <rect x="3" y="3" width="7" height="7" rx="1.5" />
                <rect x="14" y="3" width="7" height="7" rx="1.5" />
                <rect x="14" y="14" width="7" height="7" rx="1.5" />
                <path d="M6.5 10v7a2 2 0 0 0 2 2h5.5" />
              </svg>
            </div>
            <strong style={{ display: 'block', fontSize: '14px', color: 'var(--theme-text, #0f172a)', marginBottom: '4px' }}>
              No child issues linked
            </strong>
            <p style={{ margin: 0, fontSize: '12.5px', color: 'var(--theme-muted, #64748b)' }}>
              Break this work item down into smaller, actionable subtasks.
            </p>
          </div>
        )}
        {query.data?.totalPages > 1 && (
          <Pagination data={query.data} busy={query.isFetching} onPage={setPage} />
        )}
      </QueryState>
    </section>
  );
}
