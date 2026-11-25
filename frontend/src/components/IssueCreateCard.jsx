import React, { useMemo, useState } from 'react';
import { useCreateIssueMutation, useProjectAssigneesQuery } from '../services/api/api';
import { useAuth } from '../features/auth/context';
import PortalLink from './PortalLink';
import { shortIssueKey } from '../features/notifications/notificationPresentation';
import CustomDatePicker from './CustomDatePicker';

const TYPES = ['TASK', 'BUG', 'STORY', 'EPIC'];
const PRIORITIES = ['LOWEST', 'LOW', 'MEDIUM', 'HIGH', 'HIGHEST'];
const POINTS = [1, 2, 3, 5, 8, 13];
const pretty = (value) => value.charAt(0) + value.slice(1).toLowerCase();

const PALETTES = ['hue-blue', 'hue-indigo', 'hue-purple', 'hue-emerald', 'hue-amber', 'hue-cyan'];
const USER_PALETTES = ['hue-blue', 'hue-indigo', 'hue-purple', 'hue-emerald', 'hue-amber', 'hue-rose', 'hue-cyan', 'hue-teal'];

function getUserColorClass(user) {
  if (!user) return 'hue-blue';
  const str = String(user.displayName || user.id || 'U');
  let hash = 0;
  for (let i = 0; i < str.length; i++) {
    hash = (hash << 5) - hash + str.charCodeAt(i);
    hash |= 0;
  }
  return USER_PALETTES[Math.abs(hash) % USER_PALETTES.length];
}

function formatProjectDisplay(p) {
  if (!p) return { shortKey: '', initials: '', title: '', subtitle: '', colorClass: 'hue-blue' };
  const key = String(p.key || '').trim();
  const name = String(p.name || '').trim();

  // Extract a clean short key, e.g. "FLP360" from "FLP360 ONGOING FEATURES..."
  let shortKey = key;
  const match = key.match(/^([A-Z0-9]+)\b/i);
  if (match && match[1].length >= 2 && match[1].length <= 8) {
    shortKey = match[1].toUpperCase();
  } else if (key.length > 8) {
    const acronym = key.split(/\s+/).map((w) => w[0]).join('').slice(0, 5).toUpperCase();
    shortKey = acronym.length >= 2 ? acronym : key.slice(0, 6).toUpperCase();
  }

  // Meaningful 2-char initials (e.g. Google04 -> G4, Rapid Azure -> RA, Rapid PayCard -> RP, IT Helpdesk -> IT, VISA -> VA)
  const cleanWords = (name || key).replace(/[^a-zA-Z0-9\s]/g, ' ').trim().split(/\s+/).filter(Boolean);
  let initials = '';
  if (cleanWords.length >= 2) {
    initials = (cleanWords[0][0] + cleanWords[1][0]).toUpperCase();
  } else if (cleanWords.length === 1) {
    const w = cleanWords[0];
    const numMatch = w.match(/^([A-Za-z]+)(\d+)$/);
    if (numMatch) {
      initials = (numMatch[1][0] + numMatch[2]).toUpperCase();
    } else {
      initials = w.slice(0, 2).toUpperCase();
    }
  } else {
    initials = key.slice(0, 2).toUpperCase();
  }

  let title = name || key;
  let subtitle = '';
  if (name.toLowerCase() !== key.toLowerCase() && !name.toUpperCase().startsWith(shortKey)) {
    subtitle = key;
  }

  const hash = Math.abs((p.id || 0) + (p.key || '').charCodeAt(0) + (p.name || '').charCodeAt(0));
  const colorClass = PALETTES[hash % PALETTES.length];

  return { shortKey, initials, title, subtitle, colorClass };
}

const messageOf = (error) => error?.message || error?.data?.message || 'The issue was not created.';

/**
 * A New issue form in the chat. Step 1 is the project, step 2 is who to assign it to (only for people who may assign),
 * then the details. Nothing is chosen for the user, and nothing is created until they press Create. It saves through
 * the same issue endpoint, and so the same permission and project rules, as the Issues page.
 */
export default function IssueCreateCard({ form, disabled, onCreated }) {
  const { session, can } = useAuth();
  const [createIssue, { isLoading }] = useCreateIssueMutation();
  const canAssign = can('ISSUE_ASSIGN');

  const [projectId, setProjectId] = useState(form.projects.length === 1 ? form.projects[0].id : null);
  // undefined: not chosen yet. null: deliberately left unassigned. A number: that person.
  const [assigneeId, setAssigneeId] = useState(undefined);
  const [search, setSearch] = useState('');
  const [summary, setSummary] = useState(form.summary || '');
  const [description, setDescription] = useState(form.description || '');
  const [issueType, setIssueType] = useState(form.issueType || 'TASK');
  const [priority, setPriority] = useState(form.priority || 'MEDIUM');
  const [points, setPoints] = useState(null);
  const [dueDate, setDueDate] = useState('');
  const [error, setError] = useState(null);
  const [created, setCreated] = useState(null);

  const project = useMemo(() => form.projects.find((p) => p.id === projectId) || null, [form.projects, projectId]);

  const directory = useProjectAssigneesQuery(
    {
      projectIds: [String(projectId)],
      selfId: session?.user?.id,
      includeProfiles: session?.user?.role === 'ADMIN' && can('USER_VIEW'),
    },
    { skip: !projectId || !canAssign },
  );
  const people = directory.currentData || [];
  const visiblePeople = useMemo(() => {
    const term = search.trim().toLowerCase();
    return term ? people.filter((p) => String(p.displayName || '').toLowerCase().includes(term)) : people;
  }, [people, search]);
  const assignee = people.find((p) => p.id === assigneeId) || null;

  const assigneeSettled = !canAssign || assigneeId !== undefined;
  const ready = Boolean(project && summary.trim() && assigneeSettled && !isLoading && !disabled);

  function pickProject(id) {
    setProjectId(id);
    // Another project has another team, so an earlier choice no longer applies.
    setAssigneeId(undefined);
    setSearch('');
  }

  async function submit(e) {
    e.preventDefault();
    if (!ready) return;
    setError(null);
    const body = {
      projectId: Number(projectId),
      summary: summary.trim(),
      description: description.trim(),
      issueType,
      priority,
      statusCode: 'TODO',
    };
    if (points != null) body.storyPoints = points;
    if (dueDate) body.dueDate = dueDate;
    if (form.labels?.length) body.labels = form.labels;
    if (assigneeId != null) {
      body.assigneeUserId = assigneeId;
      body.assigneeUserIds = [assigneeId];
    }
    try {
      const result = await createIssue(body).unwrap();
      const disp = formatProjectDisplay(project);
      setCreated({
        id: result?.id ?? null,
        key: shortIssueKey(result?.issueKey || result?.key || ''),
        summary: body.summary,
        project: disp.shortKey || disp.title,
        priority,
        assignee: assignee?.displayName || null,
      });
      onCreated?.();
    } catch (err) {
      setError(messageOf(err));
    }
  }

  if (created) {
    return (
      <div className="worklog-card done issue-created-card" role="status">
        <span className="worklog-done-check" aria-hidden="true">✓</span>
        <div className="issue-created-body">
          <div className="issue-created-heading">
            <span className="issue-created-title">Issue created</span>
            {created.key && <span className="issue-created-key">{created.key}</span>}
          </div>
          <div className="issue-created-summary">{created.summary}</div>
          <div className="issue-created-meta">
            <span className="issue-created-project-badge">{created.project}</span>
            <span className="meta-sep" aria-hidden="true">·</span>
            <span className="issue-created-assignee-badge">
              {created.assignee ? <>Assigned to <strong>{created.assignee}</strong></> : 'Unassigned'}
            </span>
            <span className="meta-sep" aria-hidden="true">·</span>
            <span className="issue-created-priority-badge">{pretty(created.priority)} priority</span>
          </div>
        </div>
        {created.id != null && (
          <PortalLink className="issue-created-open" to={`/issues/${created.id}`}>
            <span>Open issue</span>
            <span className="open-arrow" aria-hidden="true">→</span>
          </PortalLink>
        )}
      </div>
    );
  }

  return (
    <form className="worklog-card issue-create-card" onSubmit={submit} aria-label="New issue">
      <div className="issue-create-card-header">
        <div className="issue-create-title-group">
          <span className="issue-create-title-icon">✨</span>
          <div className="worklog-title">New issue</div>
        </div>
        <span className="issue-create-step-badge">
          {!project ? 'Step 1 of 3' : !assigneeSettled ? 'Step 2 of 3' : 'Step 3 of 3'}
        </span>
      </div>

      <div className="worklog-label" id="issue-project-label">1. Project</div>
      {form.projects.length === 0 ? (
        <div className="worklog-empty">{form.note || 'You are not a member of any project yet.'}</div>
      ) : (
        <div className="worklog-issues" role="listbox" aria-labelledby="issue-project-label">
          {form.projects.map((p) => {
            const display = formatProjectDisplay(p);
            return (
              <button
                key={p.id}
                type="button"
                role="option"
                aria-selected={p.id === projectId}
                aria-label={`${p.key} ${p.name}`}
                className={`worklog-issue project-picker-item ${p.id === projectId ? 'selected' : ''} ${p.id === form.suggestedProjectId && p.id !== projectId ? 'suggested' : ''}`}
                onClick={() => pickProject(p.id)}
              >
                <span className={`project-avatar-badge ${display.colorClass}`}>
                  {display.initials || display.shortKey.slice(0, 3)}
                </span>
                <div className="project-picker-text">
                  <span className="project-picker-name">{display.title}</span>
                  {display.subtitle && <span className="project-picker-sub">{display.subtitle}</span>}
                </div>
                {p.id === form.suggestedProjectId && p.id !== projectId && (
                  <span className="project-picker-suggest">You mentioned this · click to confirm</span>
                )}
                {display.shortKey && <span className="project-picker-code">{display.shortKey}</span>}
                {p.id === projectId ? (
                  <span className="worklog-issue-check project-picker-check" aria-hidden="true">✓</span>
                ) : (
                  <span className="project-picker-circle" aria-hidden="true" />
                )}
              </button>
            );
          })}
        </div>
      )}

      {project && canAssign && (
        <>
          <div className="worklog-step-header">
            <div className="worklog-label" id="issue-assignee-label">2. Assign to</div>
            {visiblePeople.length > 0 && (
              <span className="step-counter-badge">{visiblePeople.length} {visiblePeople.length === 1 ? 'member' : 'members'}</span>
            )}
          </div>
          {directory.isLoading || directory.isFetching ? (
            <div className="worklog-empty">Loading the team…</div>
          ) : directory.isError ? (
            <div className="worklog-hint error" role="alert">The team could not be loaded. You can still leave it unassigned.</div>
          ) : null}
          {people.length >= 6 && (
            <div className="assignee-search-wrap">
              <span className="assignee-search-icon" aria-hidden="true">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="11" cy="11" r="8"></circle>
                  <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
                </svg>
              </span>
              <input
                className="worklog-custom wide assignee-search-input"
                type="search"
                placeholder="Search people"
                aria-label="Search people"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
              {search && (
                <button
                  type="button"
                  className="assignee-search-clear"
                  onClick={() => setSearch('')}
                  aria-label="Clear search"
                >
                  ×
                </button>
              )}
            </div>
          )}
          <div className="worklog-issues" role="listbox" aria-labelledby="issue-assignee-label">
            {visiblePeople.map((p) => {
              const initials = (p.displayName || 'U').split(/\s+/).map((n) => n[0]).join('').slice(0, 2).toUpperCase();
              const isYou = String(p.id) === String(session?.user?.id);
              const colorClass = getUserColorClass(p);
              const isSelected = p.id === assigneeId;
              return (
                <button
                  key={p.id}
                  type="button"
                  role="option"
                  aria-selected={isSelected}
                  aria-label={`${p.displayName} ${isYou ? '(you)' : ''}`.trim()}
                  className={`worklog-issue assignee-picker-item ${isSelected ? 'selected' : ''}`}
                  onClick={() => setAssigneeId(p.id)}
                >
                  <span className={`user-initials-badge ${colorClass}`}>{initials}</span>
                  <span className="worklog-issue-summary assignee-picker-name">
                    {p.displayName}
                    {isYou && <span className="assignee-you-tag">you</span>}
                  </span>
                  {isSelected ? (
                    <span className="worklog-issue-check assignee-picker-check" aria-hidden="true">✓</span>
                  ) : (
                    <span className="assignee-picker-circle" aria-hidden="true" />
                  )}
                </button>
              );
            })}
            <button
              type="button"
              role="option"
              aria-selected={assigneeId === null}
              aria-label="Leave unassigned"
              className={`worklog-issue assignee-picker-item unassigned-option ${assigneeId === null ? 'selected' : ''}`}
              onClick={() => setAssigneeId(null)}
            >
              <span className="user-initials-badge unassigned-badge" aria-hidden="true">
                <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="10"></circle>
                  <line x1="4.93" y1="4.93" x2="19.07" y2="19.07"></line>
                </svg>
              </span>
              <span className="worklog-issue-summary assignee-picker-name">Leave unassigned</span>
              {assigneeId === null ? (
                <span className="worklog-issue-check assignee-picker-check" aria-hidden="true">✓</span>
              ) : (
                <span className="assignee-picker-circle" aria-hidden="true" />
              )}
            </button>
          </div>
        </>
      )}

      {project && assigneeSettled && (
        <>
          <label className="worklog-field">
            <span className="worklog-label">{canAssign ? '3. Title' : '2. Title'}</span>
            <input
              className="worklog-title-input"
              type="text"
              maxLength={255}
              value={summary}
              onChange={(e) => setSummary(e.target.value)}
              placeholder="What needs to be done?"
              aria-label="Title"
            />
          </label>

          <label className="worklog-field">
            <span className="worklog-label">Description (optional)</span>
            <textarea
              className="worklog-title-input worklog-textarea"
              rows={form.description && form.description.length > 200 ? 8 : 3}
              maxLength={10000}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Add context, steps or acceptance criteria"
              aria-label="Description"
            />
          </label>

          <div className="worklog-label">Type</div>
          <div className="worklog-chips">
            {TYPES.map((t) => (
              <button key={t} type="button" className={`worklog-chip ${issueType === t ? 'selected' : ''}`} aria-pressed={issueType === t} onClick={() => setIssueType(t)}>
                {pretty(t)}
              </button>
            ))}
          </div>

          <div className="worklog-label">Priority</div>
          <div className="worklog-chips">
            {PRIORITIES.map((p) => (
              <button key={p} type="button" className={`worklog-chip ${priority === p ? 'selected' : ''}`} aria-pressed={priority === p} onClick={() => setPriority(p)}>
                {pretty(p)}
              </button>
            ))}
          </div>

          <div className="worklog-label">Story points (optional)</div>
          <div className="worklog-chips">
            {POINTS.map((n) => (
              <button
                key={n}
                type="button"
                className={`worklog-chip ${points === n ? 'selected' : ''}`}
                aria-pressed={points === n}
                aria-label={`${n} points`}
                onClick={() => setPoints(points === n ? null : n)}
              >
                {n}
              </button>
            ))}
          </div>

          <label className="worklog-field">
            <span className="worklog-label">Due date (optional)</span>
            <CustomDatePicker
              value={dueDate}
              min={form.today || undefined}
              onChange={(e) => setDueDate(e.target.value)}
              aria-label="Due date"
              placeholder="Pick due date (optional)"
            />
          </label>
        </>
      )}

      {error && <div className="worklog-hint error" role="alert">{error}</div>}

      <div className="issue-create-footer-bar">
        {!ready && (
          <span className="issue-create-hint">
            <span className="hint-bullet-dot">•</span>
            {!project
              ? 'Select a project above to continue'
              : !assigneeSettled
              ? 'Choose an assignee or leave unassigned'
              : !summary.trim()
              ? 'Enter an issue title'
              : ''}
          </span>
        )}
        <div className="worklog-actions">
          <button type="submit" className="worklog-submit" disabled={!ready}>
            {isLoading ? 'Creating…' : project ? `Create in ${formatProjectDisplay(project).shortKey || project.key}` : 'Create issue'}
          </button>
        </div>
      </div>
    </form>
  );
}
