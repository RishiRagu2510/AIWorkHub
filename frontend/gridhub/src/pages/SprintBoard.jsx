import { useState } from 'react';
import { useParams } from 'react-router';
import Link from '../../../src/components/PortalLink';
import { useAuth } from '../../../src/features/auth/context';
import { useNotice } from '../../../src/components/noticeContext';
import {
  useProjectQuery,
} from '../../../src/services/api/api';
import {
  useBoardsQuery,
  useBoardQuery,
  useCreateBoardMutation,
  useWorkflowQuery,
} from '../../../src/services/api/collaboration';
import {
  useSprintsQuery,
  useSprintActionMutation,
} from '../../../src/services/api/planning';
import { liveQueryOptions } from '../../../src/services/api/live';
import { useCollaborationLive } from '../../../src/features/collaboration/useLive';
import IssueBoard from '../../../src/features/issues/IssueBoard';
import SprintBacklog from '../../../src/features/planning/SprintBacklog';
import SprintAnalytics from '../../../src/features/planning/SprintAnalytics';
import { PageHeader, QueryState, ErrorState, Badge, Field } from '../../../src/components/common';
import { Icon } from '../../../src/components/Icon';

export default function SprintBoard() {
  const { projectId } = useParams();
  const { can, session } = useAuth();
  const notice = useNotice();
  const [activeTab, setActiveTab] = useState('board'); // 'board' | 'backlog'
  const [selectedBoardId, setSelectedBoardId] = useState('');
  const [selectedSprintId, setSelectedSprintId] = useState('');
  const [newBoardOpen, setNewBoardOpen] = useState(false);
  const [boardName, setBoardName] = useState('');
  const [boardType, setBoardType] = useState('SCRUM');

  const projectQuery = useProjectQuery(projectId);
  const boardsQuery = useBoardsQuery(projectId, liveQueryOptions);
  const [createBoard, createBoardState] = useCreateBoardMutation();

  const boardId = selectedBoardId || String(boardsQuery.data?.[0]?.id || '');
  const boardDetail = useBoardQuery(
    { id: boardId, page: 1 },
    { ...liveQueryOptions, skip: !boardId },
  );
  const workflow = useWorkflowQuery(projectId, { skip: !projectId });
  const live = useCollaborationLive('BOARD', boardId);

  const sprintsQuery = useSprintsQuery(boardId, {
    ...liveQueryOptions,
    skip: !boardId || boardDetail.data?.board?.boardType !== 'SCRUM',
  });
  const [sprintAction, sprintActionState] = useSprintActionMutation();

  const activeSprint = sprintsQuery.data?.find((s) => s.status === 'ACTIVE');
  const currentSprint = selectedSprintId
    ? sprintsQuery.data?.find((s) => String(s.id) === selectedSprintId)
    : activeSprint || sprintsQuery.data?.[0];

  const manage = session.user.role === 'ADMIN' && can('PROJECT_MANAGE');

  async function handleCreateBoard(e) {
    e.preventDefault();
    if (!boardName.trim() || createBoardState.isLoading) return;
    try {
      const res = await createBoard({
        projectId: Number(projectId),
        name: boardName.trim(),
        boardType,
      }).unwrap();
      setSelectedBoardId(String(res.id));
      setBoardName('');
      setNewBoardOpen(false);
      notice('Board created successfully.');
    } catch {
      /* Keep form */
    }
  }

  async function handleSprintAction(action) {
    if (!currentSprint) return;
    try {
      await sprintAction({
        id: currentSprint.id,
        version: currentSprint.version,
        action,
      }).unwrap();
      notice(action === 'start' ? 'Sprint started.' : 'Sprint completed.');
    } catch (err) {
      notice(err?.message || 'Failed to update sprint.');
    }
  }

  const issuesOnBoard = (boardDetail.data?.items || []).map((i) => ({
    ...i,
    projectId: Number(projectId),
  }));

  // Filter by sprint if in Scrum mode and sprint is selected
  const displayedIssues =
    currentSprint && boardDetail.data?.board?.boardType === 'SCRUM'
      ? issuesOnBoard.filter((i) => !i.sprintId || String(i.sprintId) === String(currentSprint.id))
      : issuesOnBoard;

  const totalSprintIssues = displayedIssues.length;
  const doneSprintIssues = displayedIssues.filter((i) => ['DONE', 'CANCELLED'].includes(i.statusCode)).length;
  const sprintProgress = totalSprintIssues > 0 ? Math.round((doneSprintIssues / totalSprintIssues) * 100) : 0;

  const boardStatuses = [
    ...new Set([
      ...(workflow.data?.definition.statuses || []),
      ...(boardDetail.data?.columns || []).map((c) => c.statusCode),
    ]),
  ];

  return (
    <div className="sprint-board-page">
      <QueryState query={projectQuery}>
        {projectQuery.data && (
          <PageHeader
            eyebrow={`Project · ${projectQuery.data.projectName || projectQuery.data.key}`}
            title="Sprint & Kanban Board"
            description="Manage sprints, track issue statuses, and plan team velocity."
          >
            <Link className="button" to={`/projects/${projectId}`}>
              ← Project overview
            </Link>
            {manage && (
              <button onClick={() => setNewBoardOpen(!newBoardOpen)}>
                <Icon name="plus" />
                {newBoardOpen ? 'Cancel' : 'New board'}
              </button>
            )}
          </PageHeader>
        )}
      </QueryState>

      {newBoardOpen && (
        <section className="panel board-create-panel">
          <h3>Create new board</h3>
          <form onSubmit={handleCreateBoard} className="form-inline">
            <Field
              label="Board name"
              value={boardName}
              onChange={(e) => setBoardName(e.target.value)}
              placeholder="e.g. Sprint 24 Board"
              required
            />
            <label className="field">
              <span>Board type</span>
              <select value={boardType} onChange={(e) => setBoardType(e.target.value)}>
                <option value="SCRUM">Scrum (Sprints & Backlog)</option>
                <option value="KANBAN">Kanban (Continuous flow)</option>
              </select>
            </label>
            <div className="actions" style={{ marginTop: '22px' }}>
              <button className="primary" disabled={createBoardState.isLoading || !boardName.trim()}>
                {createBoardState.isLoading ? 'Creating…' : 'Create board'}
              </button>
              <button type="button" onClick={() => setNewBoardOpen(false)}>
                Cancel
              </button>
            </div>
          </form>
          <ErrorState error={createBoardState.error} />
        </section>
      )}

      {/* Boards toolbar */}
      <div className="panel board-toolbar">
        <div className="board-toolbar-main">
          <label className="board-selector-label">
            <strong>Board:</strong>
            <select
              value={boardId}
              onChange={(e) => {
                setSelectedBoardId(e.target.value);
                setSelectedSprintId('');
              }}
            >
              {boardsQuery.data?.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name} ({b.boardType})
                </option>
              ))}
            </select>
          </label>

          <div className="board-nav-tabs">
            <button
              className={activeTab === 'board' ? 'is-active' : ''}
              onClick={() => setActiveTab('board')}
            >
              <Icon name="dashboard" />
              Active Board
            </button>
            {boardDetail.data?.board?.boardType === 'SCRUM' && (
              <>
                <button
                  className={activeTab === 'backlog' ? 'is-active' : ''}
                  onClick={() => setActiveTab('backlog')}
                >
                  <Icon name="folder" />
                  Backlog & Sprints
                </button>
                <button
                  className={activeTab === 'analytics' ? 'is-active' : ''}
                  onClick={() => setActiveTab('analytics')}
                >
                  <Icon name="chart" />
                  Sprint Analytics
                </button>
              </>
            )}
          </div>
        </div>

        <div className="board-toolbar-meta">
          <span
            className={`organization-activity-status${live.state === 'Connected' ? ' is-connected' : ''}`}
            role="status"
          >
            <span aria-hidden="true" />
            {live.state === 'Connected' ? 'Live updates' : live.state}
          </span>
        </div>
      </div>

      {activeTab === 'backlog' && boardId && (
        <SprintBacklog boardId={boardId} />
      )}

      {activeTab === 'analytics' && boardId && (
        <SprintAnalytics
          boardId={boardId}
          sprints={sprintsQuery.data || []}
          currentSprint={currentSprint}
          issues={issuesOnBoard}
          onSelectSprint={(id) => setSelectedSprintId(id)}
        />
      )}

      {activeTab === 'board' && (
        <>
          {/* Active Sprint Summary Header if Scrum */}
          {boardDetail.data?.board?.boardType === 'SCRUM' && currentSprint && (
            <div className="panel sprint-summary-banner">
              <div className="sprint-summary-info">
                <div className="sprint-summary-title">
                  <h2>{currentSprint.name}</h2>
                  <Badge value={currentSprint.status} />
                  {currentSprint.startDate && currentSprint.endDate && (
                    <span className="sprint-dates">
                      {currentSprint.startDate} → {currentSprint.endDate}
                    </span>
                  )}
                </div>
                {currentSprint.goal && <p className="sprint-goal">{currentSprint.goal}</p>}
                <div className="sprint-progress-bar-wrap">
                  <div className="sprint-progress-track">
                    <div
                      className="sprint-progress-fill"
                      style={{ width: `${sprintProgress}%` }}
                    />
                  </div>
                  <span className="sprint-progress-text">
                    {doneSprintIssues}/{totalSprintIssues} issues completed ({sprintProgress}%)
                  </span>
                </div>
              </div>

              <div className="sprint-summary-actions">
                {sprintsQuery.data?.length > 1 && (
                  <select
                    value={String(currentSprint.id)}
                    onChange={(e) => setSelectedSprintId(e.target.value)}
                  >
                    {sprintsQuery.data.map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.name} ({s.status})
                      </option>
                    ))}
                  </select>
                )}

                {manage && currentSprint.status === 'PLANNED' && (
                  <button
                    className="primary"
                    disabled={sprintActionState.isLoading}
                    onClick={() => handleSprintAction('start')}
                  >
                    Start sprint
                  </button>
                )}

                {manage && currentSprint.status === 'ACTIVE' && (
                  <button
                    className="button"
                    disabled={sprintActionState.isLoading}
                    onClick={() => handleSprintAction('complete')}
                  >
                    Complete sprint
                  </button>
                )}
              </div>
            </div>
          )}

          <QueryState query={boardDetail}>
            <QueryState query={workflow}>
              <IssueBoard
                issues={displayedIssues}
                busy={boardDetail.isFetching}
                workflows={{ [projectId]: workflow.data }}
                boardStatuses={boardStatuses}
              />
            </QueryState>
          </QueryState>
        </>
      )}
    </div>
  );
}
