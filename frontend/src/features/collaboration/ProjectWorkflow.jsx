import { useState } from 'react';
import { useAuth } from '../auth/context';
import { useWorkflowQuery, useSaveWorkflowMutation } from '../../services/api/collaboration';
import { QueryState, ErrorState } from '../../components/common';
const statusConfig = {
  TODO: { label: 'To Do', color: '#475569', bg: '#f1f5f9', border: '#cbd5e1' },
  IN_PROGRESS: { label: 'In Progress', color: '#0284c7', bg: '#e0f2fe', border: '#bae6fd' },
  CODE_REVIEW: { label: 'Code Review', color: '#7c3aed', bg: '#f5f3ff', border: '#ddd6fe' },
  TESTING: { label: 'Testing', color: '#d97706', bg: '#fef3c7', border: '#fde68a' },
  DONE: { label: 'Done', color: '#16a34a', bg: '#dcfce7', border: '#bbf7d0' },
  BLOCKED: { label: 'Blocked', color: '#dc2626', bg: '#fee2e2', border: '#fecaca' },
  CANCELLED: { label: 'Cancelled', color: '#94a3b8', bg: '#f8fafc', border: '#e2e8f0' },
};

function formatStatus(status) {
  if (statusConfig[status]) return statusConfig[status].label;
  return status
    .toLowerCase()
    .split('_')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
}

export default function ProjectWorkflow({ id }) {
  const query = useWorkflowQuery(id);
  const { session, can } = useAuth();
  const [draft, setDraft] = useState(null);
  const [save, mutation] = useSaveWorkflowMutation();
  const [error, setError] = useState(null);
  const permitted = session.user.role === 'ADMIN' && can('PROJECT_MANAGE');

  async function submit(e) {
    e.preventDefault();
    try {
      const definition = JSON.parse(draft.text);
      await save({ id, definition, expectedVersion: draft.version }).unwrap();
      setDraft(null);
      setError(null);
    } catch (e) {
      setError(e instanceof SyntaxError ? { message: 'Enter valid workflow JSON.' } : e);
    }
  }

  return (
    <section className="panel collaboration-panel project-workflow-panel">
      <div className="section-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <div style={{ background: '#fef3c7', color: '#d97706', width: '36px', height: '36px', borderRadius: '8px', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
            </svg>
          </div>
          <div>
            <h2 style={{ margin: 0, fontSize: '1.1rem', fontWeight: 600 }}>Project workflow</h2>
            <p style={{ margin: 0, fontSize: '0.85rem', color: '#64748b' }}>State machine, lifecycle stages &amp; status transitions</p>
          </div>
        </div>
        {permitted && !draft && (
          <button
            type="button"
            className="button"
            onClick={() => {
              setError(null);
              mutation.reset();
              setDraft({
                text: JSON.stringify(query.data.definition, null, 2),
                version: query.data.version,
              });
            }}
          >
            Configure workflow
          </button>
        )}
      </div>

      <QueryState query={query}>
        {query.data?.definition && (
          <div className="workflow-pipeline-container">
            <div className="workflow-pipeline-flow">
              {query.data.definition.statuses.map((status, index) => {
                const isInitial = status === query.data.definition.initialStatus;
                const conf = statusConfig[status] || {
                  color: '#475569',
                  bg: '#f8fafc',
                  border: '#e2e8f0',
                };
                return (
                  <div key={status} className="workflow-step-wrapper">
                    <div
                      className="workflow-step-pill"
                      style={{
                        backgroundColor: conf.bg,
                        borderColor: conf.border,
                        color: conf.color,
                      }}
                    >
                      <span className="step-num">{index + 1}</span>
                      <span className="step-name">{formatStatus(status)}</span>
                      {isInitial && <span className="step-initial-tag">Entry</span>}
                    </div>
                    {index < query.data.definition.statuses.length - 1 && (
                      <div className="workflow-step-arrow" aria-hidden="true">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="2.5">
                          <polyline points="9 18 15 12 9 6" />
                        </svg>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>

            <div className="workflow-meta-hint">
              <span className="hint-pill">
                <strong>Initial State:</strong> {formatStatus(query.data.definition.initialStatus)}
              </span>
              <span className="hint-pill">
                <strong>Total States:</strong> {query.data.definition.statuses.length}
              </span>
              {query.data.definition.transitions && (
                <span className="hint-pill">
                  <strong>Transitions:</strong> {query.data.definition.transitions.length} defined
                </span>
              )}
            </div>
          </div>
        )}
      </QueryState>

      {draft && (
        <form onSubmit={submit} className="workflow-edit-form">
          <label>
            <strong>Workflow definition (JSON)</strong>
            <textarea
              name="workflowDefinition"
              rows="14"
              value={draft.text}
              disabled={mutation.isLoading}
              onChange={(e) => setDraft({ ...draft, text: e.target.value })}
              style={{ fontFamily: 'monospace', fontSize: '13px', marginTop: '6px' }}
            />
          </label>
          <p className="hint">
            Define initialStatus, statuses and transitions (from, to, optional permission). Keep
            statuses already used by issues.
          </p>
          <ErrorState error={error} />
          {error?.status === 412 && (
            <button
              type="button"
              onClick={async () => {
                const result = await query.refetch();
                if (result.data) {
                  setDraft({
                    text: JSON.stringify(result.data.definition, null, 2),
                    version: result.data.version,
                  });
                  setError(null);
                }
              }}
            >
              Discard draft and load latest workflow
            </button>
          )}
          <div style={{ display: 'flex', gap: '8px', marginTop: '12px' }}>
            <button className="primary" disabled={mutation.isLoading || error?.status === 412}>
              Save workflow
            </button>
            <button type="button" disabled={mutation.isLoading} onClick={() => setDraft(null)}>
              Cancel
            </button>
          </div>
        </form>
      )}
    </section>
  );
}
