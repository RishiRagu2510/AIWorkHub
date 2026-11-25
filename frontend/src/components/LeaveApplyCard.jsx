import React, { useMemo, useState } from 'react';
import { useSaveAbsenceMutation } from '../services/api/absence';
import CustomDatePicker from './CustomDatePicker';

const ICONS = {
  CASUAL: (
    <>
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41" />
    </>
  ),
  SICK: <path d="M22 12h-4l-3 9L9 3l-3 9H2" />,
  EARNED: (
    <>
      <circle cx="12" cy="8" r="7" />
      <polyline points="8.21 13.89 7 23 12 20 17 23 15.79 13.88" />
    </>
  ),
  UNPAID: (
    <>
      <circle cx="12" cy="12" r="10" />
      <line x1="4.93" y1="4.93" x2="19.07" y2="19.07" />
    </>
  ),
  WFH: (
    <>
      <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
      <polyline points="9 22 9 12 15 12 15 22" />
    </>
  ),
};

const messageOf = (error) => error?.message || error?.data?.message || 'The request was not sent.';

const parse = (value) => (value ? new Date(`${value}T00:00:00`) : null);
const iso = (date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
const nice = (value) => new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' }).format(parse(value));

/** Weekdays between two dates, both included, leaving out company holidays. */
export function workingDays(start, end, holidays = []) {
  const from = parse(start);
  const to = parse(end);
  if (!from || !to || to < from) return 0;
  const off = new Set(holidays);
  let days = 0;
  for (let d = new Date(from); d <= to; d.setDate(d.getDate() + 1)) {
    if (d.getDay() !== 0 && d.getDay() !== 6 && !off.has(iso(d))) days += 1;
  }
  return days;
}

/**
 * The Apply for leave form in the chat: one button for each leave type, then the dates. Nothing is sent until the user
 * presses Apply, and it goes through the same endpoint, and so the same checks, as the Persona leave page.
 */
export default function LeaveApplyCard({ form, disabled, onApplied }) {
  const [saveAbsence, { isLoading }] = useSaveAbsenceMutation();
  const [kind, setKind] = useState(form.selectedKind || null);
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState(null);
  const [sent, setSent] = useState(null);

  const type = useMemo(() => form.types.find((t) => t.kind === kind) || null, [form.types, kind]);
  const limited = form.types.filter((t) => t.available != null);
  const sameYear = startDate && endDate && startDate.slice(0, 4) === endDate.slice(0, 4);
  const knowsHolidays = sameYear && Number(startDate.slice(0, 4)) === form.year;
  const days = sameYear ? workingDays(startDate, endDate, knowsHolidays ? form.holidays : []) : 0;

  const problem = !kind
    ? 'Choose a leave type'
    : !startDate
      ? 'Choose the first day'
      : !endDate
        ? 'Choose the last day'
        : endDate < startDate
          ? 'The last day is before the first day'
          : !sameYear
            ? 'A request cannot span two years; split it at 31 December'
            : days === 0
              ? 'That range has no working days'
              : type?.available != null && days > type.available
                ? `Only ${type.available} ${type.available === 1 ? 'day' : 'days'} of ${type.label.toLowerCase()} left`
                : '';
  const ready = form.policyConfigured && !problem && !isLoading && !disabled;

  async function submit(e) {
    e.preventDefault();
    if (!ready) return;
    setError(null);
    try {
      const result = await saveAbsence({
        body: { kind, startDate, endDate, reason: reason.trim() || 'Requested through Athenna' },
      }).unwrap();
      setSent({ label: type.label, startDate, endDate, days: result?.days ?? days });
      onApplied?.();
    } catch (err) {
      setError(messageOf(err));
    }
  }

  if (sent) {
    return (
      <div className="worklog-card done issue-created-card" role="status">
        <span className="worklog-done-check" aria-hidden="true">✓</span>
        <div className="issue-created-body">
          <div className="issue-created-heading">Request sent</div>
          <div className="issue-created-summary">{sent.label}</div>
          <div className="issue-created-meta">
            <span>{sent.startDate === sent.endDate ? nice(sent.startDate) : `${nice(sent.startDate)} – ${nice(sent.endDate)}`}</span>
            <span aria-hidden="true">·</span>
            <span>{sent.days} working {sent.days === 1 ? 'day' : 'days'}</span>
            <span aria-hidden="true">·</span>
            <span>{form.manager ? <>Waiting for <strong>{form.manager}</strong> to approve</> : 'Waiting for approval'}</span>
          </div>
        </div>
      </div>
    );
  }

  return (
    <form className="worklog-card leave-apply-card" onSubmit={submit} aria-label="Apply for leave">
      <div className="issue-create-card-header">
        <div className="issue-create-title-group">
          <span className="issue-create-title-icon">🏝️</span>
          <div className="worklog-title">Apply for leave</div>
        </div>
      </div>

      {!form.policyConfigured && (
        <div className="worklog-hint error" role="alert">
          The leave policy for {form.year} has not been set up yet. Please ask an administrator to configure it.
        </div>
      )}

      {limited.length > 0 && (
        <div className="leave-balance-strip" aria-label="Leave remaining">
          <span className="leave-balance-title">Leave remaining</span>
          {limited.map((t) => (
            <span key={t.kind} className={`leave-balance-chip tone-${t.kind.toLowerCase()}`}>
              {t.label}
              <strong>{t.available} {t.available === 1 ? 'day' : 'days'}</strong>
            </span>
          ))}
        </div>
      )}

      <div className="worklog-label" id="leave-type-label">Leave type</div>
      <div className="leave-type-grid" role="group" aria-labelledby="leave-type-label">
        {form.types.map((t) => (
          <button
            key={t.kind}
            type="button"
            className={`leave-type-btn tone-${t.kind.toLowerCase()} ${kind === t.kind ? 'selected' : ''}`}
            aria-pressed={kind === t.kind}
            onClick={() => setKind(t.kind)}
          >
            <span className="leave-type-icon" aria-hidden="true">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                {ICONS[t.kind]}
              </svg>
            </span>
            <span className="leave-type-text">
              <span className="leave-type-name">{t.label}</span>
              <span className="leave-type-left">
                {t.available == null
                  ? 'No limit'
                  : t.allowance != null
                    ? `${t.available} of ${t.allowance} ${t.allowance === 1 ? 'day' : 'days'} left`
                    : `${t.available} ${t.available === 1 ? 'day' : 'days'} left`}
              </span>
              {t.pending > 0 && <span className="leave-type-pending">{t.pending} pending approval</span>}
            </span>
            {kind === t.kind && <span className="leave-type-check" aria-hidden="true">✓</span>}
          </button>
        ))}
      </div>

      <div className="leave-dates">
        <label className="worklog-field">
          <span className="worklog-label">From</span>
          <CustomDatePicker
            value={startDate}
            min={iso(new Date())}
            onChange={(e) => {
              setStartDate(e.target.value);
              if (!endDate || endDate < e.target.value) setEndDate(e.target.value);
            }}
            aria-label="From"
            placeholder="From date"
          />
        </label>
        <label className="worklog-field">
          <span className="worklog-label">To</span>
          <CustomDatePicker
            value={endDate}
            min={startDate || iso(new Date())}
            onChange={(e) => setEndDate(e.target.value)}
            aria-label="To"
            placeholder="To date"
          />
        </label>
      </div>

      {days > 0 && !problem && (
        <div className="leave-days-note">{days} working {days === 1 ? 'day' : 'days'}, weekends and company holidays not counted</div>
      )}

      <label className="worklog-field">
        <span className="worklog-label">Reason (optional)</span>
        <input
          className="worklog-title-input"
          type="text"
          maxLength={1000}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder="Add a short reason for your manager"
          aria-label="Reason"
        />
      </label>

      {error && <div className="worklog-hint error" role="alert">{error}</div>}

      <div className="issue-create-footer-bar">
        {problem && form.policyConfigured && (
          <span className="issue-create-hint">
            <span className="hint-bullet-dot">•</span>
            {problem}
          </span>
        )}
        <div className="worklog-actions">
          <button type="submit" className="worklog-submit" disabled={!ready}>
            {isLoading ? 'Sending…' : 'Apply'}
          </button>
        </div>
      </div>
    </form>
  );
}
