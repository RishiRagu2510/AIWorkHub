import React, { useMemo, useState } from 'react';
import { useCreateWorkLogMutation } from '../services/api/timesheets';
import { formatDisplayKey, formatMinutes, parseDuration } from './athennaChatUtils';
import CustomDatePicker from './CustomDatePicker';

const QUICK = [15, 30, 60, 120, 240, 480];

const messageOf = (error) => error?.data?.message || error?.message || 'The time was not logged.';

/**
 * A Log time form in the chat: pick one of your issues, tap a duration (or type one), set the day and an optional
 * note, then Log time. It saves through the same work log endpoint, and so the same permission and timesheet rules,
 * as the Chronos page.
 */
export default function WorkLogCard({ form, disabled }) {
  const [createWorkLog, { isLoading }] = useCreateWorkLogMutation();
  const [issueId, setIssueId] = useState(form.selectedIssueId ?? (form.issues.length === 1 ? form.issues[0].id : null));
  const [quick, setQuick] = useState(null);
  const [custom, setCustom] = useState('');
  const [date, setDate] = useState(form.today || '');
  const [note, setNote] = useState('');
  const [error, setError] = useState(null);
  const [logged, setLogged] = useState(null);

  const minutes = custom.trim() ? parseDuration(custom) : quick;
  const issue = useMemo(() => form.issues.find((i) => i.id === issueId) || null, [form.issues, issueId]);
  const customInvalid = custom.trim() !== '' && minutes == null;
  const ready = Boolean(issue && minutes && date && !isLoading && !disabled);

  async function submit(e) {
    e.preventDefault();
    if (!ready) return;
    setError(null);
    try {
      await createWorkLog({
        issueId: issue.id,
        workDate: date,
        durationMinutes: minutes,
        description: note.trim() || undefined,
        billable: true,
      }).unwrap();
      setLogged({ issueKey: issue.issueKey, minutes, date });
    } catch (err) {
      setError(messageOf(err));
    }
  }

  if (logged) {
    return (
      <div className="worklog-card done" role="status">
        <span className="worklog-done-check" aria-hidden="true">✓</span>
        <span>
          Logged <strong>{formatMinutes(logged.minutes)}</strong> on <strong>{logged.issueKey}</strong> for {logged.date}.
        </span>
      </div>
    );
  }

  return (
    <form className="worklog-card" onSubmit={submit} aria-label="Log time">
      <div className="worklog-title">Log time</div>

      <div className="worklog-label" id="worklog-issue-label">Issue</div>
      {form.issues.length === 0 ? (
        <div className="worklog-empty">{form.note || 'You have no open issues assigned to you.'}</div>
      ) : (
        <div className="worklog-issues" role="listbox" aria-labelledby="worklog-issue-label">
          {form.issues.map((i) => (
            <button
              key={i.id}
              type="button"
              role="option"
              aria-selected={i.id === issueId}
              aria-label={`${i.issueKey} ${i.summary}`}
              className={`worklog-issue ${i.id === issueId ? 'selected' : ''}`}
              onClick={() => setIssueId(i.id)}
            >
              <span className="worklog-issue-key">{formatDisplayKey(i.issueKey)}</span>
              <span className="worklog-issue-summary">{i.summary}</span>
              {i.id === issueId && <span className="worklog-issue-check" aria-hidden="true">✓</span>}
            </button>
          ))}
        </div>
      )}

      <div className="worklog-label">Time spent</div>
      <div className="worklog-chips">
        {QUICK.map((q) => (
          <button
            key={q}
            type="button"
            className={`worklog-chip ${!custom.trim() && quick === q ? 'selected' : ''}`}
            aria-pressed={!custom.trim() && quick === q}
            onClick={() => {
              setQuick(q);
              setCustom('');
            }}
          >
            {formatMinutes(q)}
          </button>
        ))}
        <input
          className={`worklog-custom ${customInvalid ? 'invalid' : ''}`}
          type="text"
          inputMode="text"
          placeholder="Other, e.g. 1h 30m"
          aria-label="Other duration"
          value={custom}
          onChange={(e) => setCustom(e.target.value)}
        />
      </div>
      {customInvalid && <div className="worklog-hint error">Use a time like 45m, 1.5h or 1h 30m (up to 24h).</div>}

      <div className="worklog-row">
        <label className="worklog-field">
          <span className="worklog-label">Date</span>
          <CustomDatePicker
            value={date}
            max={form.today || undefined}
            onChange={(e) => setDate(e.target.value)}
            aria-label="Date"
            placeholder="Pick date"
          />
        </label>
        <label className="worklog-field grow">
          <span className="worklog-label">What did you do? (optional)</span>
          <input type="text" maxLength={500} value={note} onChange={(e) => setNote(e.target.value)} placeholder="Short note" aria-label="Note" />
        </label>
      </div>

      {error && <div className="worklog-hint error" role="alert">{error}</div>}

      <div className="issue-create-footer-bar">
        {!ready && (
          <span className="issue-create-hint">
            {!issue
              ? '👉 Select an issue above'
              : !minutes
              ? '👉 Pick a time duration above'
              : !date
              ? '👉 Pick a date'
              : ''}
          </span>
        )}
        <div className="worklog-actions">
          <button type="submit" className="worklog-submit" disabled={!ready}>
            {isLoading ? 'Logging…' : minutes && issue ? `Log ${formatMinutes(minutes)} on ${formatDisplayKey(issue.issueKey)}` : 'Log time'}
          </button>
        </div>
      </div>
    </form>
  );
}
