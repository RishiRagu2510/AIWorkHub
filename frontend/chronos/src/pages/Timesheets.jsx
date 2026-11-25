import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import Link from '../../../src/components/PortalLink';
import { liveQueryOptions } from '../../../src/services/api/live';
import { useAuth } from '../../../src/features/auth/context';
import { useTimesheetQuery } from '../../../src/services/api/timesheets';
import { useProjectsQuery } from '../../../src/services/api/api';
import { QueryState, Pagination } from '../../../src/components/common';
import UserIdentity from '../../../src/components/UserIdentity';
import TimesheetHierarchy from '../../../src/features/timesheets/TimesheetHierarchy';
import TimesheetCalendar from '../../../src/features/timesheets/TimesheetCalendar';
import WorkLogForm from '../../../src/features/timesheets/WorkLogForm';
import WorkLogTable from '../../../src/features/timesheets/WorkLogTable';
import PeriodActions from '../../../src/features/timesheets/PeriodActions';
import { durationLabel, dateRange, dateText, moveDate } from '../../../src/features/timesheets/time';
import { useAbsenceRequestsQuery } from '../../../src/services/api/absence';
import '../../../src/styles/timesheets.scss';

export default function Timesheets({ team = false }) {
  const { can } = useAuth();
  const [params] = useSearchParams();
  const requestedDate = params.get('from');
  const [viewLayout, setViewLayout] = useState('table');
  const [anchor, setAnchor] = useState(
    /^\d{4}-\d{2}-\d{2}$/.test(requestedDate || '') && Number.isFinite(Date.parse(requestedDate))
      ? requestedDate
      : dateText(new Date()),
  );
  const [mode, setMode] = useState('week');
  const [custom, setCustom] = useState(dateRange(anchor));
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState(
    team && /^[1-9]\d*$/.test(params.get('userId') || '') ? { userId: params.get('userId') } : {},
  );
  const [add, setAdd] = useState(false);
  const requestedUser = params.get('userId');

  useEffect(() => {
    if (
      /^\d{4}-\d{2}-\d{2}$/.test(requestedDate || '') &&
      Number.isFinite(Date.parse(requestedDate))
    ) {
      setAnchor(requestedDate);
      setMode('week');
      setPage(1);
      setFilters(team && /^[1-9]\d*$/.test(requestedUser || '') ? { userId: requestedUser } : {});
    }
  }, [requestedDate, requestedUser, team]);

  const range = mode === 'custom' ? custom : dateRange(anchor, mode);
  const projects = useProjectsQuery(undefined, { skip: !can('PROJECT_VIEW') });
  const query = useTimesheetQuery(
    { team, ...range, ...filters, page, pageSize: 25 },
    liveQueryOptions,
  );

  function filter(key, value) {
    setFilters((f) => ({ ...f, [key]: value }));
    setPage(1);
  }

  function move(n) {
    setAnchor(moveDate(anchor, mode, n));
    setPage(1);
  }

  const data = query.data;
  let absences = { data: null };
  try {
    absences = useAbsenceRequestsQuery({ status: 'APPROVED' }, { skip: !data });
  } catch {
    // Graceful fallback in tests
  }

  // Detect Timesheet Anomalies:
  // 1. Any single day with > 12 hours (720 minutes)
  // 2. Any worklog on an approved sick or PTO leave date
  const anomalies = [];
  if (data?.dailyTotals) {
    data.dailyTotals.forEach((d) => {
      if (d.minutes > 720) {
        anomalies.push({
          type: 'HIGH_HOURS',
          date: d.date,
          message: `${d.date}: Excessive daily hours recorded (${durationLabel(d.minutes)} > 12h limit).`,
        });
      }
    });
  }

  if (absences?.data?.items && data?.workLogs?.items) {
    const approvedLeaves = absences.data.items;
    data.workLogs.items.forEach((wl) => {
      const onLeave = approvedLeaves.find((lv) => {
        return (
          (!wl.userId || String(lv.employeeId) === String(wl.userId)) &&
          wl.workDate >= lv.startDate &&
          wl.workDate <= lv.endDate
        );
      });
      if (onLeave) {
        anomalies.push({
          type: 'LEAVE_CONFLICT',
          date: wl.workDate,
          message: `${wl.workDate}: Worklog logged during approved leave (${onLeave.type || 'PTO'}).`,
        });
      }
    });
  }

  const totalWeeklyMinutes = data?.totalMinutes || 0;
  const standardGoalMinutes = 40 * 60; // 40 hours standard weekly goal
  const progressPercent = Math.min(100, Math.round((totalWeeklyMinutes / standardGoalMinutes) * 100));
  const billablePercent = totalWeeklyMinutes > 0
    ? Math.round(((data?.billableMinutes || 0) / totalWeeklyMinutes) * 100)
    : 0;

  return (
    <div className="timesheet-workspace">
      {/* Top Header with Integrated Nav and Single Primary Action */}
      <header className="timesheet-header-banner">
        <div className="timesheet-header-main">
          <div className="timesheet-badge-tag">
            <span className="pulse-dot" />
            <span>Chronos Time Tracker</span>
          </div>
          <h1 className="timesheet-title">{team ? 'Team timesheets' : 'My timesheet'}</h1>
          <p className="timesheet-subtitle">
            Track focused work. All time values are shown in hours.
          </p>
        </div>

        <div className="timesheet-header-actions">
          <nav className="timesheet-nav-pills" role="tablist" aria-label="Timesheet screens">
            <Link className={`timesheet-nav-pill ${!team ? 'is-active' : ''}`} to="/timesheets/my">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <circle cx="12" cy="12" r="9" />
                <path d="M12 6v6l4 2" />
              </svg>
              My timesheet
            </Link>
            <Link className="timesheet-nav-pill" to="/timesheets/history">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
                <path d="M3 3v5h5" />
                <polyline points="12 7 12 12 15 15" />
              </svg>
              Timesheet history
            </Link>
            {can('TIMESHEET_TEAM_VIEW') && (
              <Link className={`timesheet-nav-pill ${team ? 'is-active' : ''}`} to="/timesheets/team">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                  <circle cx="9" cy="7" r="4" />
                  <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
                  <path d="M16 3.13a4 4 0 0 1 0 7.75" />
                </svg>
                Team timesheets
              </Link>
            )}
          </nav>

          {can('WORKLOG_CREATE') && (
            <Link className="button primary timesheet-log-work-cta" to="/timesheets/log-work">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <line x1="12" y1="5" x2="12" y2="19" />
                <line x1="5" y1="12" x2="19" y2="12" />
              </svg>
              Log work
            </Link>
          )}
        </div>
      </header>

      {/* Missing Timesheet Reminder Daemon Banner */}
      {!team && totalWeeklyMinutes < standardGoalMinutes && (new Date().getDay() >= 5 || new Date().getDay() === 0) && (
        <div style={{
          margin: '0 0 16px 0',
          padding: '10px 16px',
          borderRadius: '8px',
          background: '#eff6ff',
          border: '1px solid #bfdbfe',
          color: '#1d4ed8',
          fontSize: '0.86rem',
          display: 'flex',
          alignItems: 'center',
          gap: '8px',
        }}>
          <span>⏱️</span>
          <div>
            <strong>Missing Timesheet Reminder:</strong> You have logged {durationLabel(totalWeeklyMinutes)} of your 40h weekly target. Please submit your remaining timesheet entries before Friday close of business.
          </div>
        </div>
      )}

      {/* Date Navigation & Filters Toolbar */}
      <div className="panel time-toolbar">
        <div className="time-toolbar-primary">
          <div className="time-actions">
            <button
              className="time-nav-btn"
              disabled={mode === 'custom'}
              onClick={() => move(-1)}
              title={`Previous ${mode}`}
            >
              Previous {mode}
            </button>
            <button
              className="time-nav-btn time-current-btn"
              onClick={() => {
                setAnchor(dateText(new Date()));
                setMode('week');
                setPage(1);
              }}
            >
              Current week
            </button>
            <button
              className="time-nav-btn"
              disabled={mode === 'custom'}
              onClick={() => move(1)}
              title={`Next ${mode}`}
            >
              Next {mode}
            </button>
            <select
              className="time-mode-select"
              aria-label="Timesheet view"
              value={mode}
              onChange={(e) => {
                setMode(e.target.value);
                setPage(1);
              }}
            >
              <option value="day">Daily</option>
              <option value="week">Weekly</option>
              <option value="month">Monthly</option>
              <option value="custom">Custom range</option>
            </select>
          </div>

          <div className="time-date-range-badge">
            <svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
              <rect x="3" y="4" width="18" height="18" rx="2" />
              <path d="M16 2v4M8 2v4M3 10h18" />
            </svg>
            <span>{range.from} to {range.to}</span>
          </div>
        </div>

        {mode === 'custom' && (
          <div className="time-form-grid">
            <label>
              From
              <input
                type="date"
                value={custom.from}
                onChange={(e) => {
                  setCustom({ ...custom, from: e.target.value });
                  setPage(1);
                }}
              />
            </label>
            <label>
              To
              <input
                type="date"
                value={custom.to}
                onChange={(e) => {
                  setCustom({ ...custom, to: e.target.value });
                  setPage(1);
                }}
              />
            </label>
          </div>
        )}

        <div className="time-filters">
          <label>
            Project
            <select
              aria-label="Project"
              value={filters.projectId || ''}
              onChange={(e) => filter('projectId', e.target.value)}
            >
              <option value="">All projects</option>
              {projects.data?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.projectName || p.key}
                </option>
              ))}
            </select>
          </label>
          <label>
            Issue ID
            <input
              type="number"
              min="1"
              placeholder="e.g. 104"
              value={filters.issueId || ''}
              onChange={(e) => filter('issueId', e.target.value)}
            />
          </label>
          {team && (
            <label>
              User ID
              <input
                type="number"
                min="1"
                placeholder="e.g. 8"
                value={filters.userId || ''}
                onChange={(e) => filter('userId', e.target.value)}
              />
            </label>
          )}
          <label>
            Billable
            <select
              value={filters.billable ?? ''}
              onChange={(e) => filter('billable', e.target.value)}
            >
              <option value="">All (Billable & Non-billable)</option>
              <option value="true">Billable</option>
              <option value="false">Non-billable</option>
            </select>
          </label>
          <label>
            Status
            <select value={filters.status || ''} onChange={(e) => filter('status', e.target.value)}>
              <option value="">All statuses</option>
              {['DRAFT', 'SUBMITTED', 'APPROVED', 'REJECTED'].map((s) => (
                <option key={s} value={s}>{s}</option>
              ))}
            </select>
          </label>
        </div>

        {Boolean(filters.projectId || filters.issueId || filters.userId || (filters.billable !== undefined && filters.billable !== '') || filters.status) && (
          <div className="time-filters-active-bar">
            <span className="filters-count-tag">
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true">
                <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3" />
              </svg>
              Filters active
            </span>
            <button
              type="button"
              className="time-clear-filters-btn"
              onClick={() => {
                setFilters(team && /^[1-9]\d*$/.test(params.get('userId') || '') ? { userId: params.get('userId') } : {});
                setPage(1);
              }}
            >
              Clear filters ✕
            </button>
          </div>
        )}
      </div>

      {add && <WorkLogForm onClose={() => setAdd(false)} />}

      <QueryState query={query}>
        {data && (
          <>
            {/* Timesheet Anomaly Detector Warning Banner */}
            {anomalies.length > 0 && (
              <div
                style={{
                  margin: '0 0 16px 0',
                  padding: '12px 16px',
                  borderRadius: '8px',
                  background: '#fef2f2',
                  border: '1px solid #fee2e2',
                  color: '#991b1b',
                  fontSize: '0.86rem',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px', fontWeight: 600 }}>
                  <span>⚠️</span>
                  <span>Timesheet Anomalies Flagged ({anomalies.length}):</span>
                </div>
                <ul style={{ margin: 0, paddingLeft: '24px' }}>
                  {anomalies.map((anom, idx) => (
                    <li key={idx} style={{ marginTop: '2px' }}>
                      {anom.message}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {/* KPI Metric Cards (Moved to the top) */}
            <div className="time-totals">
              <section className="panel time-stat-card accent-total">
                <div className="time-stat-header">
                  <div className="time-stat-icon-pill icon-total">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
                  </div>
                  <span>Total hours</span>
                  <span className="time-stat-chip chip-amber">{progressPercent}% goal</span>
                </div>
                <strong className="time-stat-value">{durationLabel(data.totalMinutes)}</strong>
                <div className="time-stat-bar-track">
                  <div className="time-stat-bar-fill bar-amber" style={{ width: `${progressPercent}%` }} />
                </div>
                <small className="time-stat-note">{durationLabel(data.totalMinutes)} of 40h standard week</small>
              </section>

              <section className="panel time-stat-card accent-billable">
                <div className="time-stat-header">
                  <div className="time-stat-icon-pill icon-billable">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
                  </div>
                  <span>Billable hours</span>
                  <span className="time-stat-chip chip-emerald">{billablePercent}% ratio</span>
                </div>
                <strong className="time-stat-value">{durationLabel(data.billableMinutes)}</strong>
                <div className="time-stat-bar-track">
                  <div className="time-stat-bar-fill bar-emerald" style={{ width: `${billablePercent}%` }} />
                </div>
                <small className="time-stat-note">Client billable work</small>
              </section>

              <section className="panel time-stat-card accent-non-billable">
                <div className="time-stat-header">
                  <div className="time-stat-icon-pill icon-non-billable">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><rect x="2" y="7" width="20" height="14" rx="2" ry="2"/><path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"/></svg>
                  </div>
                  <span>Non-billable hours</span>
                  <span className="time-stat-chip chip-slate">Internal</span>
                </div>
                <strong className="time-stat-value">{durationLabel(data.nonBillableMinutes)}</strong>
                <div className="time-stat-bar-track">
                  <div className="time-stat-bar-fill bar-slate" style={{ width: `${100 - billablePercent}%` }} />
                </div>
                <small className="time-stat-note">Internal & operational activities</small>
              </section>
            </div>

            {/* Daily Breakdown Strip */}
            <div className="time-days">
              {(mode === 'week'
                ? Array.from({ length: 7 }, (_, i) => {
                    const d = new Date(range.from + 'T12:00:00');
                    d.setDate(d.getDate() + i);
                    return {
                      date: dateText(d),
                      minutes: data.dailyTotals.find((x) => x.date === dateText(d))?.minutes || 0,
                    };
                  })
                : data.dailyTotals
              ).map((d) => {
                const dayObj = new Date(d.date + 'T12:00:00');
                const dayNum = dayObj.getDay();
                const isWeekend = dayNum === 0 || dayNum === 6;
                const todayStr = dateText(new Date());
                const isToday = d.date === todayStr;
                const hasTime = d.minutes > 0;
                const dayCapPct = Math.min(100, Math.round((d.minutes / 480) * 100)); // 8h target

                return (
                  <div
                    key={d.date}
                    className={`time-day-card ${isWeekend ? 'is-weekend' : ''} ${isToday ? 'is-today' : ''} ${hasTime ? 'has-time' : ''}`}
                  >
                    <div className="time-day-header">
                      <div className="time-day-label-row">
                        <small className="time-day-name">
                          {dayObj.toLocaleDateString(undefined, { weekday: 'short' })}
                        </small>
                        {isToday && <span className="time-today-tag">Today</span>}
                      </div>
                      <span className="time-day-date">
                        {dayObj.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}
                      </span>
                    </div>
                    <strong className="time-day-value">
                      {d.minutes > 0 ? durationLabel(d.minutes) : '0h'}
                    </strong>
                    <div className="time-day-progress" title={`${durationLabel(d.minutes)} of 8h standard day`}>
                      <div className="time-day-progress-bar" style={{ width: `${dayCapPct}%` }} />
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Unified Main Workspace Card with Segmented Switcher */}
            <section className="panel time-section time-main-card">
              <div className="time-main-header-row">
                <div className="time-main-title">
                  <h2>
                    {viewLayout === 'table'
                      ? 'Time by user & project'
                      : viewLayout === 'hierarchy'
                      ? 'Project & task breakdown'
                      : 'Weekly calendar view'}
                  </h2>
                  <p className="time-main-desc">
                    {viewLayout === 'table'
                      ? `Showing ${data.workLogs.items.length} work log entries for this period.`
                      : viewLayout === 'hierarchy'
                      ? 'Grouped rollup: Project → Epic → Story → Task → Subtask.'
                      : 'Schedule and timeline distribution.'}
                  </p>
                </div>

                <div className="view-switch time-segmented-tabs" aria-label="Timesheet layout">
                  <button
                    type="button"
                    className={viewLayout === 'table' ? 'is-active' : ''}
                    aria-pressed={viewLayout === 'table'}
                    onClick={() => setViewLayout('table')}
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                      <line x1="8" y1="6" x2="21" y2="6" />
                      <line x1="8" y1="12" x2="21" y2="12" />
                      <line x1="8" y1="18" x2="21" y2="18" />
                      <line x1="3" y1="6" x2="3.01" y2="6" />
                      <line x1="3" y1="12" x2="3.01" y2="12" />
                      <line x1="3" y1="18" x2="3.01" y2="18" />
                    </svg>
                    Table view
                  </button>
                  <button
                    type="button"
                    className={viewLayout === 'hierarchy' ? 'is-active' : ''}
                    aria-pressed={viewLayout === 'hierarchy'}
                    onClick={() => setViewLayout('hierarchy')}
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                      <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                      <circle cx="9" cy="7" r="4" />
                      <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
                      <path d="M16 3.13a4 4 0 0 1 0 7.75" />
                    </svg>
                    Hierarchy view
                  </button>
                  <button
                    type="button"
                    className={viewLayout === 'calendar' ? 'is-active' : ''}
                    aria-pressed={viewLayout === 'calendar'}
                    onClick={() => setViewLayout('calendar')}
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                      <rect x="3" y="4" width="18" height="18" rx="2" />
                      <path d="M16 2v4M8 2v4M3 10h18" />
                    </svg>
                    Weekly calendar view
                  </button>
                </div>
              </div>

              {viewLayout === 'calendar' ? (
                <TimesheetCalendar
                  items={data.workLogs.items}
                  anchor={anchor}
                  onAnchorChange={(date) => { setAnchor(date); setPage(1); }}
                  canCreate={can('WORKLOG_CREATE')}
                  projects={projects.data}
                />
              ) : viewLayout === 'hierarchy' ? (
                <div className="time-hierarchy-embedded">
                  <TimesheetHierarchy
                    key={JSON.stringify([team, range, filters])}
                    team={team}
                    filters={{ ...range, ...filters }}
                  />
                </div>
              ) : (
                <>
                  <WorkLogTable items={data.workLogs.items} />
                  {data.workLogs.totalPages > 1 && (
                    <Pagination data={data.workLogs} onPage={setPage} busy={query.isFetching} />
                  )}
                </>
              )}
            </section>

            {/* Weekly Reviews / Submissions */}
            <section className="panel time-section time-submissions-card">
              <div className="time-section-header">
                <div className="time-submissions-title-row">
                  <span className="time-submissions-icon">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><rect x="3" y="4" width="18" height="18" rx="2"/><path d="M16 2v4M8 2v4M3 10h18"/></svg>
                  </span>
                  <div>
                    <h2>{team ? 'Weekly reviews' : 'Weekly submissions'}</h2>
                    <p className="time-section-subtext">
                      Submission and review always apply to the full Monday–Sunday period, regardless of the filters above.
                    </p>
                  </div>
                </div>
              </div>

              {!data.periods.length && (
                <div className="submissions-empty-card">
                  <div className="submissions-empty-icon" aria-hidden="true">
                    <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                      <rect x="3" y="4" width="18" height="18" rx="2" />
                      <path d="M16 2v4M8 2v4M3 10h18" />
                    </svg>
                  </div>
                  <p>No periods to show. Log work to start a timesheet.</p>
                  <small>Submission and review apply to full Monday–Sunday periods.</small>
                </div>
              )}

              {data.periods.map((p) => (
                <article className="time-period" key={p.id}>
                  <div className="time-period-info">
                    <div className="time-period-user">
                      <UserIdentity id={p.userId} />
                    </div>
                    <div className="time-period-meta">
                      <strong className="time-period-dates">
                        {p.periodStart} — {p.periodEnd}
                      </strong>
                      <span className={`time-status status-${p.status?.toLowerCase()}`}>{p.status}</span>
                      <div className="time-period-hours-chips">
                        <span className="hours-chip chip-total">Total: {durationLabel(p.totalMinutes)}</span>
                        <span className="hours-chip chip-billable">Billable: {durationLabel(p.billableMinutes || 0)}</span>
                        <span className="hours-chip chip-non-billable">Non-billable: {durationLabel(p.totalMinutes - (p.billableMinutes || 0))}</span>
                      </div>
                    </div>
                    {p.projects?.length > 0 && (
                      <div className="time-period-projects">
                        {p.projects.map((project) => (
                          <span className="project-time-pill" key={project.projectId}>
                            {project.projectName} · {durationLabel(project.minutes)}
                          </span>
                        ))}
                      </div>
                    )}
                    {p.rejectionReason && <p className="rejection-notice">Reason: {p.rejectionReason}</p>}
                  </div>
                  <PeriodActions period={p} />
                </article>
              ))}

              {data.periodTotalCount > 25 && (
                <Pagination
                  data={{
                    page,
                    totalPages: Math.ceil(data.periodTotalCount / 25),
                    totalCount: data.periodTotalCount,
                    pageSize: 25,
                  }}
                  onPage={setPage}
                  busy={query.isFetching}
                />
              )}
            </section>

            {team && (
              <section className="panel time-section team-totals-section">
                <div className="time-section-header">
                  <div className="time-submissions-title-row">
                    <span className="time-submissions-icon">
                      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                        <circle cx="9" cy="7" r="4" />
                        <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
                        <path d="M16 3.13a4 4 0 0 1 0 7.75" />
                      </svg>
                    </span>
                    <div>
                      <h2>Team totals</h2>
                      <p className="time-section-subtext">
                        Aggregated hours logged per team member and project distribution for this period.
                      </p>
                    </div>
                  </div>
                </div>

                <div className="time-table-scroll team-totals-table-wrap">
                  <table className="time-table team-totals-table">
                    <thead>
                      <tr>
                        <th>User</th>
                        <th>Total hours</th>
                        <th>Billable hours</th>
                        <th>Non-billable hours</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.userTotals.map((t) => (
                        <tr key={t.id}>
                          <td className="team-user-cell">
                            <UserIdentity id={t.id} />
                          </td>
                          <td>
                            <span className="hours-chip chip-total">{durationLabel(t.minutes)}</span>
                          </td>
                          <td>
                            <span className="hours-chip chip-billable">{durationLabel(t.billableMinutes)}</span>
                          </td>
                          <td>
                            <span className="hours-chip chip-non-billable">{durationLabel(t.minutes - t.billableMinutes)}</span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <div className="team-projects-breakdown">
                  <div className="team-projects-title-row">
                    <span className="team-projects-icon">
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <polygon points="12 2 2 7 12 12 22 7 12 2" />
                        <polyline points="2 17 12 22 22 17" />
                        <polyline points="2 12 12 17 22 12" />
                      </svg>
                    </span>
                    <h3>By project</h3>
                  </div>

                  <div className="team-project-grid">
                    {data.projectTotals.map((t) => {
                      const projName = projects.data?.find((p) => p.id === t.id)?.projectName || `Project #${t.id}`;
                      const sharePercent = totalWeeklyMinutes > 0
                        ? Math.min(100, Math.round((t.minutes / totalWeeklyMinutes) * 100))
                        : 0;
                      return (
                        <div className="team-project-card" key={t.id}>
                          <div className="team-project-card-header">
                            <span className="project-bullet" />
                            <p className="team-project-label">
                              {projName} · {durationLabel(t.minutes)}
                            </p>
                            <span className="team-project-badge">{sharePercent}%</span>
                          </div>
                          <div className="team-project-track">
                            <div className="team-project-fill" style={{ width: `${sharePercent}%` }} />
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              </section>
            )}
          </>
        )}
      </QueryState>
    </div>
  );
}
