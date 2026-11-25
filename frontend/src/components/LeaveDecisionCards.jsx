import React, { useState } from 'react';

/**
 * Pending leave requests with Approve and Reject buttons. A button sends a plain chat message, so the
 * assistant carries the decision out through the same tool (and the same permission checks) as typing it.
 */
export default function LeaveDecisionCards({ requests, outcomes, disabled, onSend }) {
  const [rejecting, setRejecting] = useState(null);
  const [reason, setReason] = useState('');

  if (!requests || requests.length === 0) return null;

  const describe = (r) => `${r.type || 'leave'} ${r.from}${r.to && r.to !== r.from ? ` to ${r.to}` : ''}`;

  return (
    <div className="leave-decision-cards">
      <div className="leave-decision-header">Waiting for your decision</div>
      {requests.map((r) => {
        const outcome = outcomes?.[r.requestId];
        return (
          <div key={r.requestId} className="leave-decision-card">
            <div className="leave-decision-info">
              <span className="leave-decision-name">{r.employee}</span>
              <span className="leave-decision-meta">
                {r.type} · {r.from}{r.to && r.to !== r.from ? ` → ${r.to}` : ''} · {r.days} day{r.days === 1 ? '' : 's'}
              </span>
              {r.reason && <span className="leave-decision-reason">“{r.reason}”</span>}
            </div>

            {outcome ? (
              <span className={`chat-status-pill tone-${outcome === 'APPROVED' ? 'success' : 'danger'}`}>{outcome}</span>
            ) : rejecting === r.requestId ? (
              <form
                className="leave-reject-form"
                onSubmit={(e) => {
                  e.preventDefault();
                  if (!reason.trim()) return;
                  onSend(`Reject leave request ${r.requestId} for ${r.employee} (${describe(r)}). Reason: ${reason.trim()}`);
                  setRejecting(null);
                  setReason('');
                }}
              >
                <input
                  autoFocus
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  placeholder="Reason shown to the employee"
                  aria-label="Reason for rejecting"
                  maxLength={200}
                />
                <button type="submit" className="leave-btn reject" disabled={disabled || !reason.trim()}>Confirm</button>
                <button type="button" className="leave-btn ghost" onClick={() => { setRejecting(null); setReason(''); }}>Cancel</button>
              </form>
            ) : (
              <div className="leave-decision-actions">
                <button
                  type="button"
                  className="leave-btn approve"
                  disabled={disabled}
                  onClick={() => onSend(`Approve leave request ${r.requestId} for ${r.employee} (${describe(r)})`)}
                >
                  ✓ Approve
                </button>
                <button type="button" className="leave-btn reject-outline" disabled={disabled} onClick={() => setRejecting(r.requestId)}>
                  ✕ Reject
                </button>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
