import { useState } from 'react';
import { useRequestAccessMutation } from '../services/api/plan';
import { ErrorState } from './common';

/**
 * "Cancel plan" for a portal or tool the company's plan includes. Only a company admin sees it, and it never removes anything by
 * itself: it asks whoever operates the platform, who approves or declines it. Nothing changes for anyone until they approve, and a
 * request that is already open is shown as under review instead of offering the button again. The server makes the same checks.
 */
export default function CancelPlan({ portal, feature, name, pending, alsoRemoves }) {
  const [confirming, setConfirming] = useState(false);
  const [send, { isLoading, error, reset }] = useRequestAccessMutation();

  if (pending) {
    return (
      <span className="plan-cancel-pending" role="status">
        Cancellation under review
      </span>
    );
  }
  if (!confirming) {
    return (
      <button type="button" className="button plan-cancel-button" aria-label={`Cancel ${name}`} onClick={() => setConfirming(true)}>
        Cancel plan
      </button>
    );
  }
  return (
    <div className="plan-cancel-confirm" role="group" aria-label={`Cancel ${name}`}>
      <p>
        Ask to cancel <strong>{name}</strong>?{alsoRemoves ? ` ${alsoRemoves}` : ''} Your AIWorkHub representative reviews this, and nothing changes until they approve it.
      </p>
      <div className="plan-cancel-actions">
        <button
          type="button"
          className="button plan-cancel-send"
          disabled={isLoading}
          onClick={async () => {
            const result = await send({ ...(portal ? { portal } : { feature }), action: 'remove' });
            if (!result.error) setConfirming(false);
          }}
        >
          {isLoading ? 'Sending…' : 'Send cancellation request'}
        </button>
        <button type="button" className="button plan-cancel-keep" disabled={isLoading} onClick={() => { reset(); setConfirming(false); }}>
          Keep {name}
        </button>
      </div>
      <ErrorState error={error} />
    </div>
  );
}
