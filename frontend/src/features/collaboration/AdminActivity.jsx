import { useState } from 'react';
import { useActivityQuery } from '../../services/api/collaboration';
import { liveQueryOptions } from '../../services/api/live';
import { useCollaborationLive } from './useLive';
import { ActivityList } from './IssueCollaboration';
import { QueryState, Pagination } from '../../components/common';
export default function AdminActivity() {
  const [page, setPage] = useState(1);
  const query = useActivityQuery(page, liveQueryOptions);
  const live = useCollaborationLive('ACTIVITY', 0);
  return (
    <section
      className="panel collaboration-panel organization-activity"
      aria-labelledby="organization-activity-title"
    >
      <header className="organization-activity-header">
        <h2 id="organization-activity-title">Organization activity</h2>
        <span
          className={`organization-activity-status${live.state === 'Connected' ? ' is-connected' : ''}`}
          role="status"
          aria-label={`Activity updates: ${live.state}`}
        >
          <span aria-hidden="true" />
          {live.state === 'Connected' ? 'Live' : live.state}
        </span>
      </header>
      <QueryState query={query}>
        <div
          className="organization-activity-feed"
          role="region"
          aria-label="Recent organization activity"
          tabIndex={query.data?.items?.length ? 0 : undefined}
        >
          <ActivityList items={query.data?.items} compact />
        </div>
        {query.data?.totalPages > 1 && (
          <Pagination data={query.data} busy={query.isFetching} onPage={setPage} />
        )}
      </QueryState>
    </section>
  );
}
