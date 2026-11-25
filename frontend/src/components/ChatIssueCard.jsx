import React, { useEffect, useMemo, useRef, useState } from 'react';
import { api, useAssignIssueMutation, useProjectAssigneesQuery, useTransitionIssueMutation } from '../services/api/api';
import { allowedStatuses, collaborationApi, normalNext } from '../services/api/collaboration';
import { assignmentError } from '../features/issues/assignment';
import { useAuth } from '../features/auth/context';

const pretty = (status) =>
  String(status || '')
    .toLowerCase()
    .split('_')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');

const slug = (status) => String(status || '').toLowerCase().replace('_', '-');

const messageOf = (error, fallback) => error?.data?.message || error?.message || fallback;

const initials = (name) =>
  String(name || '?')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0].toUpperCase())
    .join('');

const SEARCH_FROM = 6;

/**
 * A ticket in the chat. Besides Inspect and Done it can assign the ticket to an eligible person from a custom
 * dropdown, using the same endpoints, permissions and rules as the board: ISSUE_ASSIGN and ISSUE_TRANSITION, a
 * version check on every write, and a confirmation before Done skips workflow steps.
 */
export default function ChatIssueCard({ issue, status, displayKey, busy, onInspect, onMoved }) {
  const { session, can } = useAuth();
  const [fetchIssue] = api.useLazyIssueQuery();
  const [fetchWorkflow] = collaborationApi.useLazyWorkflowQuery();
  const [transition, transitionState] = useTransitionIssueMutation();
  const [assign, assignState] = useAssignIssueMutation();

  const [details, setDetails] = useState(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [pendingSkip, setPendingSkip] = useState(null);
  const [note, setNote] = useState(null);
  const [assigned, setAssigned] = useState(null);
  const menuRef = useRef(null);

  const canMove = can('ISSUE_TRANSITION');
  const canAssign = can('ISSUE_ASSIGN');
  const projectId = issue.projectId ?? details?.projectId;
  const working = transitionState.isLoading || assignState.isLoading;

  const directory = useProjectAssigneesQuery(
    {
      projectIds: [String(projectId)],
      selfId: session?.user?.id,
      includeProfiles: session?.user?.role === 'ADMIN' && can('USER_VIEW'),
    },
    { skip: !menuOpen || !projectId },
  );
  // Whoever already has the ticket is not offered again. The live ticket is the most reliable source, and a chat
  // answer may not list assignees at all, so use whichever knows.
  const currentIds = useMemo(() => {
    const ids = new Set();
    if (assigned) ids.add(String(assigned.id));
    else {
      // The live ticket wins when it reports assignees at all; otherwise fall back to what the answer listed.
      const liveKnown = details && ('assigneeUserId' in details || 'assigneeUserIds' in details);
      const source = liveKnown
        ? [details.assigneeUserId, ...(details.assigneeUserIds || [])]
        : (issue.assignees || []).map((a) => a?.userId);
      source.forEach((id) => {
        if (id != null) ids.add(String(id));
      });
    }
    return ids;
  }, [assigned, details, issue.assignees]);
  const people = (directory.currentData || []).filter((p) => !currentIds.has(String(p.id)));

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase();
    return term ? people.filter((p) => String(p.displayName || '').toLowerCase().includes(term)) : people;
  }, [people, search]);

  // Close the menu on an outside click or Escape.
  useEffect(() => {
    if (!menuOpen) return undefined;
    const onDown = (e) => {
      if (menuRef.current && !menuRef.current.contains(e.target)) setMenuOpen(false);
    };
    const onKey = (e) => {
      if (e.key === 'Escape') setMenuOpen(false);
    };
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [menuOpen]);

  // The ticket from a chat answer may be missing the project or version, so read the live record before changing it.
  async function ensureDetails() {
    if (details) return details;
    try {
      const fresh = await fetchIssue(issue.id).unwrap();
      setDetails(fresh);
      return fresh;
    } catch {
      return null;
    }
  }

  async function toggleMenu() {
    setNote(null);
    setPendingSkip(null);
    if (menuOpen) {
      setMenuOpen(false);
      return;
    }
    setSearch('');
    setMenuOpen(true);
    await ensureDetails();
  }

  // Runs a write with the current version and retries once with the latest one if the ticket changed meanwhile.
  async function withVersion(run) {
    let fresh = details ?? (await ensureDetails());
    const version = fresh?.version ?? issue.version;
    if (version == null) throw new Error('This ticket could not be read, so it was not changed.');
    try {
      return await run(version);
    } catch (error) {
      if (error?.status !== 412) throw error;
      fresh = await fetchIssue(issue.id).unwrap();
      setDetails(fresh);
      return run(fresh.version);
    }
  }

  async function move(target, confirmSkip = false) {
    setNote(null);
    try {
      await withVersion((version) =>
        transition({ id: String(issue.id), statusCode: target, version, ...(confirmSkip ? { confirmSkip: true } : {}) }).unwrap(),
      );
      setPendingSkip(null);
      setDetails(null);
      setNote({ ok: true, text: `Moved to ${pretty(target)}.` });
      onMoved?.(issue, target);
    } catch (error) {
      setNote({ ok: false, text: messageOf(error, 'The status was not changed.') });
    }
  }

  // Done is one click, but the project's workflow decides whether it is allowed at all and whether it skips steps.
  async function requestDone() {
    setMenuOpen(false);
    setPendingSkip(null);
    setNote(null);
    const fresh = await ensureDetails();
    const project = projectId ?? fresh?.projectId;
    let definition = null;
    if (project) {
      try {
        definition = await fetchWorkflow(project, true).unwrap();
      } catch {
        definition = null;
      }
    }
    if (definition && !allowedStatuses(definition, status, can).includes('DONE')) {
      setNote({
        ok: false,
        text: `This project's workflow does not allow ${pretty(status)} to Done directly. Move it through the steps the workflow allows first.`,
      });
      return;
    }
    const skipsSteps = definition ? definition.isDefault && normalNext(definition, status) !== 'DONE' : status !== 'TESTING';
    if (skipsSteps) setPendingSkip('DONE');
    else await move('DONE');
  }

  async function choose(person) {
    setNote(null);
    try {
      await withVersion((version) => assign({ id: String(issue.id), assigneeUserId: Number(person.id), version }).unwrap());
      setMenuOpen(false);
      setDetails(null);
      setAssigned({ id: person.id, name: person.displayName || `User #${person.id}` });
      setNote({ ok: true, text: `Assigned to ${person.displayName || `user #${person.id}`}.` });
    } catch (error) {
      setNote({ ok: false, text: messageOf(assignmentError(error), 'The ticket was not assigned.') });
    }
  }

  const names = assigned ? [assigned.name] : (issue.assignees || []).map((a) => a?.name).filter(Boolean);
  const isDone = status === 'DONE' || status === 'RESOLVED' || status === 'CLOSED';
  const loading = !projectId || directory.isFetching;

  return (
    <div className={`embedded-issue-card status-${slug(status)}`} ref={menuRef}>
      <div className="card-top">
        <span className="card-key">{displayKey}</span>
        <span className={`card-status-pill ${slug(status)}`}>{status.replace('_', ' ')}</span>
      </div>
      <div className="card-summary" title={issue.summary}>
        {issue.summary}
      </div>
      {names.length > 0 && <div className="card-assignee">Assigned to {names.join(', ')}</div>}
      <div className="card-bottom">
        <span className="card-priority">
          <span className={`priority-dot ${String(issue.priority || 'medium').toLowerCase()}`}></span>
          {issue.priority || 'Normal'}
        </span>
        <div className="card-quick-actions">
          <button type="button" className="card-quick-btn inspect" onClick={onInspect} title="Inspect issue">
            Inspect ✦
          </button>
          {canAssign && (
            <button
              type="button"
              className={`card-quick-btn ${menuOpen ? 'active' : ''}`}
              aria-haspopup="listbox"
              aria-expanded={menuOpen}
              onClick={toggleMenu}
              disabled={working || busy}
              title="Assign to a developer"
            >
              Assign ▾
            </button>
          )}
          {!isDone && canMove && (
            <button type="button" className="card-quick-btn done" disabled={working || busy} onClick={requestDone} title="Move to DONE">
              ✓ Done
            </button>
          )}
        </div>
      </div>

      {menuOpen && (
        <div className="card-assign-menu" role="listbox" aria-label="Assign to">
          <div className="card-assign-title">Assign to</div>
          {people.length >= SEARCH_FROM && (
            <input
              type="search"
              className="card-assign-search"
              placeholder="Search people"
              aria-label="Search people"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              autoFocus
            />
          )}
          <div className="card-assign-list">
            {loading && <div className="card-assign-empty">Loading people…</div>}
            {!loading && directory.error && (
              <div className="card-assign-empty error">{messageOf(assignmentError(directory.error), 'The developer list could not be loaded.')}</div>
            )}
            {!loading && !directory.error && visible.length === 0 && (
              <div className="card-assign-empty">{people.length ? 'No one matches that search.' : 'No one else is eligible for this project.'}</div>
            )}
            {!loading &&
              visible.map((person) => {
                const mine = String(person.id) === String(session?.user?.id);
                return (
                  <button
                    key={person.id}
                    type="button"
                    role="option"
                    aria-selected="false"
                    className="card-assign-option"
                    disabled={working}
                    onClick={() => choose(person)}
                  >
                    <span className="card-assign-avatar" aria-hidden="true">{initials(person.displayName)}</span>
                    <span className="card-assign-name">{person.displayName}</span>
                    {mine && <span className="card-assign-me">me</span>}
                  </button>
                );
              })}
          </div>
        </div>
      )}

      {pendingSkip && (
        <div className="card-manage-confirm" role="alert">
          <span>Moving to {pretty(pendingSkip)} skips workflow steps. Continue?</span>
          <button type="button" className="card-quick-btn done" disabled={working} onClick={() => move(pendingSkip, true)}>Confirm</button>
          <button type="button" className="card-quick-btn" disabled={working} onClick={() => setPendingSkip(null)}>Cancel</button>
        </div>
      )}

      {note && <div className={`card-manage-note ${note.ok ? 'ok' : 'error'}`} role="status">{note.text}</div>}
    </div>
  );
}
