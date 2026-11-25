import { useState } from 'react';
import Link from '../../../src/components/PortalLink';
import { PageHeader } from '../../../src/components/common';
import { useTimesheetQuery } from '../../../src/services/api/timesheets';
import { liveQueryOptions } from '../../../src/services/api/live';
import WorkLogForm from '../../../src/features/timesheets/WorkLogForm';
import WorkLogTable from '../../../src/features/timesheets/WorkLogTable';
import { dateText, dateRange } from '../../../src/features/timesheets/time';
import '../../../src/styles/timesheets.scss';

export default function LogWorkPage() {
  const [today] = useState(dateText(new Date()));
  const [successNotice, setSuccessNotice] = useState(false);
  const range = dateRange(today, 'week');
  const query = useTimesheetQuery({ team: false, ...range, page: 1, pageSize: 25 }, liveQueryOptions);

  function handleSaved() {
    setSuccessNotice(true);
    setTimeout(() => setSuccessNotice(false), 5000);
  }

  const totalMinutes = query.data?.totalMinutes || 0;
  const billableMinutes = query.data?.billableMinutes || 0;
  const nonBillableMinutes = query.data?.nonBillableMinutes || 0;
  const weeklyTargetHours = 40;
  const loggedHours = totalMinutes / 60;
  const completionPercent = Math.min(Math.round((loggedHours / weeklyTargetHours) * 100), 100);
  const todayLogs = query.data?.workLogs?.items?.filter((item) => item.workDate === today) || [];

  const formatHoursH = (mins) => `${(mins / 60).toFixed(1)}h`;

  return (
    <div className="timesheet-workspace logwork-screen">
      <PageHeader
        title="Log work"
        description="Record your focused effort against projects and assigned issues."
      >
        <Link className="button" to="/timesheets/my">
          My timesheet →
        </Link>
        <Link className="button" to="/timesheets/history">
          Timesheet history →
        </Link>
      </PageHeader>

      {successNotice && (
        <div className="logwork-success-banner" role="status">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <polyline points="20 6 9 17 4 12" />
          </svg>
          <span>Work log saved successfully!</span>
        </div>
      )}

      <div className="logwork-layout-grid">
        <div className="logwork-main-column">
          <WorkLogForm onSaved={handleSaved} initialDate={today} />
        </div>

        <div className="logwork-aside-column">
          <section className="panel logwork-target-card">
            <div className="target-card-top">
              <div className="target-logged-block">
                <span className="target-label">Total Logged</span>
                <strong className="target-total-val">{formatHoursH(totalMinutes)}</strong>
              </div>
              <div className="target-badge-box">
                <span className="target-badge-title">Weekly Target</span>
                <strong className="target-badge-val">{weeklyTargetHours}h</strong>
              </div>
            </div>

            <div className="target-progress-bar-wrap" role="progressbar" aria-valuenow={completionPercent} aria-valuemin="0" aria-valuemax="100">
              <div className="target-progress-track">
                <div
                  className="target-progress-fill"
                  style={{ width: `${Math.max(completionPercent, 4)}%` }}
                />
                <div className="target-progress-dot" />
              </div>
            </div>

            <div className="target-card-metrics">
              <div className="metric-col">
                <span className="metric-title">Billable</span>
                <strong className="metric-val text-green">{formatHoursH(billableMinutes)}</strong>
              </div>
              <div className="metric-col">
                <span className="metric-title">Non-Billable</span>
                <strong className="metric-val">{formatHoursH(nonBillableMinutes)}</strong>
              </div>
              <div className="metric-col">
                <span className="metric-title">Completion</span>
                <strong className="metric-val">{completionPercent}%</strong>
              </div>
            </div>
          </section>

          <section className="panel logwork-summary-panel">
            <div className="logwork-recent-section">
              <h3>Today's logs</h3>
              {todayLogs.length === 0 ? (
                <p className="hint">No work logged yet for today ({today}).</p>
              ) : (
                <WorkLogTable items={todayLogs} />
              )}
            </div>
          </section>
        </div>
      </div>
    </div>
  );
}
