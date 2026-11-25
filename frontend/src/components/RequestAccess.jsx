import { Link } from 'react-router';
import { usePlanQuery, useRequestAccessMutation } from '../services/api/plan';
import { ErrorState } from './common';

/**
 * "Request access" for a portal or feature the company's plan does not include. It only tells the company's admin (and
 * whoever sets the plan) that someone wants it: it grants nothing. Asking again is harmless, so a request that is already
 * open is shown as sent instead of offering the button again.
 */
export default function RequestAccess({ portal, feature, link = true }) {
  const kind = portal ? 'portal' : 'feature';
  const name = portal || feature;
  const plan = usePlanQuery();
  const [send, { isLoading, isSuccess, error }] = useRequestAccessMutation();
  const sent = isSuccess || plan.data?.requests?.some((r) => r.mine && r.kind === kind && r.name === name && r.action !== 'remove');

  if (sent) {
    return (
      <p className="request-access-sent" role="status">
        Request sent.{link && <> Your admin can follow it on the <Link to="/plan">Plan page</Link>.</>}
      </p>
    );
  }
  return (
    <div className="request-access">
      <button type="button" className="button primary" disabled={isLoading} onClick={() => send(portal ? { portal } : { feature })}>
        {isLoading ? 'Sending…' : 'Request access'}
      </button>
      <ErrorState error={error} />
    </div>
  );
}
