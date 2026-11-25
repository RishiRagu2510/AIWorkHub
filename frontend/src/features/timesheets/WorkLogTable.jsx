import { useState } from 'react';
import Link from '../../components/PortalLink';
import { useAuth } from '../auth/context';
import { useDeleteWorkLogMutation } from '../../services/api/timesheets';
import UserIdentity from '../../components/UserIdentity';
import { ErrorState } from '../../components/common';
import WorkLogForm from './WorkLogForm';
import { durationLabel } from './time';
export default function WorkLogTable({ items = [] }) {
  const { session, can } = useAuth();
  const [edit, setEdit] = useState(null);
  const [confirm, setConfirm] = useState(null);
  const [remove, removing] = useDeleteWorkLogMutation();
  const [error, setError] = useState(null);
  return (
    <>
      {edit && <WorkLogForm key={edit.id} log={edit} onClose={() => setEdit(null)} />}
      {confirm && (
        <div className="time-confirm" role="alert">
          <p>
            Delete this {durationLabel(confirm.durationMinutes)} hour work log? This cannot be
            undone.
          </p>
          <button
            disabled={removing.isLoading}
            onClick={async () => {
              try {
                await remove(confirm).unwrap();
                setConfirm(null);
              } catch (e) {
                setError(e);
              }
            }}
          >
            Confirm delete
          </button>
          <button disabled={removing.isLoading} onClick={() => setConfirm(null)}>
            Cancel
          </button>
        </div>
      )}
      <ErrorState error={error} />
      <div className="time-table-scroll">
        <table className="time-table">
          <thead>
            <tr>
              <th className="col-date">Date</th>
              <th className="col-issue">Issue / description</th>
              <th className="col-user">User</th>
              <th className="col-hours">Hours</th>
              <th className="col-billable">Billable</th>
              <th className="col-period">Period</th>
              <th className="col-actions">Actions</th>
            </tr>
          </thead>
          <tbody>
            {items.map((w) => {
              const editable =
                String(w.userId) === String(session.user.id) &&
                ['DRAFT', 'REJECTED'].includes(w.periodStatus);
              return (
                <tr key={w.id}>
                  <td className="col-date">
                    <span className="worklog-date-tag">
                      <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
                        <rect x="3" y="4" width="18" height="18" rx="2" />
                        <path d="M16 2v4M8 2v4M3 10h18" />
                      </svg>
                      {w.workDate}
                    </span>
                  </td>
                  <td className="col-issue">
                    <div className="worklog-issue-line">
                      <span className="worklog-key-pill">{w.issueKey}</span>
                      <Link className="worklog-title-link" to={`/issues/${w.issueId}`}>
                        {w.issueSummary || w.issueKey}
                      </Link>
                    </div>
                    {w.description && w.description !== w.issueSummary ? (
                      <small className="worklog-desc-text">
                        <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true" style={{ display: 'inline-block', marginRight: 4, verticalAlign: -1 }}>
                          <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
                        </svg>
                        {w.description}
                      </small>
                    ) : null}
                  </td>
                  <td className="col-user">
                    <UserIdentity id={w.userId} />
                  </td>
                  <td className="col-hours">
                    <strong className="worklog-hours-badge">{durationLabel(w.durationMinutes)}</strong>
                  </td>
                  <td className="col-billable">
                    <span className={`worklog-billable-badge ${w.billable ? 'is-billable' : 'is-non-billable'}`}>
                      <span className="badge-bullet" />
                      {w.billable ? 'Yes' : 'No'}
                    </span>
                  </td>
                  <td className="col-period">
                    <span className={`time-status status-${w.periodStatus?.toLowerCase()}`}>{w.periodStatus}</span>
                  </td>
                  <td className="col-actions">
                    {editable && can('WORKLOG_UPDATE') && (
                      <button className="time-action-btn edit-action" onClick={() => setEdit(w)} aria-label="Edit">
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                          <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                          <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                        </svg>
                        Edit
                      </button>
                    )}{' '}
                    {editable && can('WORKLOG_DELETE') && (
                      <button className="time-action-btn delete-action" onClick={() => setConfirm(w)} aria-label="Delete">
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                          <polyline points="3 6 5 6 21 6" />
                          <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                        </svg>
                        Delete
                      </button>
                    )}
                    {!editable && <span style={{ color: '#94a3b8' }}>—</span>}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      {!items.length && (
        <div className="worklog-empty-card">
          <div className="worklog-empty-icon" aria-hidden="true">
            <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="9" />
              <polyline points="12 6 12 12 16 14" />
            </svg>
          </div>
          <p className="time-empty">No time logged for this range and these filters.</p>
          <small>Log work or adjust your date range and filters to display entries.</small>
        </div>
      )}
    </>
  );
}
