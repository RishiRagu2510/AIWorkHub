import { useState } from 'react';
import Link from '../../../src/components/PortalLink';
import { QueryState, Pagination } from '../../../src/components/common';
import { useAuth } from '../../../src/features/auth/context';
import { useTimesheetQuery } from '../../../src/services/api/timesheets';
import { liveQueryOptions } from '../../../src/services/api/live';
import PeriodActions from '../../../src/features/timesheets/PeriodActions';
import TimesheetHierarchy from '../../../src/features/timesheets/TimesheetHierarchy';
import { durationLabel, dateRange, dateText, moveDate } from '../../../src/features/timesheets/time';
import '../../../src/styles/timesheets.scss';

export default function TimesheetHistory() {
  const { can } = useAuth();
  const [anchor, setAnchor] = useState(dateText(new Date()));
  const [mode, setMode] = useState('month');
  const [custom, setCustom] = useState(dateRange(anchor));
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);

  const range = mode === 'custom' ? custom : dateRange(anchor, mode);
  const query = useTimesheetQuery(
    { team: false, ...range, ...(statusFilter ? { status: statusFilter } : {}), page, pageSize: 25 },
    liveQueryOptions,
  );

  function move(n) {
    setAnchor(moveDate(anchor, mode, n));
    setPage(1);
  }

  const data = query.data;
  const periods = data?.periods || [];

  const approvedCount = periods.filter((p) => p.status === 'APPROVED').length;
  const submittedCount = periods.filter((p) => p.status === 'SUBMITTED').length;
  const rejectedCount = periods.filter((p) => p.status === 'REJECTED').length;
  const draftCount = periods.filter((p) => p.status === 'DRAFT').length;

  return (
    <div className="timesheet-workspace history-screen">
      {/* Top Header with Integrated Nav and Single Primary Action */}
      <header className="timesheet-header-banner">
        <div className="timesheet-header-main">
          <div className="timesheet-badge-tag">
            <span className="pulse-dot" />
            <span>Chronos Time Tracker</span>
          </div>
          <h1 className="timesheet-title">Timesheet history</h1>
          <p className="timesheet-subtitle">
            Review past weekly periods, submission history, approval statuses, and manager feedback.
          </p>
        </div>

        <div className="timesheet-header-actions">
          <nav className="timesheet-nav-pills" role="tablist" aria-label="Timesheet screens">
            <Link className="timesheet-nav-pill" to="/timesheets/my">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <circle cx="12" cy="12" r="9" />
                <path d="M12 6v6l4 2" />
              </svg>
              My timesheet
            </Link>
            <Link className="timesheet-nav-pill is-active" to="/timesheets/history">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
                <path d="M3 3v5h5" />
                <polyline points="12 7 12 12 15 15" />
              </svg>
              Timesheet history
            </Link>
            {can('TIMESHEET_TEAM_VIEW') && (
              <Link className="timesheet-nav-pill" to="/timesheets/team">
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

      {/* History Metrics */}
      <div className="time-totals history-totals">
        <section className="panel time-stat-card accent-billable">
          <div className="time-stat-header">
            <div className="time-stat-icon-pill icon-billable">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
                <polyline points="22 4 12 14.01 9 11.01" />
              </svg>
            </div>
            <span>Approved periods</span>
            <span className="time-stat-chip chip-emerald">Final</span>
          </div>
          <strong className="time-stat-value">{approvedCount}</strong>
          <small className="time-stat-note">Finalized & locked for payroll</small>
        </section>

        <section className="panel time-stat-card accent-total">
          <div className="time-stat-header">
            <div className="time-stat-icon-pill icon-total">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <circle cx="12" cy="12" r="10" />
                <polyline points="12 6 12 12 16 14" />
              </svg>
            </div>
            <span>Pending review</span>
            <span className="time-stat-chip chip-amber">In review</span>
          </div>
          <strong className="time-stat-value">{submittedCount}</strong>
          <small className="time-stat-note">Submitted to managers for approval</small>
        </section>

        <section className="panel time-stat-card accent-non-billable">
          <div className="time-stat-header">
            <div className="time-stat-icon-pill icon-non-billable">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
              </svg>
            </div>
            <span>Draft / Rejected</span>
            <span className="time-stat-chip chip-slate">Action needed</span>
          </div>
          <strong className="time-stat-value">{draftCount + rejectedCount}</strong>
          <small className="time-stat-note">
            {draftCount} in draft{rejectedCount > 0 ? ` · ${rejectedCount} returned with feedback` : ''}
          </small>
        </section>
      </div>

      {/* Toolbar & Filters */}
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
                setMode('month');
                setPage(1);
              }}
            >
              Current {mode}
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
              aria-label="History period range"
              value={mode}
              onChange={(e) => {
                setMode(e.target.value);
                setPage(1);
              }}
            >
              <option value="month">Monthly</option>
              <option value="week">Weekly</option>
              <option value="custom">Custom range</option>
            </select>
            <button
              type="button"
              className="button timesheet-export-btn"
              title="Export timesheet records to CSV for payroll and billing"
              onClick={() => {
                const csvHeaders = ['Week Starting', 'Week Ending', 'Status', 'Total Hours', 'Billable Hours', 'Non-Billable Hours', 'Projects', 'Manager Feedback'];
                const csvRows = periods.map((p) => [
                  `"${p.startDate || ''}"`,
                  `"${p.endDate || ''}"`,
                  `"${p.status || ''}"`,
                  `"${((p.totalMinutes || 0) / 60).toFixed(2)}"`,
                  `"${((p.billableMinutes || 0) / 60).toFixed(2)}"`,
                  `"${(((p.totalMinutes || 0) - (p.billableMinutes || 0)) / 60).toFixed(2)}"`,
                  `"${(p.projects || []).map((proj) => `${proj.projectName} (${(proj.minutes / 60).toFixed(1)}h)`).join('; ')}"`,
                  `"${(p.rejectionReason || '').replace(/"/g, '""')}"`,
                ]);
                const content = '\uFEFF' + [csvHeaders.join(','), ...csvRows.map((r) => r.join(','))].join('\r\n');
                const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = `Timesheet_History_${range.from}_to_${range.to}.csv`;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
              }}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                <polyline points="7 10 12 15 17 10" />
                <line x1="12" y1="15" x2="12" y2="3" />
              </svg>
              Export Payroll CSV
            </button>
          </div>

          <div className="time-date-range-badge">
            <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
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
            Status
            <select value={statusFilter} onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}>
              <option value="">All statuses</option>
              {['DRAFT', 'SUBMITTED', 'APPROVED', 'REJECTED'].map((s) => (
                <option key={s} value={s}>{s}</option>
              ))}
            </select>
          </label>
        </div>

        {Boolean(statusFilter) && (
          <div className="time-filters-active-bar">
            <span className="filters-count-tag">
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true">
                <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3" />
              </svg>
              Filter active
            </span>
            <button
              type="button"
              className="time-clear-filters-btn"
              onClick={() => {
                setStatusFilter('');
                setPage(1);
              }}
            >
              Clear filter ✕
            </button>
          </div>
        )}
      </div>

      {/* Periods List */}
      <QueryState query={query}>
        <section className="panel time-section history-periods-section">
          <div className="time-section-header">
            <div className="time-submissions-title-row">
              <span className="time-submissions-icon">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
                  <path d="M3 3v5h5" />
                  <polyline points="12 7 12 12 15 15" />
                </svg>
              </span>
              <div>
                <h2>Weekly submission records</h2>
                <p className="time-section-subtext">
                  Timesheet submissions apply to the full Monday–Sunday weekly cycle.
                </p>
              </div>
            </div>
          </div>

          {!periods.length ? (
            <div className="submissions-empty-card">
              <div className="submissions-empty-icon" aria-hidden="true">
                <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="4" width="18" height="18" rx="2" />
                  <path d="M16 2v4M8 2v4M3 10h18" />
                </svg>
              </div>
              <p>No timesheet records found for this period.</p>
              <small>Try selecting a different date range or status filter.</small>
            </div>
          ) : (
            <div className="history-period-cards">
              {periods.map((p) => {
                const statusTone =
                  p.status === 'APPROVED' ? 'status-approved' :
                  p.status === 'SUBMITTED' ? 'status-submitted' :
                  p.status === 'REJECTED' ? 'status-rejected' : 'status-draft';

                return (
                  <article className={`time-period history-period-card ${statusTone}`} key={p.id}>
                    <div className="period-main-info">
                      <div className="period-header-row">
                        <span className="period-calendar-icon" aria-hidden="true">
                          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <rect x="3" y="4" width="18" height="18" rx="2" />
                            <path d="M16 2v4M8 2v4M3 10h18" />
                          </svg>
                        </span>
                        <strong className="period-range-title">
                          {p.periodStart} — {p.periodEnd}
                        </strong>
                        <span className={`time-status badge-${p.status?.toLowerCase()}`}>
                          {p.status}
                        </span>
                      </div>

                      <div className="period-meta-row">
                        <span className="hours-chip chip-total">
                          <strong>Total:</strong> {durationLabel(p.totalMinutes)}
                        </span>
                        <span className="hours-chip chip-billable">
                          <strong>Billable:</strong> {durationLabel(p.billableMinutes || 0)}
                        </span>
                        <span className="hours-chip chip-non-billable">
                          <strong>Non-billable:</strong> {durationLabel(p.totalMinutes - (p.billableMinutes || 0))}
                        </span>
                      </div>

                      {p.projects?.length > 0 && (
                        <div className="period-projects-list">
                          {p.projects.map((project) => (
                            <span className="period-project-chip" key={project.projectId}>
                              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
                              </svg>
                              {project.projectName} · {durationLabel(project.minutes)}
                            </span>
                          ))}
                        </div>
                      )}

                      {p.rejectionReason && (
                        <div className="period-rejection-note">
                          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <circle cx="12" cy="12" r="10" />
                            <line x1="12" y1="8" x2="12" y2="12" />
                            <line x1="12" y1="16" x2="12.01" y2="16" />
                          </svg>
                          <div>
                            <strong>Manager feedback:</strong> {p.rejectionReason}
                          </div>
                        </div>
                      )}
                    </div>

                    <div className="period-card-actions">
                      <PeriodActions period={p} />
                    </div>
                  </article>
                );
              })}
            </div>
          )}

          {data?.periodTotalCount > 25 && (
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
      </QueryState>

      <TimesheetHierarchy
        key={JSON.stringify([range, statusFilter])}
        filters={{ ...range, ...(statusFilter ? { status: statusFilter } : {}) }}
      />
    </div>
  );
}
