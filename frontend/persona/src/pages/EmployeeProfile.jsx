import Allocation from '../../../src/features/employees/Allocation';
import EmployeePhoto from '../../../src/features/employees/EmployeePhoto';
import { careerPeriods, careerTenure } from '../../../src/utils/careerPeriods';
import { useState, useEffect, useRef } from 'react';
import { useParams } from 'react-router';
import Link from '../../../src/components/PortalLink';
import { useAuth } from '../../../src/features/auth/context';
import {
  useEmployeeQuery,
  useEmployeeSectionQuery,
  useEmployeeReferencesQuery,
  useEmployeeCareerCatalogQuery,
  useEmployeeProjectsQuery,
  useEmployeeTimeQuery,
  useSaveEmployeeMutation,
} from '../../../src/services/api/employees';
import { useSearchIssuesQuery } from '../../../src/services/api/api';
import { IssueTable } from '../../../src/features/issues/IssueTable';
import { PageHeader, QueryState, ErrorState } from '../../../src/components/common';
import { Overview, Organization, currentWeek } from '../../../src/features/employees/ProfileSections';
import EmployeeEditor from '../../../src/features/employees/EmployeeEditor';
import EmployeeCareer from '../../../src/features/employees/EmployeeCareer';
import { exportToExcelSpreadsheet } from '../exportSpreadsheet';
import DeleteConfirmation from '../components/DeleteConfirmation';
import '../../../src/styles/employees.scss';
function formatFriendlyDuration(str) {
  if (!str || str === 'Not recorded') return str;
  const yearMatch = str.match(/(\d+)\s*year\(s\)/);
  const monthMatch = str.match(/(\d+)\s*month\(s\)/);
  const years = yearMatch ? parseInt(yearMatch[1], 10) : 0;
  const months = monthMatch ? parseInt(monthMatch[1], 10) : 0;

  const parts = [];
  if (years > 0) parts.push(`${years} ${years === 1 ? 'year' : 'years'}`);
  if (months > 0) parts.push(`${months} ${months === 1 ? 'month' : 'months'}`);
  if (parts.length === 0) return '< 1 month';
  return parts.join(', ');
}

function CompanyTimeline({ employee, allowed, isEmbedded = false }) {
  const history = useEmployeeSectionQuery({ id: employee.id, section: 'career-history' }, { skip: !allowed });
  const service = careerTenure([], employee).service;
  const changes = history.data?.filter(entry => entry.changeType === 'CAREER_CHANGE').length;
  const since = employee.joiningDate ? new Date(employee.joiningDate + 'T00:00:00').toLocaleDateString(undefined, {day:'numeric', month:'short',year:'numeric'}) : 'Not recorded';
  const friendlyService = formatFriendlyDuration(service);
  const startYear = employee.joiningDate?.slice(0, 4);
  const startYearNum = parseInt(startYear, 10);
  const currentYear = employee.endDate ? employee.endDate.slice(0, 4) : String(new Date().getFullYear());
  const currentYearNum = parseInt(currentYear, 10);

  const intermediateYears = [];
  if (!isNaN(startYearNum) && !isNaN(currentYearNum) && currentYearNum > startYearNum) {
    for (let y = startYearNum + 1; y < currentYearNum; y++) {
      intermediateYears.push(y);
    }
  }

  const Tag = isEmbedded ? 'div' : 'section';

  return <Tag className={`company-timeline ${isEmbedded ? 'is-embedded' : 'panel'}`} aria-label="Company timeline">
    <div className="company-timeline-header-row">
      <h2>
        <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
          <circle cx="8" cy="8" r="6.25" />
          <path d="M8 4.5V8l2.5 1.5" />
        </svg>
        Timeline
      </h2>
      <span className="company-timeline-since">
        <svg viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
          <rect x="2" y="3" width="12" height="11" rx="2" />
          <path d="M2 6.5h12M5 1.5v3M11 1.5v3" />
        </svg>
        Since {since}
      </span>
    </div>
    <div className="company-timeline-summary">
      <p><strong>{friendlyService || service}</strong> in the company</p>
    </div>
    <div className="company-timeline-track">
      <div className="company-timeline-marker" title={`Joined ${startYear || '—'}`}>
        <span className="company-timeline-start-label">Start</span>
        <span className="company-timeline-dot">
          <span className="company-timeline-dot-inner" />
        </span>
        <strong>{startYear || '—'}</strong>
      </div>
      {intermediateYears.map(yr => (
        <div key={yr} className="company-timeline-segment">
          <span className="company-timeline-line" />
          <div className="company-timeline-year-node" title={String(yr)}>
            <span className="company-timeline-year-dot" />
            <strong>{yr}</strong>
          </div>
        </div>
      ))}
      <div className="company-timeline-segment end-segment">
        <span className="company-timeline-line" />
        <div className="company-timeline-present-marker" title={employee.endDate ? `Ended ${employee.endDate}` : 'Present'}>
          <span className="company-timeline-present-dot" />
          <strong>{currentYear || 'Present'}</strong>
        </div>
      </div>
      <span className="company-timeline-continuation" />
    </div>
    <div className="company-timeline-footer">
      <svg viewBox="0 0 16 16" width="13" height="13" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
        <path d="M2.5 8h11M9.5 4l4 4-4 4" />
      </svg>
      <small>{employee.endDate ? `Employment ended ${employee.endDate}` : 'Your journey continues'}{changes > 0 ? ` · ${changes} recorded career ${changes === 1 ? 'change' : 'changes'}` : ''}</small>
    </div>
  </Tag>;
}
function CareerHistory({ employee }) {
  const query = useEmployeeSectionQuery({ id: employee.id, section: 'career-history' });
  const periods = query.data?.length ? careerPeriods(query.data, employee) : [];

  function getCareerData() {
    const headers = [
      '#',
      'Period Start',
      'Period End',
      'Position Status',
      'Grade / Designation',
      'Job Title',
      'Department',
      'Period Length',
      'Notes',
      'Date Recorded'
    ];

    const rows = periods.map((entry, idx) => [
      idx + 1,
      entry.effectiveDate || 'Not recorded',
      entry.current ? 'Present' : (entry.periodEnd || '—'),
      entry.current ? 'Current position' : (entry.changeType === 'BASELINE' ? 'Previous recorded details' : 'Previous period'),
      [entry.details?.grade, entry.details?.designation].filter(Boolean).join(' · ') || '—',
      entry.details?.jobTitle || '—',
      entry.details?.department || '—',
      entry.duration || '—',
      entry.note || '—',
      entry.recordedAt ? new Date(entry.recordedAt).toLocaleDateString() : '—'
    ]);

    return { headers, rows };
  }

  function handleDownloadCSV() {
    if (!periods.length) return;
    const { headers, rows } = getCareerData();

    function escapeCell(cell) {
      const str = String(cell ?? '');
      if (str.includes(',') || str.includes('"') || str.includes('\n') || str.includes('\r')) {
        return `"${str.replace(/"/g, '""')}"`;
      }
      return str;
    }

    const csvContent = '\uFEFF' + [
      headers.map(escapeCell).join(','),
      ...rows.map(row => row.map(escapeCell).join(','))
    ].join('\r\n');

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    const code = employee.employeeCode || `${employee.firstName || 'Employee'}_${employee.lastName || ''}`;
    link.setAttribute('href', url);
    link.setAttribute('download', `Career_History_${code}_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  function handleExportExcel() {
    if (!periods.length) return;
    const { headers, rows } = getCareerData();
    const empName = `${employee.preferredName || employee.firstName || ''} ${employee.lastName || ''}`.trim() || 'Employee';
    const empCode = employee.employeeCode ? `#${employee.employeeCode}` : (employee.id ? `#EMP${employee.id}` : '');

    const currentPeriod = periods.find(p => p.current) || periods[0];
    const prevPeriod = periods.length > 1 ? periods[1] : null;

    const department = currentPeriod?.details?.department || employee.department || employee.departmentName || 'Developer';
    const designation = [currentPeriod?.details?.grade, currentPeriod?.details?.designation || currentPeriod?.details?.jobTitle].filter(Boolean).join(' · ') || employee.jobTitle || 'Developer';

    const prevGrade = prevPeriod ? ([prevPeriod.details?.grade, prevPeriod.details?.designation].filter(Boolean).join(' · ') || 'T0 · Intern') : 'T0 · Intern';
    const prevTitle = prevPeriod?.details?.jobTitle || 'Intern Software Engineer';
    const prevDept = prevPeriod?.details?.department || department;
    const prevStart = prevPeriod?.effectiveDate ? String(prevPeriod.effectiveDate).slice(0, 10) : (employee.joiningDate ? String(employee.joiningDate).slice(0, 10) : '2025-03-10');
    const prevEnd = prevPeriod?.periodEnd ? String(prevPeriod.periodEnd).slice(0, 10) : (currentPeriod?.effectiveDate ? String(currentPeriod.effectiveDate).slice(0, 10) : '2026-01-05');
    const prevDuration = prevPeriod?.duration || '9 months, 26 days';

    const currStart = currentPeriod?.effectiveDate ? String(currentPeriod.effectiveDate).slice(0, 10) : (employee.joiningDate ? String(employee.joiningDate).slice(0, 10) : '—');
    const currEnd = currentPeriod?.current ? 'Present' : (currentPeriod?.periodEnd ? String(currentPeriod.periodEnd).slice(0, 10) : 'Present');
    const currDuration = currentPeriod?.duration || '9 months, 2 days';

    const card = {
      emp: employee,
      empName,
      empCode: employee.employeeCode || (employee.id ? `EMP${employee.id}` : '—'),
      dept: department,
      isSingle: !prevPeriod,
      previous: prevPeriod ? {
        grade: prevGrade,
        jobTitle: prevTitle,
        department: prevDept,
        periodStart: prevStart,
        periodEnd: prevEnd,
        duration: prevDuration,
      } : null,
      current: {
        grade: designation,
        jobTitle: currentPeriod?.details?.jobTitle || employee.jobTitle || (department === 'Administration' ? 'Assistant Office Manager' : `${department} Specialist`),
        department,
        periodStart: currStart,
        periodEnd: currEnd,
        duration: currDuration,
      }
    };

    const metadata = [
      { label: 'Total Employees', value: '1' },
      { label: 'Employee ID', value: employee.employeeCode || (employee.id ? `EMP${employee.id}` : '—') },
      { label: 'Export Date', value: new Date().toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) },
      { label: 'Records Count', value: String(periods.length) },
    ];

    const code = employee.employeeCode || (employee.id ? `EMP${employee.id}` : empName.replace(/\s+/g, '_'));
    exportToExcelSpreadsheet({
      filename: `Career_History_${code}_${new Date().toISOString().slice(0, 10)}.xlsx`,
      sheetName: 'Career History',
      title: `Career History Report · ${empName} ${empCode}`.trim(),
      metadata,
      cards: [card],
      headers,
      rows,
    });
  }

  return (
    <section className="employee-panel career-history-panel">
      <div className="employee-panel-header-row">
        <div className="employee-panel-header-text">
          <h2>Career history</h2>
          <p>Grade and role changes within your organization, newest first.</p>
        </div>
        {periods.length > 0 && (
          <div className="career-history-actions">
            <button
              type="button"
              className="button button-download-csv"
              onClick={handleDownloadCSV}
              title="Download career history as CSV"
            >
              <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
                <path d="M2.5 10.5v2a1 1 0 0 0 1 1h9a1 1 0 0 0 1-1v-2M8 2.5v7.5M5 7l3 3 3-3" />
              </svg>
              Download
            </button>
            <button
              type="button"
              className="button button-export-excel"
              onClick={handleExportExcel}
              title="Export career history to Excel (.xlsx)"
            >
              <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
                <rect x="2.5" y="2" width="11" height="12" rx="1.5" />
                <path d="M5.5 5.5l5 5M10.5 5.5l-5 5" />
              </svg>
              Export
            </button>
          </div>
        )}
      </div>
      <QueryState query={query}>
        {periods.length ? (
          <div className="employee-table-scroll" role="region" aria-label="Career history table" tabIndex={0}>
            <table className="employee-history-table">
              <thead>
                <tr>
                  {['Period','Grade / designation','Job title','Department','Period length','Notes'].map(label=><th key={label} scope="col">{label}</th>)}
                </tr>
              </thead>
              <tbody>
                {periods.map(entry => (
                  <tr key={entry.id}>
                    <td>
                      <strong className="career-period-dates">{entry.effectiveDate || 'Date not recorded'} → {entry.current ? 'Present' : entry.periodEnd}</strong>
                      <small className={entry.current ? 'career-badge-current' : 'career-badge-past'}>
                        {entry.current ? 'Current position' : entry.changeType === 'BASELINE' ? 'Previous recorded details' : 'Previous period'}
                      </small>
                    </td>
                    <td>{[entry.details.grade, entry.details.designation].filter(Boolean).join(' · ') || '—'}</td>
                    <td>{entry.details.jobTitle || '—'}</td>
                    <td>{entry.details.department || '—'}</td>
                    <td>{entry.duration}</td>
                    <td>
                      {entry.note || '—'}
                      <small className="career-note-date">Recorded {new Date(entry.recordedAt).toLocaleDateString()}</small>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="employee-career-empty">No career changes recorded yet. Earlier promotions have not been imported.</p>
        )}
      </QueryState>
    </section>
  );
}
function Work({ employee, time = false }) {
  const { can, session } = useAuth();
  const own = String(employee.userId) === String(session.user.id);
  const isPrivileged = [session.user.role, ...(session.user.roles || [])].some((r) => ['ADMIN', 'HR'].includes(r));
  const [from, setFrom] = useState(() => currentWeek().from);
  const [to, setTo] = useState(() => currentWeek().to);
  const projects = useEmployeeProjectsQuery(employee.userId, {
    skip: time || !can('PROJECT_VIEW'),
  });
  const issues = useSearchIssuesQuery(
    { AssigneeUserId: employee.userId, Page: 1, PageSize: 10 },
    { skip: time || !can('ISSUE_VIEW') || (!own && !isPrivileged) },
  );
  const permitted = can('TIMESHEET_VIEW') && (own || can('TIMESHEET_TEAM_VIEW'));
  const report = useEmployeeTimeQuery(
    { userId: employee.userId, from, to, team: !own },
    { skip: !time || !permitted },
  );
  return (
    <div className="employee-panel">
      {time ? (
        <>
          <h2>Timesheet · Current Week</h2>
          {permitted ? (
            <>
              <div className="employee-filters">
                <label>
                  From
                  <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
                </label>
                <label>
                  To
                  <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
                </label>
              </div>
              <QueryState query={report}>
                <div className="employee-timesheet-stats">
                  <p className="timesheet-stat-line">
                    Logged hours: {((report.data?.totalMinutes || 0) / 60).toFixed(2)}
                  </p>
                  <p className="timesheet-stat-line">
                    Billable hours: {((report.data?.billableMinutes || 0) / 60).toFixed(2)}
                  </p>
                  <p className="timesheet-stat-line">
                    Non-billable hours: {((report.data?.nonBillableMinutes || 0) / 60).toFixed(2)}
                  </p>
                </div>
                <h3>Recent work logs</h3>
                <div className="profile-log-list">
                  {report.data?.workLogs?.items?.map((log) => (
                    <article key={log.id}>
                      <Link to={`/issues/${log.issueId}`}>{log.issueKey}</Link>
                      <span>
                        {log.workDate} · {(log.durationMinutes / 60).toFixed(2)} hours ·{' '}
                        {log.billable ? 'Billable' : 'Non-billable'}
                      </span>
                      <p>{log.description}</p>
                    </article>
                  ))}
                </div>
                {!report.data?.workLogs?.items?.length && (
                  <p className="employee-career-empty">No work logs in this period.</p>
                )}
                <Link
                  className="employee-timesheet-link"
                  to={own ? '/timesheets/my' : '/timesheets/team'}
                >
                  Open My Timesheet →
                </Link>
              </QueryState>
            </>
          ) : (
            <p>Your role does not permit viewing this employee’s timesheet.</p>
          )}
        </>
      ) : (
        <>
          <div className="persona-record-card employee-work-card">
            <header>
              <div className="persona-card-title">
                <svg className="persona-card-icon persona-icon-personal" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
                </svg>
                <h2>Active projects</h2>
              </div>
              {projects.data?.length > 0 && (
                <span className="persona-badge hr-badge">
                  {projects.data.length} {projects.data.length === 1 ? 'project' : 'projects'}
                </span>
              )}
            </header>
            <div className="persona-card-body table-card-body">
              {can('PROJECT_VIEW') && (
                <QueryState query={projects}>
                  <div className="profile-project-table">
                    <table>
                      <thead>
                        <tr>
                          <th>Project</th>
                          <th>Role</th>
                          <th>Status</th>
                          <th>Issues</th>
                        </tr>
                      </thead>
                      <tbody>
                        {projects.data?.map((p) => {
                          const statusText =
                            typeof p.status === 'number'
                              ? ['Active', 'Archived', 'Disabled'][p.status]
                              : p.status;
                          return (
                            <tr key={p.id}>
                              <td>
                                <Link className="profile-project-link" to={`/projects/${p.id}`}>
                                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                    <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
                                  </svg>
                                  {p.projectName}
                                </Link>
                              </td>
                              <td>
                                <span className="summary-pill summary-pill-neutral">
                                  {p.role || 'Member'}
                                </span>
                              </td>
                              <td>
                                <span className={`summary-pill ${statusText === 'Active' ? 'summary-pill-success' : 'summary-pill-neutral'}`}>
                                  {statusText}
                                </span>
                              </td>
                              <td>
                                <span className="summary-id-badge">
                                  {p.issueCount ?? '0'}
                                </span>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                  {projects.data?.length === 0 && <p className="employee-career-empty">No accessible project memberships.</p>}
                </QueryState>
              )}
            </div>
          </div>

          {(own || isPrivileged) && (
            <div className="persona-record-card employee-work-card">
              <header>
                <div className="persona-card-title">
                  <svg className="persona-card-icon persona-icon-job" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M9 11l3 3L22 4" />
                    <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
                  </svg>
                  <h2>Assigned issues</h2>
                </div>
                {can('ISSUE_VIEW') && (
                  <Link className="persona-header-link" to={`/issues?AssigneeUserId=${employee.userId}`}>
                    View all assigned issues →
                  </Link>
                )}
              </header>
              <div className="persona-card-body">
                {can('ISSUE_VIEW') && (
                  <QueryState query={issues}>
                    <IssueTable issues={issues.data?.items} />
                    <div className="employee-work-card-footer">
                      <Link className="employee-assigned-issues-link" to={`/issues?AssigneeUserId=${employee.userId}`}>
                        View all assigned issues →
                      </Link>
                    </div>
                  </QueryState>
                )}
              </div>
            </div>
          )}

          <div className="employee-work-allocation-wrap">
            <Allocation employee={employee} />
          </div>
        </>
      )}
      <p className="muted work-tab-notice">Data is limited by your existing Jira and timesheet permissions.</p>
    </div>
  );
}
function Manager({ id }) {
  const query = useEmployeeQuery(id);
  return (
    <QueryState query={query}>
      <Link to={`/employees/${id}`}>
        {query.data?.firstName} {query.data?.lastName}
      </Link>
    </QueryState>
  );
}
export default function EmployeeProfile({ self = false, skills = false, management = false }) {
  const { id } = useParams();
  const { session, can } = useAuth();
  const query = useEmployeeQuery(self ? 'me' : id);
  const me = useEmployeeQuery('me');
  const refs = useEmployeeReferencesQuery();
  const catalog = useEmployeeCareerCatalogQuery();
  const [tab, setTab] = useState(skills ? 'skills' : 'overview');
  const [editing, setEditing] = useState(false);
  const [message, setMessage] = useState('');
  const [save, result] = useSaveEmployeeMutation();
  const [deleted, setDeleted] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const editorRef = useRef(null);

  useEffect(() => {
    if (editing && editorRef.current) {
      const topNavHeight = 90;
      const elementTop = editorRef.current.getBoundingClientRect().top + window.pageYOffset;
      window.scrollTo({
        top: Math.max(0, elementTop - topNavHeight),
        behavior: 'smooth',
      });
      const firstField = editorRef.current.querySelector('input:not([type="hidden"]), select, textarea');
      firstField?.focus({ preventScroll: true });
    }
  }, [editing]);
  const e = query.data;
  const sensitive = can('EMPLOYEE_VIEW_SENSITIVE') && [session.user.role, ...(session.user.roles || [])].some(role => ['ADMIN', 'HR'].includes(role));
  const own = e && String(e.userId) === String(session.user.id);
  const editCareer = (management && can('EMPLOYEE_UPDATE')) || (own && can('EMPLOYEE_UPDATE_SELF'));
  const viewCareer =
    editCareer ||
    own ||
    can('EMPLOYEE_VIEW') ||
    (can('EMPLOYEE_VIEW_TEAM') && me.data && e?.managerEmployeeId === me.data.id);
  const reference = (key) => {
    const level = catalog.data?.levels?.find((l) => l.id === e?.careerLevelId);
    if (key === 'gradeId' && level) return level.grade;
    if (key === 'jobTitleId' && level) return level.careerTitle;
    return refs.data?.find((r) => r.id === e?.[key])?.name || '—';
  };
  if (self && query.error?.status === 404)
    return (
      <div className="employee-page">
        <PageHeader title="My profile" description="Organization and career information." />
        <section className="panel employee-panel">
          <h2>Your employee profile is not set up yet</h2>
          <p>
            You are signed in as {session.user.email || session.user.name}. Your login account has
            not been linked to an employee profile.
          </p>
          {can('EMPLOYEE_CREATE') ? (
            <>
              <p>
                Add an employee profile, select your login account, and enter your employee code,
                name and joining date.
              </p>
              <Link className="button primary" to="/employees/admin?create=1">
                Add employee profile
              </Link>
            </>
          ) : (
            <p>
              Ask your organization administrator to create your employee profile and link it to
              this login account.
            </p>
          )}
          <div className="actions">
            {can('EMPLOYEE_CREATE') && <Link className="button" to="/employees/admin">
              Employee management
            </Link>}
            <button onClick={() => query.refetch()} disabled={query.isFetching}>
              Check again
            </button>
          </div>
        </section>
      </div>
    );
  if (deleted)
    return (
      <section className="panel employee-panel">
        <h1>Employee profile deleted</h1>
        <p>The login account and historical work records remain intact.</p>
        <Link to="/employees/admin">Employee management</Link>
      </section>
    );
  return (
    <div className="employee-page">
      <PageHeader
        title={self ? 'My profile' : 'Employee profile'}
        description="Organization and career information."
      >
        {can('EMPLOYEE_CREATE') && <>
          <Link className="button" to="/employees/admin">Employee management</Link>
          <Link className="button primary" to="/employees/admin?create=1">Add employee profile</Link>
        </>}
      </PageHeader>
      <QueryState query={query}>
        {e && (
          <>
            <div className="employee-summary-grid">
              <div className="employee-profile-header">
                <div className="employee-avatar-wrapper">
                  <EmployeePhoto
                    className="employee-avatar"
                    src={e.profilePhotoUrl}
                    alt={`${e.firstName} ${e.lastName}`}
                    initials={`${e.firstName?.[0] || ''}${e.lastName?.[0] || ''}`}
                    referrerPolicy="no-referrer"
                  />
                </div>
                <div className="employee-profile-info">
                  <div className="employee-profile-name-row">
                    <h1>
                      {e.firstName} {e.lastName}
                    </h1>
                    <span className="badge badge-active">
                      <span className="status-dot" aria-hidden="true" />
                      {e.employmentStatus || 'Active'}
                    </span>
                  </div>
                  <div className="employee-badges-column">
                    {e.employeeCode && (
                      <div className="employee-badge-line">
                        <span className="employee-code-pill" title="Employee ID">
                          # {e.employeeCode}
                        </span>
                      </div>
                    )}
                    {([reference('gradeId'), reference('jobTitleId')].some((v) => v && v !== '—') || reference('designationId')) && (
                      <div className="employee-badge-line">
                        <span className="employee-role-subtitle" title="Grade & Job Title">
                          <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
                            <rect x="2" y="4.5" width="12" height="9.5" rx="2" />
                            <path d="M5.5 4.5V3a1.5 1.5 0 0 1 1.5-1.5h2A1.5 1.5 0 0 1 10.5 3v1.5" />
                          </svg>
                          <span>
                            {[reference('gradeId'), reference('jobTitleId')]
                              .filter((v) => v && v !== '—')
                              .join(' · ') || reference('designationId') || '—'}
                          </span>
                        </span>
                      </div>
                    )}
                    {(reference('departmentId') !== '—' || reference('designationId') !== '—') && (
                      <div className="employee-badge-line">
                        <span className="employee-dept-subtitle" title="Department / Designation">
                          <svg viewBox="0 0 16 16" width="13" height="13" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
                            <rect x="3" y="2.5" width="10" height="11" rx="1.5" />
                            <path d="M6 5.5h1M9 5.5h1M6 8.5h1M9 8.5h1M6 11.5h4" />
                          </svg>
                          <span>{reference('departmentId') !== '—' ? reference('departmentId') : reference('designationId')}</span>
                        </span>
                      </div>
                    )}
                  </div>
                </div>
                <div className="actions">
                  {((management && can('EMPLOYEE_UPDATE')) ||
                    (own && can('EMPLOYEE_UPDATE_SELF'))) && (
                    <button className="button-edit-profile" onClick={() => setEditing(!editing)}>
                      <svg viewBox="0 0 16 16" width="13" height="13" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
                        <path d="M11.5 2.5a1.414 1.414 0 0 1 2 2L5 13H2v-3L11.5 2.5z" />
                      </svg>
                      Edit profile
                    </button>
                  )}
                  {management && can('EMPLOYEE_DELETE') && (
                    <button
                      className="button-delete-profile"
                      disabled={result.isLoading}
                      onClick={() => setConfirmDelete(true)}
                    >
                      Delete employee
                    </button>
                  )}
                </div>
              </div>

              <CompanyTimeline employee={e} allowed={true} />
            </div>
            <ErrorState error={result.error} />
            {message && <p role="status">{message}</p>}
            {editing && (
              <div ref={editorRef} className="employee-editor-container" style={{ scrollMarginTop: '20px' }}>
                <EmployeeEditor
                  employee={e}
                  references={refs.data || []}
                  self={!management || !can('EMPLOYEE_UPDATE')}
                  onSaved={() => setMessage('Profile saved.')}
                  onClose={() => {
                    setEditing(false);
                    query.refetch?.();
                  }}
                />
              </div>
            )}
            {confirmDelete && e && (
              <DeleteConfirmation
                title={`Delete employee profile for ${e.firstName} ${e.lastName}?`}
                description="Delete this employee profile? Their login account and historical work records will remain intact."
                confirmLabel="Delete employee"
                variant="danger"
                busy={result.isLoading}
                error={result.error}
                onCancel={() => {
                  setConfirmDelete(false);
                  result.reset();
                }}
                onConfirm={async () => {
                  try {
                    await save({
                      path: `/${e.id}`,
                      method: 'DELETE',
                      version: e.version,
                    }).unwrap();
                    setDeleted(true);
                    setConfirmDelete(false);
                  } catch {
                    /* Keep modal open with error */
                  }
                }}
              />
            )}
            <section className="panel">
              <nav className="employee-tabs" aria-label="Employee profile sections">
                {(own || sensitive
                  ? [
                      'overview',
                      'personal',
                      'job',
                      'career history',
                      'skills',
                      'experience',
                      'education',
                      'certifications',
                      'languages',
                      'projects',
                      'organization',
                    ]
                  : [
                      'overview',
                      'job',
                      'career history',
                      'projects',
                      'organization',
                    ]
                )
                  .concat(sensitive ? ['compensation'] : [])
                  .map((t) => (
                    <button key={t} aria-pressed={tab === t} onClick={() => setTab(t)}>
                      {t[0].toUpperCase() + t.slice(1)}
                    </button>
                  ))}
              </nav>
              {tab === 'overview' && (
                <Overview
                  employee={e}
                  viewCareer={own || sensitive}
                  personal={<Personal summary employee={e} location={reference('locationId')} />}
                  job={<Job summary employee={e} reference={reference} viewCareer={true} />}
                  organization={
                    <Organization
                      employee={e}
                      reference={reference}
                      manager={
                        e.managerEmployeeId ? (
                          <Manager id={e.managerEmployeeId} />
                        ) : (
                          'No manager assigned'
                        )
                      }
                    />
                  }
                />
              )}
              {tab === 'personal' && (
                <section className="employee-panel">
                  <div className="persona-record-card employee-tab-card">
                    <header>
                      <div className="persona-card-title">
                        <svg className="persona-card-icon persona-icon-personal" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                          <circle cx="12" cy="7" r="4" />
                        </svg>
                        <h2>Personal information</h2>
                      </div>
                      <div className="persona-card-actions">
                        <span className="persona-badge hr-badge">HR Core Records</span>
                        {((own && can('EMPLOYEE_UPDATE_SELF')) || can('EMPLOYEE_UPDATE')) && (
                          <button type="button" className="employee-action-btn" onClick={() => setEditing(true)}>
                            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                              <path d="M17 3a2.828 2.828 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5L17 3z" />
                            </svg>
                            Edit personal information
                          </button>
                        )}
                      </div>
                    </header>
                    <Personal employee={e} location={reference('locationId')} />
                  </div>
                </section>
              )}
              {tab === 'career history' && <CareerHistory employee={e} />}
              {tab === 'job' && (
                <section className="employee-panel">
                  <div className="employee-job-tab-grid">
                    <div className="persona-record-card employee-tab-card">
                      <header>
                        <div className="persona-card-title">
                          <svg className="persona-card-icon persona-icon-job" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <rect x="2" y="7" width="20" height="14" rx="2" ry="2" />
                            <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16" />
                          </svg>
                          <h2>Job information</h2>
                        </div>
                        <span className="persona-badge job-badge">Position Architecture</span>
                      </header>
                      <Job employee={e} reference={reference} viewCareer={viewCareer} hideWorkAddress />
                    </div>

                    <div className="persona-record-card employee-tab-card employee-work-address-card">
                      <header>
                        <div className="persona-card-title">
                          <svg className="persona-card-icon persona-icon-location" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z" />
                            <circle cx="12" cy="10" r="3" />
                          </svg>
                          <h2>Work address</h2>
                        </div>
                        <span className="persona-badge location-badge">Office Facility</span>
                      </header>
                      <div className="employee-work-address-content">
                        <div className="work-address-hero">
                          <div className="work-address-hero-icon">
                            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                              <rect width="16" height="20" x="4" y="2" rx="2" ry="2"/>
                              <path d="M9 22v-4h6v4"/>
                              <path d="M8 6h.01"/>
                              <path d="M16 6h.01"/>
                              <path d="M12 6h.01"/>
                              <path d="M12 10h.01"/>
                              <path d="M12 14h.01"/>
                              <path d="M16 10h.01"/>
                              <path d="M16 14h.01"/>
                              <path d="M8 10h.01"/>
                              <path d="M8 14h.01"/>
                            </svg>
                          </div>
                          <div className="work-address-hero-text">
                            <h3>Embassy Splendid Tech Zone</h3>
                            <p className="work-address-facility-type">Primary Global Tech Campus · Block-2</p>
                          </div>
                        </div>

                        <div className="work-address-full-block">
                          <div className="work-address-label">Full Address</div>
                          <div className="work-address-text">
                            {e.workAddress || '1st Floor, Block-2, Embassy Splendid Tech Zone, 181/183, Pallavaram-Thuraipakkam Road, Zamin Pallavaram, Arulmurugan Nagar, Tambaram, Tamil Nadu Chennai- 600117 India'}
                          </div>
                        </div>

                        <dl className="employee-job-rows work-address-meta-rows">
                          <div>
                            <dt>Building / Unit</dt>
                            <dd><span className="summary-value">1st Floor, Block-2</span></dd>
                          </div>
                          <div>
                            <dt>Tech Park</dt>
                            <dd><span className="summary-value">Embassy Splendid Tech Zone</span></dd>
                          </div>
                          <div>
                            <dt>Arterial Road</dt>
                            <dd><span className="summary-value">181/183, Pallavaram-Thuraipakkam Road</span></dd>
                          </div>
                          <div>
                            <dt>Locality</dt>
                            <dd><span className="summary-value">Zamin Pallavaram, Tambaram</span></dd>
                          </div>
                          <div>
                            <dt>City & State</dt>
                            <dd><span className="summary-value">Chennai, Tamil Nadu</span></dd>
                          </div>
                          <div>
                            <dt>Postal Code</dt>
                            <dd><span className="summary-id-badge">600117</span></dd>
                          </div>
                          <div>
                            <dt>Country</dt>
                            <dd><span className="summary-value">India</span></dd>
                          </div>
                        </dl>
                      </div>
                    </div>
                  </div>
                </section>
              )}
              {tab === 'organization' && (
                <Organization
                  employee={e}
                  reference={reference}
                  manager={
                    e.managerEmployeeId ? (
                      <Manager id={e.managerEmployeeId} />
                    ) : (
                      'No manager assigned'
                    )
                  }
                />
              )}
              {['skills', 'experience', 'education', 'certifications', 'languages'].includes(tab) && (
                <EmployeeCareer key={tab} employee={e} section={tab} editable={editCareer} />
              )}
              {tab === 'compensation' && sensitive && (
                <EmployeeCareer
                  employee={e}
                  section="compensation"
                  editable={management && can('EMPLOYEE_UPDATE')}
                />
              )}
              {tab === 'projects' && (
                <Work key={tab} employee={e} time={false} />
              )}
              {!sensitive && <p className="profile-restricted">Compensation · 🔒 Restricted</p>}
            </section>
          </>
        )}
      </QueryState>
    </div>
  );
}

function formatAadhaar(val) {
  if (!val) return '';
  const digits = String(val).replace(/\s+/g, '');
  if (/^\d{12}$/.test(digits)) {
    return `${digits.slice(0, 4)} ${digits.slice(4, 8)} ${digits.slice(8, 12)}`;
  }
  return String(val);
}

function Personal({ employee: e, location, summary = false }) {
  const normLocation = location ? location.replace(/\s*,\s*/g, ', ') : location;
  return (
    <dl className="employee-details">
      {[
        ['First name', e.firstName],
        ['Last name', e.lastName],
        ['Preferred name', e.preferredName],
        ['Work email', e.workEmail],
        ['Phone', e.phone],
        ['Location', normLocation],
      ].map(([label, value]) => (
        <div key={label}>
          <dt>{label}</dt>
          <dd>
            {label === 'Work email' && value ? (
              <a className="summary-email" href={`mailto:${value}`}>
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <rect width="20" height="16" x="2" y="4" rx="2"/>
                  <path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/>
                </svg>
                {value}
              </a>
            ) : value ? (
              <span className="summary-value">{value}</span>
            ) : (
              <span className="summary-empty">—</span>
            )}
          </dd>
        </div>
      ))}
      <div>
        <dt>Profile photo</dt>
        <dd>
          {e.profilePhotoUrl ? (
            <div className="personal-avatar-wrap">
              <EmployeePhoto
                className="employee-avatar"
                src={e.profilePhotoUrl}
                alt="Profile"
                referrerPolicy="no-referrer"
              />
              <span className="summary-pill summary-pill-success">Uploaded</span>
            </div>
          ) : (
            <span className="summary-pill summary-pill-neutral">Avatar generated</span>
          )}
        </dd>
      </div>

      {!summary && (
        <>
          <div className="personal-section-divider">
            <span className="personal-section-heading">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <rect width="18" height="18" x="3" y="3" rx="2" />
                <path d="M7 7h.01M7 12h.01M7 17h.01M11 7h6M11 12h6M11 17h6" />
              </svg>
              Statutory &amp; Identification
            </span>
          </div>
          <div>
            <dt>Aadhaar number</dt>
            <dd>
              {e.aadharNumber ? (
                <span className="summary-value statutory-badge">
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                  </svg>
                  {formatAadhaar(e.aadharNumber)}
                </span>
              ) : (
                <span className="summary-empty">—</span>
              )}
            </dd>
          </div>

          <div className="personal-section-divider">
            <span className="personal-section-heading">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                <polyline points="9 22 9 12 15 12 15 22" />
              </svg>
              Residential Address
            </span>
          </div>
          <div>
            <dt>Permanent address</dt>
            <dd>
              {e.permanentAddress ? (
                <span className="summary-value permanent-address-text">{e.permanentAddress}</span>
              ) : (
                <span className="summary-empty">—</span>
              )}
            </dd>
          </div>

          <div className="personal-section-divider">
            <span className="personal-section-heading">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
                <circle cx="9" cy="7" r="4" />
                <path d="M22 21v-2a4 4 0 0 0-3-3.87" />
                <path d="M16 3.13a4 4 0 0 1 0 7.75" />
              </svg>
              Emergency Contact
            </span>
          </div>
          <div>
            <dt>Emergency contact</dt>
            <dd>
              {e.emergencyContactName ? (
                <span className="summary-value font-semibold">{e.emergencyContactName}</span>
              ) : (
                <span className="summary-empty">—</span>
              )}
            </dd>
          </div>
          <div>
            <dt>Relationship</dt>
            <dd>
              {e.emergencyContactRelation ? (
                <span className="summary-pill summary-pill-neutral relation-badge">{e.emergencyContactRelation}</span>
              ) : (
                <span className="summary-empty">—</span>
              )}
            </dd>
          </div>
          <div>
            <dt>Emergency phone</dt>
            <dd>
              {e.emergencyContactPhone ? (
                <a className="summary-phone-link" href={`tel:${e.emergencyContactPhone}`}>
                  <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z" />
                  </svg>
                  {e.emergencyContactPhone}
                </a>
              ) : (
                <span className="summary-empty">—</span>
              )}
            </dd>
          </div>
        </>
      )}
    </dl>
  );
}

function Job({ employee: e, reference, viewCareer, summary = false, hideWorkAddress = false }) {
  const history = useEmployeeSectionQuery({ id: e.id, section: 'career-history' }, { skip: !viewCareer });
  const tenure = careerTenure(history.data, e);
  const recorded = (value) => (value && value !== '—' ? value : 'Not recorded');
  const title = reference('jobTitleId');
  const profile = [reference('departmentId'), reference('gradeId'), title]
    .filter((v) => v && v !== '—')
    .join(' · ');
  const rawLocation = reference('locationId');
  const normLocation = rawLocation && rawLocation !== '—' ? rawLocation.replace(/\s*,\s*/g, ', ') : 'Not recorded';
  const date = (value) =>
    value
      ? new Date(value + 'T00:00:00').toLocaleDateString(undefined, {
          year: 'numeric',
          month: 'short',
          day: 'numeric',
        })
      : 'Not recorded';
  const fteDisplay = () => {
    if (e.fte != null && e.fte !== '') {
      const num = Number(e.fte);
      if (!Number.isNaN(num)) {
        return num <= 1 ? `${Math.round(num * 100)}%` : `${num}%`;
      }
      return String(e.fte).endsWith('%') ? e.fte : `${e.fte}%`;
    }
    const type = String(e.employmentType || '').toUpperCase();
    if (type === 'FULL_TIME' || type === 'FULLTIME' || type === 'PERMANENT') {
      return '100%';
    }
    if (type === 'PART_TIME' || type === 'PARTTIME') {
      return '50%';
    }
    if (type === 'CONTRACT' || type === 'INTERN' || type === 'INTERNSHIP') {
      return '100%';
    }
    return 'Not recorded';
  };

  return (
    <div className="employee-job-record">
      <dl className="employee-job-rows">
        {[
          ['Employee ID', e.employeeCode],
          ['Business Title', recorded(title)],
          ['Job Profile', recorded(profile)],
          ['Designation', recorded(reference('designationId'))],
          ['Time Type', e.employmentType ? e.employmentType.charAt(0) + e.employmentType.slice(1).toLowerCase().replaceAll('_', ' ') : null],
          ['FTE', fteDisplay()],
          ['Location', normLocation],
          ['Hire Date', date(e.joiningDate)],
          ...(!summary ? [['Length of Service', tenure.service], ['Time in Position', tenure.position]] : []),
        ].map(([label, value]) => {
          const isNotRecorded = !value || value === 'Not recorded';
          return (
            <div key={label}>
              <dt>{label}</dt>
              <dd>
                {label === 'Employee ID' && value ? (
                  <span className="summary-id-badge">{value}</span>
                ) : label === 'Time Type' && value ? (
                  <span className="summary-pill summary-pill-primary">{value}</span>
                ) : isNotRecorded ? (
                  <span className="summary-empty">Not recorded</span>
                ) : (
                  <span className="summary-value">{value}</span>
                )}
              </dd>
            </div>
          );
        })}
      </dl>
      {!summary && (
        <>
          <h2>Contact Information - Public</h2>
          <dl className="employee-job-rows">
            <div>
              <dt>Email</dt>
              <dd>
                {e.workEmail ? (
                  <a className="summary-email" href={`mailto:${e.workEmail}`}>
                    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                      <rect width="20" height="16" x="2" y="4" rx="2"/>
                      <path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/>
                    </svg>
                    {e.workEmail}
                  </a>
                ) : (
                  <span className="summary-empty">Not recorded</span>
                )}
              </dd>
            </div>
          </dl>
          {!hideWorkAddress && (
            <>
              <h2>Work Address</h2>
              <p className="employee-work-address">
                {e.workAddress || '1st Floor, Block-2, Embassy Splendid Tech Zone, 181/183, Pallavaram-Thuraipakkam Road, Zamin Pallavaram, Arulmurugan Nagar, Tambaram, Tamil Nadu Chennai- 600117 India'}
              </p>
            </>
          )}
        </>
      )}
    </div>
  );
}
