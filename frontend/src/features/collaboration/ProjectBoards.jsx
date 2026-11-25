import SprintBacklog from '../planning/SprintBacklog';
import { useRef, useState } from 'react';
import { Icon } from '../../components/Icon';
import { useNotice } from '../../components/noticeContext';
import { useAuth } from '../auth/context';
import {
  useBoardsQuery,
  useBoardQuery,
  useCreateBoardMutation,
  useWorkflowQuery,
} from '../../services/api/collaboration';
import { QueryState, ErrorState, Pagination } from '../../components/common';
import { liveQueryOptions } from '../../services/api/live';
import { useCollaborationLive } from './useLive';
import IssueBoard from '../issues/IssueBoard';
function SavedBoard({ id, projectId }) {
  const [page, setPage] = useState(1);
  const query = useBoardQuery({ id, page }, liveQueryOptions);
  const workflow = useWorkflowQuery(projectId);
  const live = useCollaborationLive('BOARD', id);
  return (
    <section className="saved-board-content">
      <div className="saved-board-meta">
        <span>Workflow board</span>
        <span
          className={`organization-activity-status${live.state === 'Connected' ? ' is-connected' : ''}`}
          role="status"
          aria-label={`Board updates: ${live.state}`}
        >
          <span aria-hidden="true" />
          {live.state === 'Connected' ? 'Live updates' : live.state}
        </span>
      </div>
      {query.data?.board?.boardType === 'SCRUM' && <SprintBacklog boardId={id} />}
      <QueryState query={query}>
        <QueryState query={workflow}>
          <IssueBoard
            issues={(query.data?.items || []).map((i) => ({ ...i, projectId: Number(projectId) }))}
            busy={query.isFetching}
            workflows={{ [projectId]: workflow.data }}
            boardStatuses={[
              ...new Set([
                ...(workflow.data?.definition.statuses || []),
                ...(query.data?.columns || []).map((c) => c.statusCode),
              ]),
            ]}
          />
        </QueryState>
        <Pagination data={query.data} busy={query.isFetching} onPage={setPage} />
      </QueryState>
    </section>
  );
}
export default function ProjectBoards({ id }) {
  const query = useBoardsQuery(id, liveQueryOptions);
  const { session, can } = useAuth();
  const [selected, setSelected] = useState('');
  const createPanel = useRef(null);
  const notice = useNotice();
  const [create, mutation] = useCreateBoardMutation();
  const [name, setName] = useState('');
  const [type, setType] = useState('KANBAN');
  const boardId = selected || String(query.data?.[0]?.id || '');
  async function submit(e) {
    e.preventDefault();
    if (!name.trim() || mutation.isLoading) return;
    try {
      const result = await create({
        projectId: Number(id),
        name: name.trim(),
        boardType: type,
      }).unwrap();
      setSelected(String(result.id));
      setName('');
      if (createPanel.current) {
        createPanel.current.open = false;
        createPanel.current.querySelector('summary')?.focus();
      }
      notice('Board created.');
    } catch {
      /* Keep draft. */
    }
  }
  return (
    <section className="panel collaboration-panel project-boards-panel">
      <header className="project-boards-header">
        <span className="project-boards-icon" aria-hidden="true">
          <Icon name="dashboard" />
        </span>
        <div>
          <h2>Project boards</h2>
          <p>Organize your team’s work and track progress.</p>
        </div>
        {query.data && (
          <span className="project-boards-count">
            {query.data.length} {query.data.length === 1 ? 'board' : 'boards'}
          </span>
        )}
      </header>
      <QueryState query={query}>
        {query.data?.length ? (
          <label className="saved-board-selector">
            Active board
            <select name="boardId" value={boardId} onChange={(e) => setSelected(e.target.value)}>
              {query.data.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          </label>
        ) : (
          <div className="boards-empty">
            <strong>No boards yet</strong>
            <p>A shared board brings this project’s issues and workflow together.</p>
          </div>
        )}
      </QueryState>
      {session.user.role === 'ADMIN' && can('PROJECT_MANAGE') && (
        <details className="board-create" ref={createPanel}>
          <summary>
            <Icon name="plus" /> New board
          </summary>
          <form onSubmit={submit}>
            <label>
              New board name
              <input
                name="boardName"
                value={name}
                maxLength={200}
                required
                placeholder="e.g. Delivery board"
                onChange={(e) => setName(e.target.value)}
              />
            </label>
            <label className="saved-board-selector">
              Active board type
              <select name="boardType" value={type} onChange={(e) => setType(e.target.value)}>
                <option value="KANBAN">Kanban</option>
                <option value="SCRUM">Scrum</option>
              </select>
            </label>
            <button className="primary" disabled={mutation.isLoading || !name.trim()}>
              {mutation.isLoading ? 'Creating…' : 'Create board'}
            </button>
            <ErrorState error={mutation.error} />
          </form>
        </details>
      )}
      {boardId && can('ISSUE_VIEW') && <SavedBoard key={boardId} id={boardId} projectId={id} />}
    </section>
  );
}
