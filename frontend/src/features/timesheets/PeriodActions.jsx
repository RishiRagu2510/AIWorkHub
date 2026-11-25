import { useState } from 'react';
import { useAuth } from '../auth/context';
import {
  useTimesheetApproversQuery,
  useTimesheetActionMutation,
} from '../../services/api/timesheets';
import { ErrorState } from '../../components/common';
export default function PeriodActions({ period }) {
  const { session, can } = useAuth();
  const own = String(period.userId) === String(session.user.id);
  const [mode, setMode] = useState(null);
  const [reason, setReason] = useState('');
  const [approver, setApprover] = useState('');
  const [error, setError] = useState(null);
  const candidates = useTimesheetApproversQuery(period.id, { skip: mode !== 'submit' });
  const [act, acting] = useTimesheetActionMutation();
  async function perform(action) {
    if (action === 'reject' && !reason.trim()) {
      setError({ message: 'A rejection reason is required.' });
      return;
    }
    if (action === 'submit' && !approver) {
      setError({ message: 'Select an approver.' });
      return;
    }
    try {
      await act({
        action,
        version: period.version,
        periodId: period.id,
        ...(action === 'submit' ? { approverUserId: Number(approver) } : {}),
        ...(action === 'reject' ? { reason } : {}),
      }).unwrap();
      setMode(null);
      setError(null);
    } catch (e) {
      setError(e);
    }
  }
  return (
    <div className="time-period-actions">
      {own && ['DRAFT', 'REJECTED'].includes(period.status) && can('TIMESHEET_VIEW') && (
        <button
          className="submit-period-btn"
          disabled={!period.totalMinutes || acting.isLoading}
          onClick={() => setMode('submit')}
        >
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true" style={{ marginRight: 6 }}>
            <line x1="22" y1="2" x2="11" y2="13" />
            <polygon points="22 2 15 22 11 13 2 9 22 2" />
          </svg>
          Submit
        </button>
      )}
      {!own &&
        period.status === 'SUBMITTED' &&
        can('TIMESHEET_APPROVE') &&
        can('TIMESHEET_TEAM_VIEW') && (
          <div className="period-approval-group">
            <button className="approve-period-btn" disabled={acting.isLoading} onClick={() => perform('approve')}>
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true" style={{ marginRight: 5 }}>
                <polyline points="20 6 9 17 4 12" />
              </svg>
              Approve
            </button>
            <button className="reject-period-btn" disabled={acting.isLoading} onClick={() => setMode('reject')}>
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true" style={{ marginRight: 5 }}>
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
              Reject
            </button>
          </div>
        )}
      {mode === 'submit' && (
        <div className="period-action-drawer">
          <div className="drawer-header">
            <strong>Submit Timesheet for Review</strong>
            <button type="button" className="drawer-close-btn" onClick={() => setMode(null)} aria-label="Close">✕</button>
          </div>
          <p className="drawer-note">The approver must have access to all issues in this week.</p>
          <label className="field-drawer">
            <span>Approver</span>
            <select value={approver} onChange={(e) => setApprover(e.target.value)}>
              <option value="">Choose approver</option>
              {candidates.data?.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.displayName}
                </option>
              ))}
            </select>
          </label>
          <ErrorState error={candidates.error} retry={candidates.refetch} />
          <div className="drawer-actions">
            <button className="button primary" disabled={acting.isLoading || !approver} onClick={() => perform('submit')}>
              Confirm submission
            </button>
            <button className="button-cancel" onClick={() => setMode(null)}>Cancel</button>
          </div>
        </div>
      )}
      {mode === 'reject' && (
        <div className="period-action-drawer">
          <div className="drawer-header">
            <strong>Reject Timesheet</strong>
            <button type="button" className="drawer-close-btn" onClick={() => setMode(null)} aria-label="Close">✕</button>
          </div>
          <label className="field-drawer">
            <span>Rejection reason</span>
            <textarea
              placeholder="State why this timesheet is being rejected..."
              value={reason}
              maxLength={2000}
              onChange={(e) => setReason(e.target.value)}
            />
          </label>
          <div className="drawer-actions">
            <button className="button danger" disabled={acting.isLoading} onClick={() => perform('reject')}>
              Confirm rejection
            </button>
            <button className="button-cancel" onClick={() => setMode(null)}>Cancel</button>
          </div>
        </div>
      )}
      <ErrorState error={error} />
    </div>
  );
}
