import Allocation from './Allocation';
import { useState } from 'react';
import Link from '../../components/PortalLink';
import { useAuth } from '../auth/context';
import {
  useEmployeeProjectsQuery,
  useEmployeeTimeQuery,
  useEmployeeSectionQuery,
  useEmployeeChartQuery,
} from '../../services/api/employees';
import { useSearchIssuesQuery } from '../../services/api/api';
import { QueryState } from '../../components/common';

export function currentWeek() {
  const now = new Date();
  const from = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  from.setDate(from.getDate() - ((from.getDay() + 6) % 7));
  const to = new Date(from);
  to.setDate(to.getDate() + 6);
  const date = (d) =>
    [
      d.getFullYear(),
      String(d.getMonth() + 1).padStart(2, '0'),
      String(d.getDate()).padStart(2, '0'),
    ].join('-');
  return { from: date(from), to: date(to) };
}
export const proficiency = (value) =>
  ['', 'Beginner', 'Basic', 'Intermediate', 'Advanced', 'Expert'][value] || 'Not specified';
export function Organization({ employee, manager, reference }) {
  const [page, setPage] = useState(1);
  const reports = useEmployeeChartQuery({ managerId: employee.id, page });
  return (
    <section className="employee-panel profile-organization">
      <h2>Organization</h2>
      <div className="profile-org-node profile-org-manager">
        <small>Manager</small>
        {manager}
      </div>
      <span className="profile-org-arrow" aria-hidden="true">↓</span>
      <div className="profile-org-node profile-org-current">
        <strong>
          {employee.firstName} {employee.lastName}
        </strong>
        <span>
          {[reference('departmentId'), reference('disciplineId')]
            .filter((v) => v && v !== '—')
            .join(' · ') || '—'}
        </span>
      </div>
      <span className="profile-org-arrow" aria-hidden="true">↓</span>
      <h3>Direct reports</h3>
      <QueryState query={reports}>
        <div className="profile-report-grid">
          {reports.data?.items?.map((r) => (
            <Link className="profile-org-node" key={r.id} to={`/employees/${r.id}`}>
              {r.firstName} {r.lastName}
            </Link>
          ))}
        </div>
        {!reports.data?.items?.length && (
          <p className="profile-org-empty">No direct reports.</p>
        )}
        <div className="actions">
          {page > 1 && <button onClick={() => setPage(page - 1)}>Previous reports</button>}
          {page * 50 < reports.data?.totalCount && (
            <button onClick={() => setPage(page + 1)}>More reports</button>
          )}
        </div>
      </QueryState>
      <Link className="profile-org-link" to="/employees/org-chart">
        View Full Organization Chart →
      </Link>
    </section>
  );
}
function Stat({ title, icon, subtitle, query, children, allowed = true, unavailableLabel = 'Private', className = '' }) {
  return (
    <article className={`profile-stat ${className}`}>
      <div className="stat-header">
        <span className="stat-title">{title}</span>
        {icon && <span className="stat-icon-wrapper" aria-hidden="true">{icon}</span>}
      </div>
      <div className="stat-body">
        {allowed ? (
          <QueryState query={query}>
            <strong>{children}</strong>
          </QueryState>
        ) : (
          <strong className="stat-unavailable">{unavailableLabel}</strong>
        )}
      </div>
      {subtitle && <span className="stat-subtitle">{subtitle}</span>}
    </article>
  );
}
export function Overview({ employee, personal, job, viewCareer }) {
  const { can, session } = useAuth();
  const own = String(employee.userId) === String(session.user.id);
  const showDeveloperSummary = employee.roles?.includes('DEVELOPER') === true;
  const skills = useEmployeeSectionQuery({ id: employee.id, section: 'skills' }, { skip: !viewCareer });
  const week = currentWeek();
  const days = Array.from({ length: 7 }, (_, index) => { const date = new Date(week.from + 'T00:00:00'); date.setDate(date.getDate() + index); return { date: [date.getFullYear(), String(date.getMonth()+1).padStart(2,'0'), String(date.getDate()).padStart(2,'0')].join('-'), label: date.toLocaleDateString(undefined, {weekday:'short'}) }; });
  const projects = useEmployeeProjectsQuery(employee.userId, { skip: !showDeveloperSummary || !can('PROJECT_VIEW') });
  const timeAllowed = can('TIMESHEET_VIEW') && (own || can('TIMESHEET_TEAM_VIEW'));
  const report = useEmployeeTimeQuery(
    { userId: employee.userId, ...currentWeek(), team: !own },
    { skip: !timeAllowed },
  );
  const params = { AssigneeUserId: employee.userId, Page: 1, PageSize: 1 };
  const opts = { skip: !showDeveloperSummary || !can('ISSUE_VIEW') };
  const overdue = useSearchIssuesQuery({ ...params, WorkGroup: 'OVERDUE' }, opts);
  const soon = useSearchIssuesQuery({ ...params, WorkGroup: 'DUE_SOON' }, opts);
  const progress = useSearchIssuesQuery({ ...params, WorkGroup: 'IN_PROGRESS' }, opts);
  const other = useSearchIssuesQuery({ ...params, WorkGroup: 'OTHER' }, opts);
  const groups = [overdue, soon, progress, other];
  const open = {
    isLoading: groups.some((x) => x.isLoading),
    error: groups.find((x) => x.error)?.error,
  };
  return (
    <div className="employee-panel">
      {showDeveloperSummary && <div className="profile-stats">
        <Stat
          title="Active projects"
          subtitle="Assigned software projects"
          icon={
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
            </svg>
          }
          query={projects}
          allowed={can('PROJECT_VIEW')}
        >
          {projects.data?.filter((p) => p.status === 0 || p.status === 'ACTIVE').length ?? '—'}
        </Stat>
        <Stat
          title="Open issues"
          subtitle="Backlog & in-progress tasks"
          icon={
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10" />
              <line x1="12" y1="8" x2="12" y2="12" />
              <line x1="12" y1="16" x2="12.01" y2="16" />
            </svg>
          }
          query={open}
          allowed={can('ISSUE_VIEW')}
        >
          {groups.every((x) => x.data)
            ? groups.reduce((n, x) => n + (x.data.totalCount ?? 0), 0)
            : '—'}
        </Stat>
        <Stat
          title="Logged hours · current week"
          subtitle="Monday to Sunday logged time"
          icon={
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10" />
              <polyline points="12 6 12 12 16 14" />
            </svg>
          }
          query={report}
          allowed={timeAllowed}
        >
          {report.data ? (report.data.totalMinutes / 60).toFixed(2) : '—'}
        </Stat>
        <Allocation employee={employee} />
      </div>}
      <div className="persona-overview-grid">
        <div className="persona-overview-column">
          <section className="persona-record-card">
            <header>
              <div className="persona-card-title">
                <svg className="persona-card-icon persona-icon-personal" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                  <circle cx="12" cy="7" r="4" />
                </svg>
                <h2>Personal summary</h2>
              </div>
              <span className="persona-badge hr-badge">HR Core Records</span>
            </header>
            {personal}
          </section>
          {viewCareer && (
            <section className="persona-record-card">
              <header>
                <div className="persona-card-title">
                  <svg className="persona-card-icon persona-icon-tech" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <polyline points="16 18 22 12 16 6" />
                    <polyline points="8 6 2 12 8 18" />
                  </svg>
                  <h2>Technical Skills &amp; Stack</h2>
                </div>
                <span className="persona-badge tech-badge">Competencies</span>
              </header>
              <div className="persona-card-body">
                <QueryState query={skills}>
                  {skills.data?.length ? (
                    <div className="persona-skills-list">
                      {skills.data.map((skill) => {
                        const profLevel = skill.proficiency || 0;
                        const profName = proficiency(profLevel);
                        const percent = Math.min(100, Math.max(0, (profLevel / 5) * 100));
                        return (
                          <div className="persona-skill-item" key={skill.id}>
                            <div className="persona-skill-header">
                              <div className="persona-skill-name-wrap">
                                <span className="persona-skill-dot" />
                                <span className="persona-skill-name">{skill.skillName}</span>
                              </div>
                              <span className={`persona-proficiency-badge level-${profLevel}`}>
                                {profName}
                              </span>
                            </div>
                            <div className="persona-progress-wrapper">
                              <div className="persona-progress-track">
                                <div
                                  className={`persona-progress-fill level-${profLevel}`}
                                  style={{ width: `${percent}%` }}
                                />
                              </div>
                            </div>
                            <progress
                              className="sr-only"
                              aria-label={`${skill.skillName} proficiency`}
                              max="5"
                              value={profLevel}
                            />
                          </div>
                        );
                      })}
                    </div>
                  ) : (
                    <div className="persona-empty-state">
                      <p>No skills recorded yet.</p>
                    </div>
                  )}
                </QueryState>
              </div>
            </section>
          )}
        </div>
        <div className="persona-overview-column">
          <section className="persona-record-card">
            <header>
              <div className="persona-card-title">
                <svg className="persona-card-icon persona-icon-job" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="2" y="7" width="20" height="14" rx="2" ry="2" />
                  <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16" />
                </svg>
                <h2>Job summary</h2>
              </div>
              <span className="persona-badge job-badge">Position Architecture</span>
            </header>
            {job}
          </section>
          <section className="persona-record-card">
            <header>
              <div className="persona-card-title">
                <svg className="persona-card-icon persona-icon-time" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="10" />
                  <polyline points="12 6 12 12 16 14" />
                </svg>
                <h2>Weekly contribution &amp; logged hours</h2>
              </div>
              <span className="persona-badge time-badge">Current Week</span>
            </header>
            <div className="persona-card-body">
              {timeAllowed ? (
                <QueryState query={report}>
                  <div className="persona-hours-total-card">
                    <span className="persona-hours-total-label">Daily work hours for the current week</span>
                    <div className="persona-hours-total-value">
                      <strong>{report.data ? (report.data.totalMinutes / 60).toFixed(1) : '—'}</strong>
                      <span>hours total</span>
                    </div>
                  </div>
                  {report.data?.dailyTotals ? (
                    <div className="persona-hours-chart" aria-label="Daily logged hours">
                      {days.map((day) => {
                        const minutes = report.data.dailyTotals.find((total) => total.date === day.date)?.minutes || 0;
                        const maximum = Math.max(480, ...report.data.dailyTotals.map((total) => total.minutes));
                        const heightPercent = minutes > 0 ? Math.max(8, (minutes / maximum) * 100) : 0;
                        const isWeekend = ['Sat', 'Sun'].includes(day.label);
                        return (
                          <div key={day.date} className={`persona-chart-col ${isWeekend ? 'is-weekend' : ''}`}>
                            <small className="persona-col-hours">{(minutes / 60).toFixed(1)}h</small>
                            <div className="persona-bar-track">
                              <span
                                className={`persona-bar-fill ${minutes > 0 ? 'has-hours' : 'zero-hours'}`}
                                style={{ height: minutes > 0 ? `${heightPercent}%` : '2px' }}
                              />
                            </div>
                            <small className="persona-col-day">{day.label}</small>
                          </div>
                        );
                      })}
                    </div>
                  ) : (
                    <p className="persona-empty-state">Daily totals are not available.</p>
                  )}
                  <div className="persona-hours-legend">
                    <div className="persona-legend-item billable">
                      <span className="persona-legend-dot" />
                      <span>Billable: {((report.data?.billableMinutes || 0) / 60).toFixed(1)}h</span>
                    </div>
                    <div className="persona-legend-item non-billable">
                      <span className="persona-legend-dot" />
                      <span>Non-billable: {((report.data?.nonBillableMinutes || 0) / 60).toFixed(1)}h</span>
                    </div>
                  </div>
                </QueryState>
              ) : (
                <p className="persona-empty-state">Your role does not permit viewing these hours.</p>
              )}
            </div>
          </section>
        </div>
      </div>

    </div>
  );
}
