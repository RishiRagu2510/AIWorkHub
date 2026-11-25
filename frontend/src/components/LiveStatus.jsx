import { dateTime } from '../utils/format';
export function LiveStatus({ query, refresh }) {
  return (
    <div className="live-status">
      <span>
        {query?.isFetching
          ? 'Updating…'
          : query?.error
            ? 'Updates interrupted — retry available'
            : 'Auto-refresh every 30 seconds'}
      </span>
      <span>
        Last updated:{' '}
        {query?.fulfilledTimeStamp ? dateTime(query.fulfilledTimeStamp) : 'Waiting for data'}
      </span>
      {refresh && (
        <button type="button" onClick={refresh} disabled={query?.isFetching}>
          Refresh now
        </button>
      )}
    </div>
  );
}
