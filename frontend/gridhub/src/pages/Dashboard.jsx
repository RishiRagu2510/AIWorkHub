import '../../../src/styles/dashboard.scss';
import { useWorkflowsQuery } from '../../../src/services/api/collaboration';
import { statuses } from '../../../src/features/issues/contracts';
import { LiveStatus } from '../../../src/components/LiveStatus';
import { liveQueryOptions } from '../../../src/services/api/live';
import { useState, useMemo } from 'react';
import AdminCharts from '../../../src/features/dashboard/AdminCharts';
import Link from '../../../src/components/PortalLink';
import { PermissionGuard } from '../../../src/features/auth/AuthProvider';
import { useAuth } from '../../../src/features/auth/context';
import { FeatureGuard } from '../../../src/features/auth/FeatureGuard';
import { ATHENNA } from '../../../src/features/auth/features';
import {
  useProjectsQuery,
  useSearchIssuesQuery,
  useIssueStatusCountsQuery,
} from '../../../src/services/api/api';
import { PageHeader, QueryState, EmptyState } from '../../../src/components/common';
import { Icon } from '../../../src/components/Icon';
import { IssueTable } from '../../../src/features/issues/IssueTable';
import { label } from '../../../src/utils/format';
import { useRunAthennaCopilotMutation } from '../../../src/services/api/agent';
import AthennaStandupDigestCard from '../../../src/features/dashboard/AthennaStandupDigestCard';

function Metric({ title, icon, query, enabled, value, children }) {
  return (
    <section className={`metric metric-${icon}`}>
      <div className="metric-header-row">
        <span className="metric-label-text">{title}</span>
        <span className="metric-icon-tile">
          <Icon name={icon} />
        </span>
      </div>
      {enabled ? (
        <QueryState query={query}>
          <div className="metric-value-row">
            <strong>{typeof value === 'number' ? value.toLocaleString() : (value ?? '—')}</strong>
          </div>
          <div className="metric-action-link">{children}</div>
        </QueryState>
      ) : (
        <p className="metric-unavailable">No data available</p>
      )}
    </section>
  );
}

export default function Dashboard() {
  const { session, can } = useAuth();
  const [issueView, setIssueView] = useState('recent');
  const issueWorkspaceParams = new URLSearchParams({
    filters: '1',
    SortBy: 'updatedAt',
    SortDirection: 'desc',
    ...(issueView === 'assigned'
      ? { AssigneeUserId: String(session?.user?.id) }
      : issueView === 'created'
        ? { ReporterUserId: String(session?.user?.id) }
        : {}),
  });
  const issueWorkspaceUrl = `/issues?${issueWorkspaceParams}`;
  const issueDescription =
    issueView === 'assigned'
      ? 'Your assigned issues, with the most recently updated first'
      : issueView === 'created'
        ? 'Issues you created, with the most recently updated first'
        : 'The most recently updated issues you can access';
  const projects = useProjectsQuery(undefined, { ...liveQueryOptions, skip: !can('PROJECT_VIEW') });
  const issues = useSearchIssuesQuery(
    {
      Page: 1,
      PageSize: 5,
      SortBy: 'updatedAt',
      SortDirection: 'desc',
      ...(issueView === 'assigned'
        ? { AssigneeUserId: session?.user?.id }
        : issueView === 'created'
          ? { ReporterUserId: session?.user?.id }
          : {}),
    },
    { ...liveQueryOptions, skip: !can('ISSUE_VIEW') },
  );
  const totals = useSearchIssuesQuery(
    { Page: 1, PageSize: 1 },
    { ...liveQueryOptions, skip: !can('ISSUE_VIEW') },
  );
  const inProgress = useSearchIssuesQuery(
    { Status: 'IN_PROGRESS', Page: 1, PageSize: 1 },
    { ...liveQueryOptions, skip: !can('ISSUE_VIEW') },
  );
  const resolved = useSearchIssuesQuery(
    { Status: 'DONE', Page: 1, PageSize: 1 },
    { ...liveQueryOptions, skip: !can('ISSUE_VIEW') },
  );
  const workflows = useWorkflowsQuery(
    (projects.data || []).map((p) => p.id),
    { skip: !can('ISSUE_VIEW') || !projects.data },
  );
  const chartStatuses = [
    ...new Set([
      ...statuses,
      ...Object.values(workflows.data || {}).flatMap((w) => w.definition.statuses),
    ]),
  ];
  const statusCounts = useIssueStatusCountsQuery(chartStatuses, {
    ...liveQueryOptions,
    skip: !can('ISSUE_VIEW'),
  });
  const queries = [projects, issues, totals, inProgress, resolved, statusCounts];
  const [runCopilot, { isLoading: isDigestLoading }] = useRunAthennaCopilotMutation();
  const [standupDigest, setStandupDigest] = useState(null);

  const handleGenerateStandup = async () => {
    const assignedList = issues.data?.items || [];
    const inProgList = inProgress.data?.items || [];
    const doneList = resolved.data?.items || [];

    const summaryData = `
    Done in past 24h: ${doneList.map(i => i.summary).slice(0, 5).join('; ') || 'None'}
    In Progress: ${inProgList.map(i => i.summary).slice(0, 5).join('; ') || 'None'}
    Assigned To Me: ${assignedList.map(i => i.summary).slice(0, 5).join('; ') || 'None'}
    `;

    try {
      const res = await runCopilot({
        feature: 'DailyStandupDigest',
        inputText: summaryData,
      }).unwrap();
      if (res?.outputText) {
        setStandupDigest(res.outputText);
      }
    } catch (err) {
      console.error('Failed to generate standup digest:', err);
    }
  };

  function refresh() {
    queries.filter((q) => !q.isUninitialized).forEach((q) => q.refetch());
  }

  const todayFormatted = useMemo(() => {
    try {
      return new Intl.DateTimeFormat('en-US', {
        weekday: 'short',
        month: 'short',
        day: 'numeric',
      }).format(new Date());
    } catch {
      return '';
    }
  }, []);

  return (
    <div className="dashboard-page">
      <PageHeader
        eyebrow="ENGINEERING WORKSPACE"
        title={`Welcome back${session?.user?.name ? `, ${session.user.name.split(' ')[0]}` : ''}!`}
        description="Here’s what’s happening with your work today."
      >
        {todayFormatted && (
          <span className="dashboard-date-chip" aria-label={`Current date: ${todayFormatted}`}>
            <Icon name="clock" />
            {todayFormatted}
          </span>
        )}
        <button onClick={refresh} disabled={queries.some((q) => q.isFetching)}>
          <Icon name="refresh" />
          Refresh metrics
        </button>
        <FeatureGuard feature={ATHENNA}>
          <button
          type="button"
          onClick={handleGenerateStandup}
          disabled={isDigestLoading}
          className="standup-digest-trigger-btn"
        >
          <span className="sparkle-icon">✦</span>
          <span>{isDigestLoading ? 'Generating…' : 'Daily Standup Digest'}</span>
          </button>
        </FeatureGuard>
        <PermissionGuard permission="ISSUE_CREATE">
          <Link className="button primary" to="/issues/create">
            <Icon name="plus" />
            Create issue
          </Link>
        </PermissionGuard>
      </PageHeader>
      <div className="dashboard-workspace">
        <div className="dashboard-main" aria-label="Workspace overview details">
          {standupDigest && (
            <AthennaStandupDigestCard
              digestText={standupDigest}
              onClose={() => setStandupDigest(null)}
              onRegenerate={handleGenerateStandup}
              isRegenerating={isDigestLoading}
              runCopilot={runCopilot}
            />
          )}

          {/* Executive KPI Metric Ribbon */}
          <div className="overview-grid">
            <Metric
              title="Projects"
              icon="folder"
              query={projects}
              enabled={can('PROJECT_VIEW')}
              value={projects.data?.length}
            >
              <Link to="/projects?filters=1">Explore projects →</Link>
            </Metric>
            <Metric
              title="Total tracked issues"
              icon="issues"
              query={totals}
              enabled={can('ISSUE_VIEW')}
              value={totals.data?.totalCount}
            >
              <Link to="/issues?filters=1">View all issues →</Link>
            </Metric>
            <Metric
              title="In progress"
              icon="clock"
              query={inProgress}
              enabled={can('ISSUE_VIEW')}
              value={inProgress.data?.totalCount}
            >
              <Link to="/issues?Status=IN_PROGRESS">In progress →</Link>
            </Metric>
            <Metric
              title="Resolved"
              icon="shield"
              query={resolved}
              enabled={can('ISSUE_VIEW')}
              value={resolved.data?.totalCount}
            >
              <Link to="/issues?Status=DONE">Completed issues →</Link>
            </Metric>
          </div>

          {can('ISSUE_VIEW') && (
            <AdminCharts
              issues={can('ISSUE_VIEW') ? statusCounts : null}
              projects={can('PROJECT_VIEW') ? projects : null}
            />
          )}

          <div className="dashboard-secondary">
            <PermissionGuard permission="PROJECT_VIEW">
              <section
                className="panel dashboard-projects"
                aria-labelledby="dashboard-projects-title"
              >
                <div className="section-header">
                  <div>
                    <h2 id="dashboard-projects-title">Your projects</h2>
                    <p>Continue working across your workspace</p>
                  </div>
                  <Link to="/projects">View all →</Link>
                </div>
                <QueryState query={projects}>
                  {projects.data?.length ? (
                    <div className="table-scroll">
                      <table>
                        <thead>
                          <tr>
                            <th>Project</th>
                            <th>Key</th>
                            <th>Type</th>
                          </tr>
                        </thead>
                        <tbody>
                          {projects.data.slice(0, 5).map((project) => (
                            <tr key={project.id}>
                              <td>
                                <Link to={`/projects/${project.id}`} className="dashboard-project-link">
                                  <span className="project-folder-icon">
                                    <Icon name="folder" />
                                  </span>{' '}
                                  {project.projectName || project.name || project.key}
                                </Link>
                              </td>
                              <td>
                                <span className="project-key-chip">{project.key}</span>
                              </td>
                              <td>
                                <span className="project-type-chip">
                                  {label(project.projectType || 'Unspecified')}
                                </span>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ) : (
                    <EmptyState title="No projects yet">
                      Your accessible projects will appear here.
                    </EmptyState>
                  )}
                </QueryState>
              </section>
            </PermissionGuard>

            {/* Smart Quick Actions Command Tiles */}
            <section
              className="panel dashboard-shortcuts"
              aria-labelledby="dashboard-shortcuts-title"
            >
              <div className="section-header">
                <div>
                  <h2 id="dashboard-shortcuts-title">Quick actions</h2>
                  <p>Jump into your next task</p>
                </div>
              </div>
              <nav aria-label="Dashboard quick actions" className="dashboard-action-grid">
                {[
                  ['ISSUE_CREATE', '/issues/create', 'plus', 'Create issue', 'New bug or task', '#3b82f6'],
                  ['ISSUE_VIEW', '/my-work', 'user', 'My work', 'Active assignments', '#10b981'],
                  ['TIMESHEET_VIEW', '/timesheets/my', 'clock', 'My timesheet', 'Log hours & time', '#f59e0b'],
                  ['PROJECT_VIEW', '/projects', 'folder', 'Browse projects', 'Workspaces portfolio', '#6366f1'],
                ]
                  .filter(([permission]) => can(permission))
                  .map(([permission, to, icon, title, subtitle, color]) => (
                    <Link className="button action-tile" key={permission} to={to}>
                      <div className="action-tile-header">
                        <span className="action-tile-icon" style={{ backgroundColor: `${color}18`, color }}>
                          <Icon name={icon} />
                        </span>
                        <span className="action-tile-arrow" aria-hidden="true">→</span>
                      </div>
                      <div className="action-tile-content">
                        <span className="action-tile-title">{title}</span>
                        <span className="action-tile-sub">{subtitle}</span>
                      </div>
                    </Link>
                  ))}
                {!['ISSUE_CREATE', 'ISSUE_VIEW', 'TIMESHEET_VIEW', 'PROJECT_VIEW'].some(can) && (
                  <p>No shortcuts available for your access.</p>
                )}
              </nav>
            </section>
          </div>

          <div className="dashboard-columns">
            <PermissionGuard permission="ISSUE_VIEW">
              <section className="panel dashboard-issues">
                <div className="section-header">
                  <div>
                    <h2>
                      {issueView === 'assigned'
                        ? 'Assigned to me'
                        : issueView === 'created'
                          ? 'Created by me'
                          : 'Recently updated issues'}
                    </h2>
                    <p>{issueDescription}</p>
                  </div>
                  <Link to={issueWorkspaceUrl}>View all →</Link>
                </div>
                <nav className="dashboard-issue-tabs" aria-label="Issue view">
                  {[
                    ['recent', 'Recent issues'],
                    ['assigned', 'Assigned to me'],
                    ['created', 'Created by me'],
                  ].map(([value, title]) => (
                    <button
                      key={value}
                      aria-pressed={issueView === value}
                      onClick={() => setIssueView(value)}
                    >
                      {title}
                    </button>
                  ))}
                </nav>
                <QueryState query={issues}>
                  <IssueTable key={issueView} issues={issues.data?.items} />
                </QueryState>
                <div className="section-footer">
                  Showing {issues.data?.items.length ?? 0} of {issues.data?.totalCount ?? '—'}{' '}
                  issues
                  <Link to={issueWorkspaceUrl}>Open issue workspace →</Link>
                </div>
              </section>
            </PermissionGuard>
          </div>
        </div>
      </div>
      <LiveStatus query={totals} />
    </div>
  );
}
