import { Icon } from '../../components/Icon';
import VersionConflict from '../../components/VersionConflict';
import UserIdentity from '../../components/UserIdentity';
import { useRef, useState } from 'react';
import Link from '../../components/PortalLink';
import { useTransitionIssueMutation } from '../../services/api/api';
import { useAuth } from '../auth/context';
import { useNotice } from '../../components/noticeContext';
import { ConfirmDialog, ErrorState, Badge, EmptyState } from '../../components/common';
import { useWorkflowsQuery, allowedStatuses, normalNext } from '../../services/api/collaboration';
import { label, formatDisplayKey } from '../../utils/format';

export default function IssueBoard({ issues, busy, workflows, boardStatuses }) {
  const { can } = useAuth();
  const notice = useNotice();
  const projectIds = [...new Set(issues.map((i) => i.projectId).filter(Boolean))];
  const query = useWorkflowsQuery(projectIds, { skip: !!workflows || !projectIds.length });
  const definitions = workflows || query.currentData || {};
  const targets = (issue) => allowedStatuses(definitions[issue.projectId], issue.statusCode, can);
  const statuses = [
    ...new Set([
      ...(boardStatuses || []),
      ...Object.values(definitions).flatMap((w) => w.definition.statuses),
      ...issues.map((i) => i.statusCode),
    ]),
  ];
  const [transition, mutation] = useTransitionIssueMutation();
  const [pending, setPending] = useState(null);
  const lock = useRef(false);
  const dragged = useRef(null);
  const enabled = can('ISSUE_TRANSITION') && !busy && !mutation.isLoading;
  async function move(issue, status, confirmSkip = false) {
    if (!enabled || lock.current) return;
    lock.current = true;
    try {
      await transition({
        id: String(issue.id),
        statusCode: status,
        ...(confirmSkip ? { confirmSkip: true } : {}),
        version: issue.version,
      }).unwrap();
      setPending(null);
      notice(`${issue.issueKey} moved to ${label(status)}.`);
    } catch (error) {
      if (error.status === 412) setPending({ issue, status });
    } finally {
      lock.current = false;
    }
  }
  function requestMove(issue, status) {
    if (!enabled || !issue || !targets(issue).includes(status) || status === issue.statusCode)
      return;
    mutation.reset();
    if (
      !definitions[issue.projectId]?.isDefault ||
      status === normalNext(definitions[issue.projectId], issue.statusCode)
    )
      move(issue, status);
    else setPending({ issue, status });
  }
  const current = pending && issues.find((issue) => issue.id === pending.issue.id);
  const stale = pending && (!current || current.version !== pending.issue.version);
  if (!issues.length)
    return (
      <EmptyState title="No issues match these filters" variant="issues">
        Clear filters or choose another project to see its work.
      </EmptyState>
    );
  return (
    <div className="board-workspace">
      <ErrorState error={query.error} />
      <p className="hint">
        Showing issues on this results page. Drag a card or use its Move to menu. Skipping workflow
        steps requires confirmation.
      </p>
      {!pending && <ErrorState error={mutation.error} />}
      <div className="issue-board" aria-label="Issue workflow board" tabIndex="0">
        {statuses.map((status) => {
          const cards = issues.filter((issue) => issue.statusCode === status);
          return (
            <section
              key={status}
              className="board-column"
              aria-label={label(status)}
              onDragOver={(event) => {
                if (enabled) event.preventDefault();
              }}
              onDrop={(event) => {
                event.preventDefault();
                const id = event.dataTransfer.getData('text/plain');
                requestMove(
                  String(dragged.current?.id) === id
                    ? dragged.current
                    : issues.find((issue) => String(issue.id) === id),
                  status,
                );
              }}
            >
              <h2>
                {label(status)} <span>{cards.length}</span>
              </h2>
              {cards.length ? (
                cards.map((issue) => (
                  <article
                    key={issue.id}
                    className="board-card"
                    draggable={enabled}
                    onDragStart={(event) => {
                      dragged.current = issue;
                      event.dataTransfer.setData('text/plain', String(issue.id));
                      event.dataTransfer.effectAllowed = 'move';
                    }}
                  >
                    <div className="board-card-header">
                      <span className="board-issue-key" title={issue.issueKey}>
                        {formatDisplayKey(issue.issueKey)}
                      </span>
                      <Badge value={issue.priority} />
                    </div>
                    <Link
                      className="board-card-title"
                      to={`/issues/${issue.id}`}
                      aria-label={`${issue.issueKey}: ${issue.summary}`}
                    >
                      <span className="board-card-summary">{issue.summary}</span>
                    </Link>
                    <div className="board-card-footer">
                      <div className="board-assignee">
                        {issue.assigneeUserId == null && (
                          <span className="board-unassigned-avatar" title="Unassigned">
                            <Icon name="user" />
                          </span>
                        )}
                        <UserIdentity id={issue.assigneeUserId} />
                      </div>
                      {can('ISSUE_TRANSITION') && (
                        <div className="board-card-actions">
                          <select
                            name="transitionStatus"
                            className="board-status-select"
                            aria-label={`Move ${issue.issueKey} to`}
                            value=""
                            title="Move to another status"
                            disabled={!enabled}
                            onChange={(event) => requestMove(issue, event.target.value)}
                          >
                            <option value="">Move ▾</option>
                            {targets(issue)
                              .filter((value) => value !== status)
                              .map((value) => (
                                <option key={value} value={value}>
                                  {label(value)}
                                  {value === normalNext(definitions[issue.projectId], status)
                                    ? ' (next)'
                                    : ''}
                                </option>
                              ))}
                          </select>
                        </div>
                      )}
                    </div>
                  </article>
                ))
              ) : (
                <div className="column-empty-state">
                  <span>No issues</span>
                </div>
              )}
            </section>
          );
        })}
      </div>
      {pending && (
        <ConfirmDialog
          title="Confirm status change"
          confirmLabel="Confirm transition"
          busyLabel="Updating…"
          confirmClass="primary"
          close={() => setPending(null)}
          busy={mutation.isLoading}
          confirmDisabled={mutation.error?.status === 412 || (stale && !pending.reviewed)}
          error={mutation.error?.status === 412 ? null : mutation.error}
          confirm={() => (!stale || pending.reviewed) && move(pending.issue, pending.status, true)}
        >
          <VersionConflict
            error={
              mutation.error?.status === 412
                ? mutation.error
                : stale && !pending.reviewed
                  ? { status: 412 }
                  : null
            }
            path={`/api/issues/${pending.issue.id}`}
            onReviewed={(current) => {
              setPending({ ...pending, issue: current, reviewed: true });
              mutation.reset();
            }}
          />
          <p>
            Move {pending.issue.issueKey} from <strong>{label(pending.issue.statusCode)}</strong> to{' '}
            <strong>{label(pending.status)}</strong>?
          </p>
          <p>This move is outside the normal next workflow step.</p>
        </ConfirmDialog>
      )}
    </div>
  );
}
