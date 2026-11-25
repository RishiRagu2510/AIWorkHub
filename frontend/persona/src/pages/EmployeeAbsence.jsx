import { useState, useRef, useEffect } from 'react';
import { Link } from 'react-router';
import { useAuth } from '../../../src/features/auth/context';
import { useAbsenceRequestsQuery, useAbsencePolicyQuery, useSaveAbsenceMutation, useSaveAbsencePolicyMutation } from '../../../src/services/api/absence';
import { PageHeader, QueryState, ErrorState, Badge, Pagination } from '../../../src/components/common';
import '../../../src/styles/employees.scss';
import '../../../src/styles/absence.scss';

const names = { CASUAL: 'Floating holidays', SICK: 'Sick leave', EARNED: 'Earned leave', UNPAID: 'Unpaid leave', WFH: 'Work from home' };

const kindIcons = {
  CASUAL: (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="4"/>
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41"/>
    </svg>
  ),
  SICK: (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M22 12h-4l-3 9L9 3l-3 9H2"/>
    </svg>
  ),
  EARNED: (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="8" r="7"/>
      <polyline points="8.21 13.89 7 23 12 20 17 23 15.79 13.88"/>
    </svg>
  ),
  UNPAID: (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10"/>
      <line x1="4.93" y1="4.93" x2="19.07" y2="19.07"/>
    </svg>
  ),
  WFH: (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>
      <polyline points="9 22 9 12 15 12 15 22"/>
    </svg>
  ),
};

function CustomYearSelect({ year, onYearChange, currentYear }) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef(null);

  useEffect(() => {
    function handleClickOutside(event) {
      if (containerRef.current && !containerRef.current.contains(event.target)) {
        setOpen(false);
      }
    }
    if (open) {
      document.addEventListener('mousedown', handleClickOutside);
      return () => document.removeEventListener('mousedown', handleClickOutside);
    }
  }, [open]);

  const years = [];
  for (let y = currentYear - 3; y <= currentYear + 3; y++) {
    years.push(y);
  }
  if (!years.includes(year)) {
    years.push(year);
    years.sort((a, b) => a - b);
  }

  return (
    <div className="field absence-year-field" ref={containerRef}>
      <label className="absence-field-label">
        <span>Year</span>
        <select
          className="absence-hidden-native-select"
          aria-label="Year"
          value={year}
          onChange={(e) => onYearChange(Number(e.target.value))}
        >
          {years.map((y) => (
            <option key={y} value={y}>{y}</option>
          ))}
        </select>
      </label>

      <div
        className={`absence-year-trigger ${open ? 'active' : ''}`}
        onClick={() => setOpen((prev) => !prev)}
        tabIndex={0}
        onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); setOpen((prev) => !prev); } }}
      >
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="absence-year-calendar-icon">
          <rect x="3" y="4" width="18" height="18" rx="2" ry="2"/>
          <line x1="16" y1="2" x2="16" y2="6"/>
          <line x1="8" y1="2" x2="8" y2="6"/>
          <line x1="3" y1="10" x2="21" y2="10"/>
        </svg>
        <span className="absence-year-val">{year}</span>
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={`absence-year-chevron ${open ? 'open' : ''}`}>
          <polyline points="6 9 12 15 18 9" />
        </svg>
      </div>

      {open && (
        <div className="absence-year-popover">
          {years.map((y) => {
            const isSelected = y === year;
            const isCurrent = y === currentYear;
            return (
              <div
                key={y}
                className={`absence-year-option ${isSelected ? 'selected' : ''}`}
                onClick={() => {
                  onYearChange(y);
                  setOpen(false);
                }}
              >
                <span className="absence-year-option-text">{y}</span>
                {isCurrent && <span className="absence-year-curr-tag">Current</span>}
                {isSelected && (
                  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="absence-year-check">
                    <polyline points="20 6 9 17 4 12" />
                  </svg>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

function NumberStepper({ label, value, onChange, min = 0, max = 366, required }) {
  const numVal = Number(value) || 0;
  const stepDown = () => {
    const next = Math.max(min, numVal - 1);
    onChange({ target: { value: next } });
  };
  const stepUp = () => {
    const next = Math.min(max, numVal + 1);
    onChange({ target: { value: next } });
  };

  return (
    <label className="field absence-stepper-field">
      <span>{label}</span>
      <div className="absence-stepper-group">
        <button
          type="button"
          className="absence-stepper-btn btn-down"
          onClick={stepDown}
          disabled={numVal <= min}
          aria-label={`Decrease ${label}`}
        >
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <line x1="5" y1="12" x2="19" y2="12" />
          </svg>
        </button>
        <input
          type="number"
          min={min}
          max={max}
          required={required}
          value={value}
          onChange={onChange}
        />
        <button
          type="button"
          className="absence-stepper-btn btn-up"
          onClick={stepUp}
          disabled={numVal >= max}
          aria-label={`Increase ${label}`}
        >
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <line x1="12" y1="5" x2="12" y2="19" />
            <line x1="5" y1="12" x2="19" y2="12" />
          </svg>
        </button>
      </div>
    </label>
  );
}

function CustomSelect({ label, value, onChange, options }) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef(null);

  useEffect(() => {
    function handleClickOutside(event) {
      if (containerRef.current && !containerRef.current.contains(event.target)) {
        setOpen(false);
      }
    }
    if (open) {
      document.addEventListener('mousedown', handleClickOutside);
      return () => document.removeEventListener('mousedown', handleClickOutside);
    }
  }, [open]);

  const selectedName = options[value] || value;

  return (
    <div className="field absence-custom-select-field" ref={containerRef}>
      <label className="absence-field-label">
        <span>{label}</span>
        <select
          className="absence-hidden-native-select"
          aria-label={label}
          value={value}
          onChange={(e) => onChange(e.target.value)}
        >
          {Object.entries(options).map(([k, name]) => (
            <option key={k} value={k}>{name}</option>
          ))}
        </select>
      </label>

      <div
        className={`absence-select-trigger ${open ? 'active' : ''}`}
        onClick={() => setOpen((prev) => !prev)}
        tabIndex={0}
        onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); setOpen((prev) => !prev); } }}
      >
        <div className="absence-select-trigger-val">
          <span className="absence-select-icon">{kindIcons[value]}</span>
          <span>{selectedName}</span>
        </div>
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={`absence-select-chevron ${open ? 'open' : ''}`}>
          <polyline points="6 9 12 15 18 9" />
        </svg>
      </div>

      {open && (
        <div className="absence-select-popover">
          {Object.entries(options).map(([k, name]) => {
            const isSelected = value === k;
            return (
              <div
                key={k}
                className={`absence-select-option ${isSelected ? 'selected' : ''}`}
                onClick={() => {
                  onChange(k);
                  setOpen(false);
                }}
              >
                <div className="absence-option-left">
                  <span className={`absence-option-icon absence-kind-${k.toLowerCase()}`}>
                    {kindIcons[k]}
                  </span>
                  <span className="absence-option-name">{name}</span>
                </div>
                {isSelected && (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="absence-option-check">
                    <polyline points="20 6 9 17 4 12" />
                  </svg>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

function CustomDatePicker({ label, value, onChange, min, max, required }) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef(null);

  const selectedDate = value ? new Date(value + 'T00:00:00') : null;
  const [viewDate, setViewDate] = useState(() => (selectedDate && !isNaN(selectedDate.getTime()) ? selectedDate : (min ? new Date(min + 'T00:00:00') : new Date())));

  useEffect(() => {
    function handleClickOutside(event) {
      if (containerRef.current && !containerRef.current.contains(event.target)) {
        setOpen(false);
      }
    }
    if (open) {
      document.addEventListener('mousedown', handleClickOutside);
      return () => document.removeEventListener('mousedown', handleClickOutside);
    }
  }, [open]);

  useEffect(() => {
    if (value) {
      const parsed = new Date(value + 'T00:00:00');
      if (!isNaN(parsed.getTime())) {
        setViewDate(parsed);
      }
    } else if (min) {
      const minParsed = new Date(min + 'T00:00:00');
      if (!isNaN(minParsed.getTime())) {
        setViewDate(minParsed);
      }
    }
  }, [value, min]);

  const year = viewDate.getFullYear();
  const month = viewDate.getMonth();

  const monthNames = [
    'January', 'February', 'March', 'April', 'May', 'June',
    'July', 'August', 'September', 'October', 'November', 'December'
  ];

  const prevMonth = (e) => { e.stopPropagation(); setViewDate(new Date(year, month - 1, 1)); };
  const nextMonth = (e) => { e.stopPropagation(); setViewDate(new Date(year, month + 1, 1)); };

  const firstDayIndex = new Date(year, month, 1).getDay();
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const daysInPrevMonth = new Date(year, month, 0).getDate();

  const handleSelectDay = (day) => {
    const formatted = `${year}-${String(month + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
    onChange({ target: { value: formatted } });
    setOpen(false);
  };

  const handleToday = (e) => {
    e.stopPropagation();
    onChange({ target: { value: today } });
    setOpen(false);
  };

  const handleClear = (e) => {
    e.stopPropagation();
    onChange({ target: { value: '' } });
    setOpen(false);
  };

  const displayFormatted = selectedDate && !isNaN(selectedDate.getTime())
    ? selectedDate.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
    : '';

  return (
    <div className="field absence-custom-date-field" ref={containerRef}>
      <label className="absence-field-label">
        <span>{label}</span>
        <input
          type="date"
          className="absence-hidden-native-date"
          aria-label={label}
          min={min}
          max={max}
          required={required}
          value={value || ''}
          onChange={onChange}
        />
      </label>

      <div
        className={`absence-date-trigger ${open ? 'active' : ''}`}
        onClick={() => setOpen((prev) => !prev)}
        tabIndex={0}
        onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); setOpen((prev) => !prev); } }}
      >
        <span className={displayFormatted ? 'absence-date-val' : 'absence-date-placeholder'}>
          {displayFormatted || 'Select date'}
        </span>
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="absence-date-calendar-icon">
          <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
          <line x1="16" y1="2" x2="16" y2="6" />
          <line x1="8" y1="2" x2="8" y2="6" />
          <line x1="3" y1="10" x2="21" y2="10" />
        </svg>
      </div>

      {open && (
        <div className="absence-calendar-popover">
          <div className="absence-calendar-header">
            <button type="button" className="calendar-nav-btn" onClick={prevMonth} aria-label="Previous month">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="15 18 9 12 15 6" />
              </svg>
            </button>
            <span className="calendar-month-title">
              {monthNames[month]} {year}
            </span>
            <button type="button" className="calendar-nav-btn" onClick={nextMonth} aria-label="Next month">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="9 18 15 12 9 6" />
              </svg>
            </button>
          </div>

          <div className="absence-calendar-weekdays">
            {['SU', 'MO', 'TU', 'WE', 'TH', 'FR', 'SA'].map((w) => (
              <span key={w}>{w}</span>
            ))}
          </div>

          <div className="absence-calendar-days">
            {Array.from({ length: firstDayIndex }).map((_, i) => (
              <span key={`prev-${i}`} className="calendar-day muted">
                {daysInPrevMonth - firstDayIndex + 1 + i}
              </span>
            ))}

            {Array.from({ length: daysInMonth }).map((_, i) => {
              const day = i + 1;
              const dateStr = `${year}-${String(month + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
              const isSelected = value === dateStr;
              const isToday = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Kolkata', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date()) === dateStr;
              const isDisabled = (min && dateStr < min) || (max && dateStr > max);

              return (
                <button
                  key={day}
                  type="button"
                  disabled={isDisabled}
                  className={`calendar-day current ${isSelected ? 'selected' : ''} ${isToday ? 'today' : ''}`}
                  onClick={() => handleSelectDay(day)}
                >
                  {day}
                </button>
              );
            })}
          </div>

          <div className="absence-calendar-footer">
            <button type="button" className="calendar-action-link" onClick={handleClear}>Clear</button>
            <button type="button" className="calendar-action-link today-btn" onClick={handleToday}>Today</button>
          </div>
        </div>
      )}
    </div>
  );
}

function PolicyEditor({ year, policy }) {
  const [form, setForm] = useState({ casualDays: policy.casualDays, sickDays: policy.sickDays, earnedDays: policy.earnedDays });
  const [save, state] = useSaveAbsencePolicyMutation();
  const [saved, setSaved] = useState(false);
  const update = (key, value) => { setSaved(false); setForm((old) => ({ ...old, [key]: value })); };

  async function submit(e) {
    e.preventDefault();
    try {
      await save({
        year,
        body: {
          casualDays: form.casualDays,
          sickDays: form.sickDays,
          earnedDays: form.earnedDays,
          holidays: policy.holidays || [],
          version: policy.version,
        },
      }).unwrap();
      setSaved(true);
    } catch { /* ErrorState handles the mutation result. */ }
  }

  return (
    <details className="panel employee-panel absence-policy-card">
      <summary className="absence-policy-summary">
        <div className="absence-policy-summary-left">
          <span className="absence-policy-icon">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
            </svg>
          </span>
          <div className="absence-policy-title-group">
            <span className="absence-policy-title">Organization leave policy · {year}</span>
            <span className="absence-admin-pill">Admin Policy</span>
          </div>
        </div>
        <span className="absence-policy-chevron">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="6 9 12 15 18 9"></polyline>
          </svg>
        </span>
      </summary>

      <div className="absence-policy-content">
        <div className="absence-policy-info-box">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="10"/>
            <line x1="12" y1="16" x2="12" y2="12"/>
            <line x1="12" y1="8" x2="12.01" y2="8"/>
          </svg>
          <p>Confirm your company allowances before accepting requests. This template grants annual days upfront; it does not calculate accrual, carry-forward or payroll deductions.</p>
        </div>

        <form onSubmit={submit} className="absence-policy-form">
          <div className="absence-policy-allowances">
            <div className="absence-policy-tile tile-casual">
              <div className="tile-top">
                <span className="tile-icon">{kindIcons.CASUAL}</span>
                <span className="tile-tag">Annual</span>
              </div>
              <NumberStepper
                label="Floating holiday days"
                min={0}
                max={366}
                required
                value={form.casualDays}
                onChange={(e) => update('casualDays', Number(e.target.value))}
              />
            </div>

            <div className="absence-policy-tile tile-sick">
              <div className="tile-top">
                <span className="tile-icon">{kindIcons.SICK}</span>
                <span className="tile-tag">Annual</span>
              </div>
              <NumberStepper
                label="Sick leave days"
                min={0}
                max={366}
                required
                value={form.sickDays}
                onChange={(e) => update('sickDays', Number(e.target.value))}
              />
            </div>

            <div className="absence-policy-tile tile-earned">
              <div className="tile-top">
                <span className="tile-icon">{kindIcons.EARNED}</span>
                <span className="tile-tag">Annual</span>
              </div>
              <NumberStepper
                label="Earned leave days"
                min={0}
                max={366}
                required
                value={form.earnedDays}
                onChange={(e) => update('earnedDays', Number(e.target.value))}
              />
            </div>
          </div>

          <div className="absence-policy-actions">
            <button className="primary" disabled={state.isLoading}>
              {state.isLoading ? 'Saving policy...' : 'Save policy'}
            </button>
            {saved && (
              <span className="absence-save-status" role="status">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <polyline points="20 6 9 17 4 12"/>
                </svg>
                Policy saved.
              </span>
            )}
          </div>
        </form>
        <ErrorState error={state.error} />
      </div>
    </details>
  );
}

function RequestRow({ row, team, busy, decide }) {
  const [note, setNote] = useState('');
  const cancellable = row.status === 'PENDING';

  const kindBadgeClass = `absence-kind-chip absence-kind-${(row.kind || '').toLowerCase()}`;

  return (
    <tr>
      {team && (
        <td>
          <div className="absence-employee-cell">
            <div className="absence-employee-avatar">{(row.employeeName || '?').charAt(0).toUpperCase()}</div>
            <div className="absence-employee-name">{row.employeeName}</div>
          </div>
        </td>
      )}
      <td>
        <span className={kindBadgeClass}>
          {kindIcons[row.kind]}
          <span>{names[row.kind] || row.kind}</span>
        </span>
      </td>
      <td className="absence-dates-cell">
        <div className="absence-dates-range">
          <span className="absence-date-val">{row.startDate}</span>
          <span className="absence-date-sep">→</span>
          <span className="absence-date-val">{row.endDate}</span>
        </div>
      </td>
      <td className="absence-days-cell">
        <span className="absence-days-pill">{row.days} {row.days === 1 ? 'day' : 'days'}</span>
      </td>
      <td className="absence-status-cell">
        <Badge value={row.status} />
      </td>
      <td className="absence-reason-cell">
        <div className="absence-reason-text">{row.reason}</div>
        {row.decisionNote && (
          <div className="absence-decision-note">
            <span className="absence-decision-label">Decision note:</span> {row.decisionNote}
          </div>
        )}
      </td>
      <td className="absence-actions-cell">
        {team && row.status === 'PENDING' ? (
          <div className="absence-decisions">
            {/* Leave Coverage & Blackout Validator */}
            {row.days >= 5 && (
              <div style={{
                fontSize: '0.72rem',
                color: '#b45309',
                background: '#fffbeb',
                padding: '2px 6px',
                borderRadius: '4px',
                marginBottom: '4px',
                border: '1px solid #fef3c7',
                display: 'flex',
                alignItems: 'center',
                gap: '3px'
              }}>
                <span>⚠️</span>
                <span>Check department quorum ({row.days}d block)</span>
              </div>
            )}
            <input
              className="absence-decision-input"
              aria-label={`Decision note for request ${row.id}`}
              placeholder="Reason required to reject"
              maxLength={1000}
              value={note}
              onChange={(e) => setNote(e.target.value)}
            />
            <div className="absence-decision-buttons">
              <button
                type="button"
                className="absence-btn-approve"
                disabled={busy}
                onClick={() => decide(row, 'APPROVE', note)}
              >
                Approve
              </button>
              <button
                type="button"
                className="absence-btn-reject"
                disabled={busy || !note.trim()}
                onClick={() => decide(row, 'REJECT', note)}
                title={!note.trim() ? 'Enter a reason to reject' : 'Reject request'}
              >
                Reject
              </button>
            </div>
          </div>
        ) : !team && cancellable ? (
          <button type="button" className="absence-btn-cancel" disabled={busy} onClick={() => decide(row, 'CANCEL')}>
            Cancel request
          </button>
        ) : (
          <span className="absence-empty-action">—</span>
        )}
      </td>
    </tr>
  );
}

function calculateWorkingDays(startStr, endStr) {
  if (!startStr || !endStr) return 0;
  const start = new Date(startStr + 'T00:00:00');
  const end = new Date(endStr + 'T00:00:00');
  if (isNaN(start.getTime()) || isNaN(end.getTime()) || start > end) return 0;

  let count = 0;
  const cur = new Date(start);
  while (cur <= end) {
    const dayOfWeek = cur.getDay();
    if (dayOfWeek !== 0 && dayOfWeek !== 6) {
      count++;
    }
    cur.setDate(cur.getDate() + 1);
  }
  return count;
}

export default function EmployeeAbsence() {
  const { can } = useAuth();
  const currentYear = Number(new Intl.DateTimeFormat('en', { year: 'numeric', timeZone: 'Asia/Kolkata' }).format(new Date()));
  const [year, setYear] = useState(currentYear);
  const [team, setTeam] = useState(false);
  const [page, setPage] = useState(1);
  const [form, setForm] = useState({ kind: 'CASUAL', startDate: '', endDate: '', reason: '' });
  const [notice, setNotice] = useState('');
  const query = useAbsenceRequestsQuery({ year, team, page }, { refetchOnMountOrArgChange: true, refetchOnFocus: true, pollingInterval: 30000 });
  const policy = useAbsencePolicyQuery(year);
  const [save, mutation] = useSaveAbsenceMutation();
  const update = (key, value) => setForm((old) => ({ ...old, [key]: value }));

  const data = query.data;
  const requestedDays = calculateWorkingDays(form.startDate, form.endDate);
  const selectedBalance = data?.balances?.find((b) => b.kind === form.kind);
  const availableDays = selectedBalance ? selectedBalance.available : 0;
  const isLeaveKind = form.kind === 'CASUAL' || form.kind === 'SICK' || form.kind === 'EARNED';
  const isExceeded = isLeaveKind && requestedDays > availableDays;

  async function submit(e) {
    e.preventDefault();
    if (isExceeded) return;
    setNotice('');
    try {
      await save({ body: form }).unwrap();
      setForm({ kind: 'CASUAL', startDate: '', endDate: '', reason: '' });
      setNotice('Request sent to your manager.');
    } catch { /* Mutation error below. */ }
  }

  async function decide(row, action, note) {
    setNotice('');
    try {
      await save({ id: row.id, body: { action, note, version: row.version } }).unwrap();
      setNotice(`Request ${action === 'APPROVE' ? 'approved' : action === 'REJECT' ? 'rejected' : 'cancelled'}.`);
    } catch { /* Mutation error below. */ }
  }

  return (
    <div className="employee-page absence-page">
      <PageHeader
        title="Leave & WFH"
        description="Plan time away, request work from home, and track manager approvals."
      >
        <div className="absence-header-year">
          <CustomYearSelect
            year={year}
            currentYear={currentYear}
            onYearChange={(next) => {
              setYear(next);
              setPage(1);
              setForm({ kind: 'CASUAL', startDate: '', endDate: '', reason: '' });
            }}
          />
        </div>
      </PageHeader>

      <ErrorState error={mutation.error} />
      {notice && (
        <div className="absence-alert-success" role="status">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/>
            <polyline points="22 4 12 14.01 9 11.01"/>
          </svg>
          <span>{notice}</span>
        </div>
      )}

      <QueryState query={policy}>
        {can('EMPLOYEE_UPDATE') && policy.data && (
          <PolicyEditor key={`${year}-${policy.data.version}`} year={year} policy={policy.data} />
        )}
      </QueryState>

      {query.error?.status === 404 ? (
        <section className="panel employee-panel absence-empty-profile-card">
          <div className="absence-empty-icon">
            <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/>
              <circle cx="12" cy="7" r="4"/>
            </svg>
          </div>
          <h2>Your employee profile is not set up yet</h2>
          <p>Your sign-in account is available, but  needs a linked employee profile and an active reporting manager.</p>
          {can('EMPLOYEE_CREATE') ? (
            <div className="absence-empty-actions">
              <p>Add your employee profile and select your login account. Then assign a reporting manager and configure the leave policy before submitting requests.</p>
              <div className="actions">
                <Link className="button primary" to="/employees/admin?create=1">Add employee profile</Link>
                <button onClick={query.refetch} disabled={query.isFetching}>Check again</button>
              </div>
            </div>
          ) : (
            <div className="absence-empty-actions">
              <p>Ask your administrator to create your employee profile and assign your reporting manager.</p>
              <button onClick={query.refetch} disabled={query.isFetching}>Check again</button>
            </div>
          )}
        </section>
      ) : (
        <QueryState query={query}>
          {data && (
            <>
              {!data.policyConfigured && (
                <div className="absence-alert-warning panel employee-panel">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/>
                    <line x1="12" y1="9" x2="12" y2="13"/>
                    <line x1="12" y1="17" x2="12.01" y2="17"/>
                  </svg>
                  <span>The {year} leave policy needs to be configured by an administrator before requests can be submitted.</span>
                </div>
              )}

              <div className="absence-balances">
                {data.balances.map((b) => {
                  const kindKey = (b.kind || '').toLowerCase();
                  const totalAllowance = b.allowance || 0;
                  const availableDays = data.policyConfigured ? b.available : '—';
                  const usedPct = totalAllowance > 0 ? Math.min(100, Math.round(((b.used || 0) / totalAllowance) * 100)) : 0;
                  const pendingPct = totalAllowance > 0 ? Math.min(100 - usedPct, Math.round(((b.pending || 0) / totalAllowance) * 100)) : 0;

                  return (
                    <section className={`panel employee-panel absence-balance-card absence-card-${kindKey}`} key={b.kind}>
                      <div className="absence-card-header">
                        <div className="absence-card-title-group">
                          <span className="absence-card-icon">{kindIcons[b.kind]}</span>
                          <h2>{names[b.kind]}</h2>
                        </div>
                        <span className="absence-card-annual-badge">
                          {b.allowance}d annual{!data.policyConfigured && ' (proposed)'}
                        </span>
                      </div>

                      <div className="absence-card-stat">
                        <strong>
                          {availableDays} <small>days available</small>
                        </strong>
                      </div>

                      {data.policyConfigured && totalAllowance > 0 && (
                        <div className="absence-card-progress-bar" title={`Used: ${b.used}, Pending: ${b.pending}, Available: ${b.available}`}>
                          <div className="absence-progress-used" style={{ width: `${usedPct}%` }} />
                          <div className="absence-progress-pending" style={{ width: `${pendingPct}%` }} />
                        </div>
                      )}

                      <div className="absence-card-footer">
                        <p>
                          <span className="absence-stat-pill approved">
                            <span className="dot" /> {b.used} approved
                          </span>
                          <span className="absence-stat-pill pending">
                            <span className="dot" /> {b.pending} pending
                          </span>
                          <span className="absence-stat-pill total">
                            {b.allowance} annual{!data.policyConfigured && ' (proposed)'}
                          </span>
                        </p>
                      </div>
                    </section>
                  );
                })}
              </div>

              {!data.manager && (
                <div className="absence-alert-warning panel employee-panel">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <circle cx="12" cy="12" r="10"/>
                    <line x1="12" y1="8" x2="12" y2="12"/>
                    <line x1="12" y1="16" x2="12.01" y2="16"/>
                  </svg>
                  <span>Ask an administrator to assign an active manager to your employee profile. All leave and WFH requests need manager approval.</span>
                </div>
              )}

              <section className="panel employee-panel absence-request-card">
                <div className="absence-card-topbar">
                  <div className="absence-card-header-titles">
                    <h2>New request</h2>
                    <p className="absence-subtitle">
                      {data.manager ? 'WFH does not deduct leave days.' : 'No manager assigned.'}
                    </p>
                  </div>
                  {data.manager && (
                    <div className="absence-approver-chip">
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/>
                        <circle cx="8.5" cy="7" r="4"/>
                        <polyline points="17 11 19 13 23 9"/>
                      </svg>
                      <span>Approver: {data.manager.name}</span>
                    </div>
                  )}
                </div>

                <form className="absence-request-form" onSubmit={submit}>
                  {isExceeded && (
                    <div className="absence-alert-warning" role="alert">
                      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <circle cx="12" cy="12" r="10"/>
                        <line x1="12" y1="8" x2="12" y2="12"/>
                        <line x1="12" y1="16" x2="12.01" y2="16"/>
                      </svg>
                      <span>
                        Requested duration ({requestedDays} working {requestedDays === 1 ? 'day' : 'days'}) exceeds your available {names[form.kind]} balance ({availableDays} {availableDays === 1 ? 'day' : 'days'} available).
                      </span>
                    </div>
                  )}

                  <div className="absence-request-grid">
                    <CustomSelect
                      label="Request type"
                      value={form.kind}
                      onChange={(val) => update('kind', val)}
                      options={names}
                    />
                    <CustomDatePicker
                      label="Start date"
                      required
                      min={`${year}-01-01`}
                      max={`${year}-12-31`}
                      value={form.startDate}
                      onChange={(e) => update('startDate', e.target.value)}
                    />
                    <CustomDatePicker
                      label="End date"
                      required
                      min={form.startDate || `${year}-01-01`}
                      max={`${year}-12-31`}
                      value={form.endDate}
                      onChange={(e) => update('endDate', e.target.value)}
                    />
                    <label className="field absence-wide">
                      <span>Reason</span>
                      <textarea
                        required
                        maxLength={1000}
                        rows="3"
                        placeholder="Specify the reason or details for this request..."
                        value={form.reason}
                        onChange={(e) => update('reason', e.target.value)}
                      />
                    </label>
                  </div>
                  {requestedDays > 0 && (
                    <div className="absence-duration-bar">
                      <div className={`absence-duration-pill ${isExceeded ? 'exceeded' : 'valid'}`}>
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <rect x="3" y="4" width="18" height="18" rx="2" ry="2"/>
                          <line x1="16" y1="2" x2="16" y2="6"/>
                          <line x1="8" y1="2" x2="8" y2="6"/>
                          <line x1="3" y1="10" x2="21" y2="10"/>
                        </svg>
                        <span>
                          <strong>{requestedDays} working {requestedDays === 1 ? 'day' : 'days'} selected</strong>
                          {isLeaveKind && ` · ${availableDays} ${availableDays === 1 ? 'day' : 'days'} available`}
                        </span>
                      </div>
                    </div>
                  )}
                  <div className="absence-request-actions">
                    <button className="primary" disabled={mutation.isLoading || !data.manager || !data.policyConfigured || isExceeded}>
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <line x1="22" y1="2" x2="11" y2="13"/>
                        <polygon points="22 2 15 22 11 13 2 9 22 2"/>
                      </svg>
                      {mutation.isLoading ? 'Sending...' : 'Send request'}
                    </button>
                    <span className="absence-form-hint">
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <circle cx="12" cy="12" r="10"/>
                        <line x1="12" y1="16" x2="12" y2="12"/>
                        <line x1="12" y1="8" x2="12.01" y2="8"/>
                      </svg>
                      Only working days count; pending leave reserves your balance.
                    </span>
                  </div>
                </form>
              </section>

              <section className="panel employee-panel absence-table-card">
                <div className="absence-table-header-row">
                  <div className="actions" aria-label="Request views">
                    <button
                      className={`absence-tab-btn ${!team ? 'active' : ''}`}
                      aria-pressed={!team}
                      onClick={() => { setTeam(false); setPage(1); }}
                    >
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
                        <polyline points="14 2 14 8 20 8"/>
                        <line x1="16" y1="13" x2="8" y2="13"/>
                        <line x1="16" y1="17" x2="8" y2="17"/>
                      </svg>
                      <span>My requests</span>
                    </button>
                    {data.hasReports && (
                      <button
                        className={`absence-tab-btn ${team ? 'active' : ''}`}
                        aria-pressed={team}
                        onClick={() => { setTeam(true); setPage(1); }}
                      >
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/>
                          <circle cx="9" cy="7" r="4"/>
                          <path d="M23 21v-2a4 4 0 0 0-3-3.87"/>
                          <path d="M16 3.13a4 4 0 0 1 0 7.75"/>
                        </svg>
                        <span>Team approvals</span>
                      </button>
                    )}
                  </div>
                  <button className="absence-refresh-btn" onClick={query.refetch} disabled={query.isFetching} title="Refresh requests">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={query.isFetching ? 'spin' : ''}>
                      <polyline points="23 4 23 10 17 10"/>
                      <polyline points="1 20 1 14 7 14"/>
                      <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/>
                    </svg>
                    <span>Refresh</span>
                  </button>
                </div>

                <div className="absence-table" role="region" aria-label="Leave and WFH requests" tabIndex={0}>
                  <table>
                    <thead>
                      <tr>
                        {team && <th>Employee</th>}
                        <th>Type</th>
                        <th>Dates</th>
                        <th>Days</th>
                        <th>Status</th>
                        <th>Reason</th>
                        <th>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.items.map((row) => (
                        <RequestRow key={row.id} row={row} team={team} decide={decide} busy={mutation.isLoading} />
                      ))}
                      {data.items.length === 0 && (
                        <tr className="absence-empty-row">
                          <td colSpan={team ? 7 : 6}>
                            <div className="absence-table-empty">
                              <div className="absence-empty-icon-badge">
                                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
                                  <rect x="3" y="4" width="18" height="18" rx="2" ry="2"/>
                                  <line x1="16" y1="2" x2="16" y2="6"/>
                                  <line x1="8" y1="2" x2="8" y2="6"/>
                                  <line x1="3" y1="10" x2="21" y2="10"/>
                                </svg>
                              </div>
                              <h3 className="absence-empty-title">No requests for {year}</h3>
                              <p className="absence-empty-subtitle">
                                {team ? 'There are no pending team approval requests for this year.' : 'You have not submitted any leave or WFH requests for this year.'}
                              </p>
                            </div>
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>

                <Pagination data={data} onPage={setPage} busy={query.isFetching} />
              </section>
            </>
          )}
        </QueryState>
      )}
    </div>
  );
}
