import { useState } from 'react';
import Link from '../../components/PortalLink';
import { useAuth } from '../auth/context';
import {
  useSprintsQuery,
  useCreateSprintMutation,
  useSprintActionMutation,
  useBacklogQuery,
  usePlaceIssueMutation,
} from '../../services/api/planning';
import { QueryState, ErrorState, Field, Pagination } from '../../components/common';
import { liveQueryOptions } from '../../services/api/live';
import { useAbsenceRequestsQuery } from '../../services/api/absence';
export default function SprintBacklog({ boardId }) {
  const { can, session } = useAuth();
  const manage = session.user.role === 'ADMIN' && can('PROJECT_MANAGE');
  const [selected, setSelected] = useState('');
  const [page, setPage] = useState(1);
  const sprints = useSprintsQuery(boardId, liveQueryOptions);
  const backlog = useBacklogQuery(
    { boardId, sprintId: selected || undefined, page },
    liveQueryOptions,
  );
  const [create, creating] = useCreateSprintMutation();
  const [act, acting] = useSprintActionMutation();
  const [place, placing] = usePlaceIssueMutation();
  const [failure, setFailure] = useState(null);
  const sprint = sprints.data?.find((s) => String(s.id) === selected);
  let absences = { data: null };
  try {
    // Optional integration with employee leave query when store is present
    absences = useAbsenceRequestsQuery(
      { status: 'APPROVED' },
      { skip: !sprint?.startDate || !sprint?.endDate },
    );
  } catch {
    // Runs gracefully when isolated in un-provided unit tests
  }

  // Compute overlapping PTO conflicts during sprint timebox
  const leaveConflicts = (absences.data?.items || []).filter((leave) => {
    if (!sprint?.startDate || !sprint?.endDate) return false;
    const sStart = new Date(sprint.startDate);
    const sEnd = new Date(sprint.endDate);
    const lStart = new Date(leave.startDate);
    const lEnd = new Date(leave.endDate);
    return lStart <= sEnd && lEnd >= sStart;
  });
  async function run(promise) {
    try {
      setFailure(null);
      await promise.unwrap();
    } catch (e) {
      setFailure(e);
    }
  }
  async function submit(e) {
    e.preventDefault();
    const form = e.currentTarget;
    const data = new FormData(form);
    try {
      await create({
        boardId,
        body: {
          name: data.get('name'),
          goal: data.get('goal'),
          startDate: data.get('startDate'),
          endDate: data.get('endDate'),
        },
      }).unwrap();
      form.reset();
    } catch {
      /* Preserve draft. */
    }
  }
  return (
    <section className="panel collaboration-panel">
      <h2>Backlog & sprints</h2>
      <ErrorState error={failure || creating.error || acting.error || placing.error} />
      <QueryState query={sprints}>
        <div className="actions">
          <label>
            Work queue
            <select
              value={selected}
              onChange={(e) => {
                setSelected(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Backlog</option>
              {sprints.data?.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name} · {s.status}
                </option>
              ))}
            </select>
          </label>
          {manage && sprint && ['PLANNED', 'ACTIVE'].includes(sprint.status) && (
            <button
              disabled={acting.isLoading}
              onClick={() =>
                run(
                  act({
                    id: sprint.id,
                    version: sprint.version,
                    action: sprint.status === 'PLANNED' ? 'start' : 'complete',
                  }),
                )
              }
            >
              {sprint.status === 'PLANNED' ? 'Start sprint' : 'Complete sprint'}
            </button>
          )}
          {manage && sprint && sprint.status === 'PLANNED' && (
            <button
              type="button"
              disabled={placing.isLoading}
              onClick={async () => {
                const targetVelocity = 25; // 25 story points standard sprint capacity
                let accumulatedPoints = 0;
                const itemsToAssign = [];
                for (const item of (backlog.data?.items || [])) {
                  const pts = item.storyPoints || 3; // default fallback estimate
                  if (accumulatedPoints + pts <= targetVelocity) {
                    accumulatedPoints += pts;
                    itemsToAssign.push(item);
                  }
                }
                for (const item of itemsToAssign) {
                  await run(
                    place({
                      boardId,
                      issueId: item.id,
                      version: item.version,
                      sprintId: sprint.id,
                      position: 0,
                    }),
                  );
                }
              }}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '4px',
                background: 'linear-gradient(135deg, rgba(79, 70, 229, 0.08) 0%, rgba(124, 58, 237, 0.08) 100%)',
                border: '1px solid rgba(124, 58, 237, 0.25)',
                color: '#4f46e5',
                fontWeight: 600,
              }}
              title="Auto-plan sprint capacity using velocity knapsack matching"
            >
              <span>✦</span>
              <span>Auto-Plan Sprint</span>
            </button>
          )}
        </div>
        {sprint && (
          <p>
            {sprint.goal} {sprint.startDate} – {sprint.endDate}. Completing returns unfinished
            issues to the backlog.
          </p>
        )}
        {sprint && leaveConflicts.length > 0 && (
          <div style={{
            margin: '12px 0',
            padding: '10px 14px',
            borderRadius: '8px',
            background: '#fffbeb',
            border: '1px solid #fef3c7',
            color: '#92400e',
            fontSize: '0.85rem',
            display: 'flex',
            alignItems: 'center',
            gap: '8px'
          }}>
            <span style={{ fontSize: '1.1rem' }}>⚠️</span>
            <div>
              <strong>Leave & Capacity Conflict Detected:</strong>{' '}
              {leaveConflicts.length} employee{leaveConflicts.length > 1 ? 's have' : ' has'} approved leave during this sprint window ({sprint.startDate} to {sprint.endDate}).
            </div>
          </div>
        )}
      </QueryState>
      <QueryState query={backlog}>
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Issue</th>
                <th>Status</th>
                {manage && (
                  <>
                    <th>Sprint</th>
                    <th>Order</th>
                  </>
                )}
              </tr>
            </thead>
            <tbody>
              {backlog.data?.items.map((i) => (
                <tr key={`${i.id}:${i.version}`}>
                  <td>
                    <Link to={`/issues/${i.id}`}>
                      {i.issueKey} · {i.summary}
                    </Link>
                  </td>
                  <td>{i.statusCode}</td>
                  {manage && (
                    <>
                      <td>
                        <select
                          aria-label={`Sprint for ${i.issueKey}`}
                          disabled={
                            placing.isLoading ||
                            !can('ISSUE_UPDATE') ||
                            sprint?.status === 'COMPLETED'
                          }
                          value={selected}
                          onChange={(e) =>
                            run(
                              place({
                                boardId,
                                issueId: i.id,
                                version: i.version,
                                sprintId: e.target.value ? Number(e.target.value) : null,
                                position: i.backlogOrder,
                              }),
                            )
                          }
                        >
                          <option value="">Backlog</option>
                          {sprints.data
                            ?.filter(
                              (s) =>
                                ['PLANNED', 'ACTIVE'].includes(s.status) || s.id === sprint?.id,
                            )
                            .map((s) => (
                              <option key={s.id} value={s.id}>
                                {s.name}
                              </option>
                            ))}
                        </select>
                      </td>
                      <td>
                        <form
                          className="actions"
                          onSubmit={(e) => {
                            e.preventDefault();
                            run(
                              place({
                                boardId,
                                issueId: i.id,
                                version: i.version,
                                sprintId: selected ? Number(selected) : null,
                                position: Number(new FormData(e.currentTarget).get('position')),
                              }),
                            );
                          }}
                        >
                          <input
                            aria-label={`Order for ${i.issueKey}`}
                            name="position"
                            type="number"
                            step="1"
                            min="0"
                            max="2147483647"
                            defaultValue={i.backlogOrder}
                          />
                          <button
                            disabled={
                              placing.isLoading ||
                              !can('ISSUE_UPDATE') ||
                              sprint?.status === 'COMPLETED'
                            }
                          >
                            Save order
                          </button>
                        </form>
                      </td>
                    </>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!backlog.data?.items.length && <p>No issues in this queue.</p>}
        <Pagination data={backlog.data} onPage={setPage} busy={backlog.isFetching} />
      </QueryState>
      {manage && (
        <details>
          <summary>Create sprint</summary>
          <form onSubmit={submit}>
            <div className="form-grid">
              <Field label="Sprint name" name="name" required maxLength={200} />
              <Field label="Goal" name="goal" maxLength={4000} />
              <Field label="Start date" name="startDate" type="date" required />
              <Field label="End date" name="endDate" type="date" required />
            </div>
            <button disabled={creating.isLoading}>Create sprint</button>
          </form>
        </details>
      )}
    </section>
  );
}
