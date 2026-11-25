import { useOnboardingRequestQuery } from '../../../src/services/api/onboarding';
import './issue-details-panel.scss';
import IssueWorkLogs from '../../../src/features/timesheets/IssueWorkLogs';
import WorkLogForm from '../../../src/features/timesheets/WorkLogForm';
import Subtasks from '../../../src/features/issues/Subtasks';
import VersionConflict from '../../../src/components/VersionConflict';
import { useAuth } from '../../../src/features/auth/context';
const IssueAssignment = lazy(() => import('../../../src/features/issues/IssueAssignment'));
import UserIdentity from '../../../src/components/UserIdentity';
import { IssueHistory } from '../../../src/features/collaboration/IssueCollaboration';
import { PresenceContext, useCollaborationLive } from '../../../src/features/collaboration/useLive';
import { useWorkflowQuery, allowedStatuses, normalNext } from '../../../src/services/api/collaboration';
import { useRef, useState, lazy, Suspense } from 'react';
import { useNavigate, useParams } from 'react-router';
import Link from '../../../src/components/PortalLink';
import {
  useIssueQuery,
  useTransitionIssueMutation,
  useDeleteIssueMutation,
  useProjectsQuery,
} from '../../../src/services/api/api';
import { PageHeader, QueryState, ConfirmDialog, SelectField } from '../../../src/components/common';
import { Icon } from '../../../src/components/Icon';
import { useNotice } from '../../../src/components/noticeContext';
import { PermissionGuard } from '../../../src/features/auth/AuthProvider';
import OnboardingReview from '../../../src/features/employees/OnboardingReview';
import IssueAttachments from '../../../src/features/comments/IssueAttachments';
import { IssueComments } from '../../../src/features/comments/IssueComments';

import { dateTime, label } from '../../../src/utils/format';

function formatMinutes(min) {
  if (!min || min <= 0) return '0m';
  const h = Math.floor(min / 60);
  const m = min % 60;
  if (h === 0) return `${m}m`;
  if (m === 0) return `${h}h`;
  return `${h}h ${m}m`;
}

function Details({ issue, issueId }) {
  const { can } = useAuth();
  const onboarding = useOnboardingRequestQuery(issueId);
  const showIssueTools = onboarding.data?.isEmployeeRequest === false;
  const canSelectAssignee = can('ISSUE_ASSIGN');
  const navigate = useNavigate();
  const notice = useNotice();
  const [confirm, setConfirm] = useState(null);
  const [logWorkOpen, setLogWorkOpen] = useState(false);
  const [transitionVersion, setTransitionVersion] = useState(issue.version);
  const [pendingStatus, setPendingStatus] = useState(null);
  const transitionLock = useRef(false);
  const [pendingFrom, setPendingFrom] = useState(null);
  const workflow = useWorkflowQuery(issue.projectId);
  const nextStatus = normalNext(workflow.data, issue.statusCode);
  const statuses = allowedStatuses(workflow.data, issue.statusCode, can);
  const live = useCollaborationLive('ISSUE', issueId);
  const [transition, transitionState] = useTransitionIssueMutation();
  const [remove, deleteState] = useDeleteIssueMutation();
  function changeStatus(e) {
    e.preventDefault();
    if (transitionLock.current) return;
    const target = new FormData(e.currentTarget).get('statusCode');
    if (!statuses.includes(target) || target === issue.statusCode) return;
    if (transitionState.error?.status === 412) return;
    transitionState.reset();
    if (workflow.data?.isDefault && target !== nextStatus) {
      setPendingFrom(issue.statusCode);
      setPendingStatus(target);
      return;
    }
    applyStatus(target);
  }
  async function applyStatus(statusCode, version = transitionVersion, confirmSkip = false) {
    if (transitionLock.current) return;
    transitionLock.current = true;
    try {
      const result = await transition({ id: issueId, statusCode, version, confirmSkip }).unwrap();
      setTransitionVersion(result.version);
      setPendingStatus(null);
      notice('Issue status updated.');
    } catch {
      /* Keep the selected target and show the server error for retry. */
    } finally {
      transitionLock.current = false;
    }
  }
  async function deleteIssue() {
    try {
      await remove({ id: issueId, version: confirm.version }).unwrap();
      notice('Issue deleted.');
      navigate('/issues');
    } catch {
      /* Keep dialog open for retry. */
    }
  }
  const projectsQuery = useProjectsQuery();
  const project = projectsQuery.data?.find((p) => p.id === issue.projectId);

  const metadata = [
    [
      'Project',
      project ? (
        <span key="project-badge" className="meta-project-chip">
          <svg
            width="14"
            height="14"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
          </svg>
          {project.name}
          {project.key && project.key !== project.name ? <small>({project.key})</small> : null}
        </span>
      ) : (
        `#${issue.projectId}`
      ),
    ],
    [
      'Type',
      <span
        key="type-badge"
        className={`meta-type-chip type-${(issue.issueType || '').toLowerCase()}`}
      >
        {label(issue.issueType)}
      </span>,
    ],
    [
      'Priority',
      <span
        key="priority-badge"
        className={`meta-priority-chip priority-${(issue.priority || '').toLowerCase()}`}
      >
        <span className="priority-dot" aria-hidden="true" />
        {label(issue.priority)}
      </span>,
    ],
    ['Reporter', <UserIdentity key="reporter" id={issue.reporterUserId} />],
    [
      issue.assigneeUserIds && issue.assigneeUserIds.length > 1 ? 'Assignees' : 'Assignee',
      issue.assigneeUserIds && issue.assigneeUserIds.length > 0 ? (
        <span key="assignees" style={{ display: 'flex', flexWrap: 'wrap', gap: '6px' }}>
          {issue.assigneeUserIds.map((uid) => (
            <UserIdentity key={uid} id={uid} />
          ))}
        </span>
      ) : issue.assigneeUserId ? (
        <UserIdentity key="assignee" id={issue.assigneeUserId} />
      ) : (
        <span key="unassigned" className="unassigned-chip">
          Unassigned
        </span>
      ),
    ],
    ['Story points', issue.storyPoints ?? '—'],
    ['Due date', issue.dueDate || '—'],
    ['Parent issue', issue.parentIssueId ? `#${issue.parentIssueId}` : '—'],
    ['Component', issue.componentId ? `#${issue.componentId}` : '—'],
    ['Created', dateTime(issue.createdAt)],
    ['Updated', dateTime(issue.updatedAt)],
    ['Resolved', dateTime(issue.resolvedAt)],
  ];
  return (
    <PresenceContext.Provider value={live}>
      <PageHeader eyebrow={issue.issueKey} title={issue.summary}>
        <PermissionGuard permission="ISSUE_CREATE">
          <Link className="button action-btn-duplicate" to={`/issues/create?copy=${issueId}`}>
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
              <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
            </svg>
            Duplicate issue
          </Link>
        </PermissionGuard>
        <PermissionGuard permission="ISSUE_UPDATE">
          <Link className="button action-btn-edit" to={`/issues/${issueId}/edit`}>
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <path d="M12 20h9" />
              <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" />
            </svg>
            Edit issue
          </Link>
        </PermissionGuard>
        <PermissionGuard permission="ISSUE_DELETE">
          <button
            className="danger-outline action-btn-delete"
            onClick={() => {
              deleteState.reset();
              setConfirm({ version: issue.version });
            }}
          >
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <polyline points="3 6 5 6 21 6" />
              <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
            </svg>
            Delete
          </button>
        </PermissionGuard>
      </PageHeader>
      {showIssueTools && <section className="panel time-tracking-panel">
        <div className="section-header">
          <h2>Time tracking</h2>
          {(can('WORKLOG_CREATE') || can('TIMESHEET_VIEW') || can('ISSUE_UPDATE')) && (
            <button className="primary" onClick={() => setLogWorkOpen(!logWorkOpen)}>
              <Icon name="clock" /> {logWorkOpen ? 'Close form' : 'Log work'}
            </button>
          )}
        </div>
        <div className="panel-body">
          <div className="time-tracking-metrics">
            <div className="time-metric">
              <span className="time-metric-label">Estimated:</span>
              <strong>{formatMinutes(issue.originalEstimateMinutes)}</strong>
            </div>
            <div className="time-metric">
              <span className="time-metric-label">Logged:</span>
              <strong style={{ color: '#10b981' }}>{formatMinutes(issue.loggedTimeMinutes)}</strong>
            </div>
            <div className="time-metric">
              <span className="time-metric-label">Remaining:</span>
              <strong style={{ color: '#f59e0b' }}>{formatMinutes(issue.remainingEstimateMinutes)}</strong>
            </div>
          </div>
          {(() => {
            const orig = issue.originalEstimateMinutes ?? 0;
            const log = issue.loggedTimeMinutes ?? 0;
            const rem = issue.remainingEstimateMinutes ?? 0;
            const total = orig > 0 ? orig : log + rem;
            const pct = total > 0 ? Math.min(100, Math.round((log / total) * 100)) : 0;
            return (
              <div className="time-progress-bar-container" style={{ margin: '12px 0 6px' }}>
                <div className="time-progress-bar-track" style={{ height: '8px', background: 'var(--theme-background, #f1f5f9)', borderRadius: '4px', overflow: 'hidden' }}>
                  <div
                    className="time-progress-bar-fill"
                    style={{ height: '100%', width: `${pct}%`, background: '#10b981', borderRadius: '4px', transition: 'width 0.3s ease' }}
                  />
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '11px', color: 'var(--theme-muted, #64748b)', marginTop: '4px' }}>
                  <span>{pct}% logged</span>
                  {total > 0 && <span>Total: {formatMinutes(total)}</span>}
                </div>
              </div>
            );
          })()}
          {issue.labels?.length > 0 && (
            <div className="issue-labels-row" style={{ marginTop: '10px' }}>
              <span className="muted">Labels:</span>{' '}
              {issue.labels.map((l) => (
                <span key={l} className="badge" style={{ marginRight: '4px' }}>{l}</span>
              ))}
            </div>
          )}
        </div>
      </section>}

      {showIssueTools && logWorkOpen && (
        <div className="panel" style={{ marginBottom: '16px', borderLeft: '4px solid #f59e0b' }}>
          <WorkLogForm issue={issue} onClose={() => setLogWorkOpen(false)} />
        </div>
      )}

      <div className="detail-grid">
        <div className="detail-main">
          <OnboardingReview id={issueId} />
          <section className="panel issue-description-panel">
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
                  <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                  <polyline points="14 2 14 8 20 8" />
                  <line x1="16" y1="13" x2="8" y2="13" />
                  <line x1="16" y1="17" x2="8" y2="17" />
                  <polyline points="10 9 9 9 8 9" />
                </svg>
                <h2>Description</h2>
              </div>
            </div>
            <div className="panel-body preserve-lines">
              {issue.description ? (
                <div className="description-content">{issue.description}</div>
              ) : (
                <span className="muted" style={{ fontStyle: 'italic', fontSize: '13px' }}>
                  No description provided.
                </span>
              )}
            </div>
          </section>
          {showIssueTools && (can('WORKLOG_VIEW') || can('TIMESHEET_VIEW') || can('ISSUE_VIEW')) && (
            <IssueWorkLogs issue={issue} />
          )}
          <Subtasks key={issue.id} issue={issue} />
          <IssueComments id={issueId} issue={issue} />
          {showIssueTools && <IssueAttachments id={issueId} />}
        </div>
        <aside className="panel metadata issue-details-panel">
          <div className="section-header">
            <h2>Details</h2>
          </div>
          <dl>
            {metadata.map(([title, value]) => (
              <div key={title}>
                <dt>{title}</dt>
                <dd>{value}</dd>
              </div>
            ))}
          </dl>
          <PermissionGuard permission="ISSUE_TRANSITION">
            <QueryState query={workflow}>
              <form className="transition-form" onSubmit={changeStatus} key={issue.statusCode}>
                <SelectField
                  label="Move to status"
                  name="statusCode"
                  onChange={() => {
                    if (!transitionState.error) setTransitionVersion(issue.version);
                  }}
                  values={statuses.filter((status) => status !== issue.statusCode)}
                  defaultValue={nextStatus || ''}
                  empty={nextStatus ? undefined : 'Select a status'}
                  required
                  disabled={transitionState.isLoading}
                />
                <p className="hint">
                  {nextStatus
                    ? `Next step: ${label(nextStatus)}. Other moves require confirmation.`
                    : workflow.data?.isDefault
                      ? 'Changing this status requires confirmation.'
                      : 'Transitions follow the configured project workflow.'}
                </p>
                <button
                  className="primary"
                  disabled={
                    transitionState.isLoading ||
                    !workflow.data ||
                    workflow.isFetching ||
                    !statuses.length
                  }
                >
                  {transitionState.isLoading ? 'Updating…' : 'Transition issue'}
                </button>
                <VersionConflict
                  error={pendingStatus ? null : transitionState.error}
                  path={`/api/issues/${issueId}`}
                  onReviewed={(current) => {
                    setTransitionVersion(current.version);
                    transitionState.reset();
                  }}
                />
              </form>
            </QueryState>
          </PermissionGuard>
          <PermissionGuard permission="ISSUE_ASSIGN">
            {canSelectAssignee ? (
              <Suspense fallback={<p>Loading assignment controls…</p>}>
                <IssueAssignment key={issue.projectId} issue={issue} />
              </Suspense>
            ) : (
              <div className="assignment">
                <button disabled aria-describedby="assignment-reason">
                  Assign issue
                </button>
                <p className="hint" id="assignment-reason">
                  Selecting assignees requires issue assignment permission.
                </p>
              </div>
            )}
          </PermissionGuard>
        </aside>
      </div>
      {showIssueTools && <IssueHistory id={issueId} />}
      {pendingStatus && pendingFrom === issue.statusCode && (
        <PermissionGuard permission="ISSUE_TRANSITION">
          <ConfirmDialog
            title="Confirm status change"
            confirmLabel="Confirm transition"
            busyLabel="Updating…"
            confirmClass="primary"
            close={() => setPendingStatus(null)}
            confirm={() => {
              if (pendingFrom === issue.statusCode)
                applyStatus(pendingStatus, transitionVersion, true);
            }}
            busy={transitionState.isLoading}
            error={transitionState.error?.status === 412 ? null : transitionState.error}
            confirmDisabled={transitionState.error?.status === 412}
          >
            <VersionConflict
              error={transitionState.error?.status === 412 ? transitionState.error : null}
              path={`/api/issues/${issueId}`}
              onReviewed={(current) => {
                setTransitionVersion(current.version);
                transitionState.reset();
              }}
            />
            <p>
              Move {issue.issueKey} from <strong>{label(issue.statusCode)}</strong> to{' '}
              <strong>{label(pendingStatus)}</strong>?
            </p>
            <p>
              {nextStatus
                ? `This bypasses the normal next step, ${label(nextStatus)}.`
                : 'This move is outside the normal workflow sequence.'}
            </p>
          </ConfirmDialog>
        </PermissionGuard>
      )}
      {confirm && (
        <ConfirmDialog
          title={`Delete ${issue.issueKey}?`}
          close={() => setConfirm(false)}
          confirm={deleteIssue}
          busy={deleteState.isLoading}
          error={deleteState.error?.status === 412 ? null : deleteState.error}
          confirmDisabled={deleteState.error?.status === 412}
        >
          <p>This action cannot be undone.</p>
          <VersionConflict
            error={deleteState.error?.status === 412 ? deleteState.error : null}
            path={`/api/issues/${issueId}`}
            onReviewed={(current) => {
              setConfirm({ version: current.version });
              deleteState.reset();
            }}
          />
        </ConfirmDialog>
      )}
    </PresenceContext.Provider>
  );
}
export default function IssueDetails({ selectedId }) {
  const params = useParams();
  const issueId = selectedId || params.issueId;
  const query = useIssueQuery(String(issueId), {
    pollingInterval: 30000,
    refetchOnFocus: true,
    refetchOnReconnect: true,
  });
  return (
    <QueryState query={query}>
      {query.data && <Details key={issueId} issue={query.data} issueId={String(issueId)} />}
    </QueryState>
  );
}
