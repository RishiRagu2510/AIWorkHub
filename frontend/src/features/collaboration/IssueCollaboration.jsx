import { useState } from 'react';
import { useAuth } from '../auth/context';
import UserIdentity from '../../components/UserIdentity';
import { QueryState, ErrorState, Pagination } from '../../components/common';
import {
  useWatchersQuery,
  useWatchMutation,
  useIssueActivityQuery,
} from '../../services/api/collaboration';
import { liveQueryOptions } from '../../services/api/live';
import AuditChanges from '../audit/AuditChanges';
import { dateTime, label } from '../../utils/format';
export function Presence({ live }) {
  return (
    <section className="panel collaboration-panel" aria-label="Issue presence">
      <p role="status">Live updates: {live.state}</p>
      <ul>
        {live.presence.map((p) => (
          <li key={p.userId}>
            <UserIdentity id={p.userId} /> —{' '}
            {p.typing ? 'typing…' : p.editing ? 'editing' : 'viewing'}
          </li>
        ))}
      </ul>
    </section>
  );
}
export function Watchers({ id }) {
  const { session } = useAuth();
  const query = useWatchersQuery(id, liveQueryOptions);
  const [watch, mutation] = useWatchMutation();
  const watching = query.data?.some((user) => String(user) === String(session.user.id));
  return (
    <section className="panel collaboration-panel">
      <h2>Watchers</h2>
      <QueryState query={query}>
        <p>{query.data?.length || 0} watching</p>
        <button
          disabled={mutation.isLoading || query.isFetching}
          onClick={() => watch({ id, enabled: !watching })}
        >
          {watching ? 'Stop watching' : 'Watch issue'}
        </button>
        <ul>
          {query.data?.map((user) => (
            <li key={user}>
              <UserIdentity id={user} />
            </li>
          ))}
        </ul>
      </QueryState>
      <ErrorState error={mutation.error} />
    </section>
  );
}
export function ActivityList({ items = [], compact = false }) {
  return items.length ? (
    <ol className={`issue-timeline${compact ? ' compact-activity-list' : ''}`}>
      {items.map((e) => (
        <li key={e.eventId}>
          {compact ? (
            <>
              <div className="activity-event-header">
                {e.actorUserId ? <UserIdentity id={e.actorUserId} /> : <strong>System</strong>}
                <time dateTime={e.occurredAt}>{dateTime(e.occurredAt)}</time>
              </div>
              <span className="activity-event-action">{label(e.action)}</span>
            </>
          ) : (
            <>
              <p>
                {e.actorUserId ? <UserIdentity id={e.actorUserId} /> : 'System'} · {label(e.action)}
              </p>
              <time dateTime={e.occurredAt}>{dateTime(e.occurredAt)}</time>
            </>
          )}
          {!compact && <AuditChanges oldValues={e.oldValues} newValues={e.newValues} />}
        </li>
      ))}
    </ol>
  ) : (
    <p>No activity yet. Recent changes may take a moment to appear.</p>
  );
}
export function IssueHistory({ id }) {
  const [page, setPage] = useState(1);
  const query = useIssueActivityQuery({ id, page }, liveQueryOptions);
  return (
    <section className="panel collaboration-panel">
      <h2>Issue history</h2>
      <QueryState query={query}>
        <ActivityList items={query.data?.items} />
        {query.data?.totalPages > 1 && (
          <Pagination data={query.data} busy={query.isFetching} onPage={setPage} />
        )}
      </QueryState>
    </section>
  );
}
