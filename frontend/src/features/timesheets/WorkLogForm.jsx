import { useState, useMemo, useRef } from 'react';
import { useAuth } from '../auth/context';
import { FeatureGuard } from '../auth/FeatureGuard';
import { ATHENNA } from '../auth/features';
import { useSearchIssuesQuery } from '../../services/api/api';
import { useCreateWorkLogMutation, useUpdateWorkLogMutation } from '../../services/api/timesheets';
import { useRunAthennaCopilotMutation } from '../../services/api/agent';
import { ErrorState, Field } from '../../components/common';
import { durationMinutes, durationLabel, dateText } from './time';
export default function WorkLogForm({ issue, log, initialDate, onClose, onSaved }) {
  const { session } = useAuth() || {};
  const userId = session?.user?.id;
  const [timed, setTimed] = useState(Boolean(log?.startTime));
  const text = '';
  const page = 1;
  const [selected, setSelected] = useState(issue?.id || log?.issueId || '');
  const query = useSearchIssuesQuery(
    {
      Text: text,
      Page: page,
      PageSize: 100,
      ...(userId ? { AssigneeUserId: userId } : {}),
    },
    { skip: Boolean(issue || log) },
  );

  const assignedIssues = useMemo(() => {
    const list = query.data?.items || [];
    if (!userId) return list;
    return list.filter(
      (i) => i.assigneeUserId === undefined || String(i.assigneeUserId) === String(userId),
    );
  }, [query.data?.items, userId]);
  const [create, creating] = useCreateWorkLogMutation();
  const [update, updating] = useUpdateWorkLogMutation();
  const [runCopilot, { isLoading: isCopilotLoading }] = useRunAthennaCopilotMutation();
  const descRef = useRef(null);
  const [error, setError] = useState(null);
  const busy = creating.isLoading || updating.isLoading;

  async function handleAutoDraft() {
    const chosenIssue = assignedIssues.find((i) => String(i.id) === String(selected)) || issue;
    const issueTitle = chosenIssue ? `${chosenIssue.key || ''} ${chosenIssue.summary || ''}` : '';
    const currentText = descRef.current?.value || '';
    const promptInput = issueTitle ? `Ticket: ${issueTitle}. Notes: ${currentText}` : currentText;
    if (!promptInput.trim()) {
      setError({ message: 'Please select an issue first to auto-draft a worklog description.' });
      return;
    }
    try {
      const res = await runCopilot({
        feature: 'DraftWorklog',
        inputText: promptInput,
      }).unwrap();
      if (res?.outputText && descRef.current) {
        descRef.current.value = res.outputText;
      }
    } catch (err) {
      console.error('Failed to draft worklog with Athenna:', err);
    }
  }
  async function save(e) {
    e.preventDefault();
    if (busy) return;
    const form = new FormData(e.currentTarget);
    const minutes = timed ? 0 : durationMinutes(form.get('duration'));
    if (
      timed &&
      (!form.get('startTime') ||
        !form.get('endTime') ||
        form.get('endTime') <= form.get('startTime'))
    ) {
      setError({ message: 'End time must be after start time on the same day.' });
      return;
    }
    if (!timed && !minutes) {
      setError({ message: 'Enter hours such as 1, 3, 5 or 1.5, up to 24 hours.' });
      return;
    }
    if (!selected) {
      setError({ message: 'Choose an issue you can access.' });
      return;
    }
    const body = {
      issueId: Number(selected),
      workDate: form.get('workDate'),
      durationMinutes: minutes,
      ...(timed ? { startTime: form.get('startTime'), endTime: form.get('endTime') } : {}),
      description: form.get('description'),
      billable: form.get('billable') === 'on',
    };
    try {
      const res = await (log ? update({ id: log.id, version: log.version, body }) : create(body)).unwrap();
      onSaved?.(res);
      onClose?.();
    } catch (failure) {
      setError(failure);
    }
  }
  return (
    <form
      className="panel time-form"
      onSubmit={save}
      aria-label={log ? 'Edit work log' : 'Log work'}
    >
      <h2>{log ? 'Edit work log' : 'Log work'}</h2>
      <fieldset disabled={busy}>
        {issue || log ? (
          <p>
            {issue?.issueKey || log?.issueKey} — {issue?.summary || log?.issueSummary}
          </p>
        ) : (
          <>
            <label>
              Issue
              <select required value={selected} onChange={(e) => setSelected(e.target.value)}>
                <option value="">Choose an issue</option>
                {selected && !assignedIssues.some((i) => String(i.id) === String(selected)) && (
                  <option value={selected}>Selected issue #{selected}</option>
                )}
                {assignedIssues.length === 0 && !query.isLoading && (
                  <option value="" disabled>No issues assigned to you</option>
                )}
                {assignedIssues.map((i) => (
                  <option key={i.id} value={i.id}>
                    {i.issueKey} — {i.summary}
                  </option>
                ))}
              </select>
            </label>
            <ErrorState error={query.error} retry={query.refetch} />
          </>
        )}
        <label className="time-checkbox time-calc-toggle">
          <input type="checkbox" checked={timed} onChange={(e) => setTimed(e.target.checked)} />{' '}
          Calculate from start and end times
        </label>
        <div className="time-form-grid">
          <Field
            label="Work date"
            type="date"
            name="workDate"
            required
            defaultValue={log?.workDate || initialDate || dateText(new Date())}
          />
          {!timed && (
            <Field
              label="Hours"
              inputMode="decimal"
              name="duration"
              required
              defaultValue={log ? durationLabel(log.durationMinutes) : ''}
              placeholder="e.g. 1, 3, 5"
            />
          )}
          {timed && (
            <>
              <Field
                label="Start time"
                name="startTime"
                type="time"
                step="60"
                required
                defaultValue={log?.startTime?.slice(0, 5) || ''}
              />
              <Field
                label="End time"
                name="endTime"
                type="time"
                step="60"
                required
                defaultValue={log?.endTime?.slice(0, 5) || ''}
              />
            </>
          )}
        </div>
        <p className="hint">
          Enter decimal hours. For example, 1.5 means 1 hour 30 minutes. Saved to the nearest
          minute.
        </p>
        <div className="field-with-copilot">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
            <label htmlFor="worklog-description" style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--text-primary, #0f172a)' }}>
              Description
            </label>
            <FeatureGuard feature={ATHENNA}>
              <button
              type="button"
              onClick={handleAutoDraft}
              disabled={isCopilotLoading || busy}
              style={{
                background: 'linear-gradient(135deg, rgba(79, 70, 229, 0.08) 0%, rgba(124, 58, 237, 0.08) 100%)',
                border: '1px solid rgba(124, 58, 237, 0.25)',
                borderRadius: '16px',
                padding: '3px 10px',
                fontSize: '0.78rem',
                fontWeight: 600,
                color: 'var(--primary, #4f46e5)',
                cursor: isCopilotLoading ? 'wait' : 'pointer',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '4px',
              }}
              title="Auto-draft description from selected issue using Athenna"
            >
              <span>✦</span>
              <span>{isCopilotLoading ? 'Drafting...' : 'Auto-Draft with Athenna'}</span>
              </button>
            </FeatureGuard>
          </div>
          <textarea
            id="worklog-description"
            ref={descRef}
            name="description"
            maxLength={4000}
            rows={3}
            defaultValue={log?.description || ''}
            style={{ width: '100%', boxSizing: 'border-box' }}
          />
        </div>
        <label className="time-checkbox">
          <input type="checkbox" name="billable" defaultChecked={log?.billable ?? true} />
          Billable
        </label>
        <ErrorState error={error} />
        <div className="time-actions">
          <button className="primary" type="submit">
            {busy ? 'Saving…' : 'Save work log'}
          </button>
          {onClose && (
            <button type="button" onClick={onClose}>
              Cancel
            </button>
          )}
        </div>
      </fieldset>
    </form>
  );
}
