import '../../../src/styles/accounts.scss';
import { Icon } from '../../../src/components/Icon';
import { useState, useMemo } from 'react';
import Link from '../../../src/components/PortalLink';
import {
  useAccountsQuery,
  useProjectsQuery,
  useSearchIssuesQuery,
  useCreateAccountMutation,
  useUpdateAccountMutation,
  useDeleteAccountMutation,
} from '../../../src/services/api/api';
import { useAuth } from '../../../src/features/auth/context';
import { PageHeader, QueryState, ErrorState, Pagination } from '../../../src/components/common';

const AVATAR_PALETTES = [
  { bg: 'linear-gradient(135deg, #4f46e5 0%, #3730a3 100%)', color: '#ffffff', shadow: 'rgba(79, 70, 229, 0.28)' },
  { bg: 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)', color: '#ffffff', shadow: 'rgba(2, 132, 199, 0.28)' },
  { bg: 'linear-gradient(135deg, #0d9488 0%, #0f766e 100%)', color: '#ffffff', shadow: 'rgba(13, 148, 136, 0.28)' },
  { bg: 'linear-gradient(135deg, #7c3aed 0%, #6d28d9 100%)', color: '#ffffff', shadow: 'rgba(124, 58, 237, 0.28)' },
  { bg: 'linear-gradient(135deg, #d97706 0%, #b45309 100%)', color: '#ffffff', shadow: 'rgba(217, 119, 6, 0.28)' },
  { bg: 'linear-gradient(135deg, #e11d48 0%, #be123c 100%)', color: '#ffffff', shadow: 'rgba(225, 29, 72, 0.28)' },
  { bg: 'linear-gradient(135deg, #059669 0%, #047857 100%)', color: '#ffffff', shadow: 'rgba(5, 150, 105, 0.28)' },
];

function getAvatarPalette(name = '') {
  let hash = 0;
  for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
  const index = Math.abs(hash) % AVATAR_PALETTES.length;
  return AVATAR_PALETTES[index];
}

const ISSUE_TYPE_STYLES = {
  BUG: { bg: '#fef2f2', color: '#dc2626', border: '#fecaca' },
  STORY: { bg: '#ecfdf5', color: '#059669', border: '#a7f3d0' },
  TASK: { bg: '#eff6ff', color: '#2563eb', border: '#bfdbfe' },
  EPIC: { bg: '#faf5ff', color: '#7c3aed', border: '#e9d5ff' },
  SUBTASK: { bg: '#f8fafc', color: '#475569', border: '#e2e8f0' },
};

function IssueBranch({ projectId, parentId, ancestors = [] }) {
  const [page, setPage] = useState(1);
  const query = useSearchIssuesQuery({
    ProjectId: projectId,
    ...(parentId ? { ParentIssueId: parentId } : { RootOnly: true }),
    Page: page,
    PageSize: 20,
  });

  return (
    <QueryState query={query}>
      <ul className="account-tree">
        {query.data?.items?.map((issue) => {
          const typeStyle = ISSUE_TYPE_STYLES[issue.issueType?.toUpperCase()] || {
            bg: '#f8fafc',
            color: '#475569',
            border: '#e2e8f0',
          };

          return (
            <li className="account-issue" key={issue.id}>
              <div className="account-issue-row">
                <Link to={`/issues/${issue.id}`} className="account-issue-link">
                  {issue.issueKey}: {issue.summary}
                </Link>{' '}
                <small
                  className="account-type"
                  style={{
                    backgroundColor: typeStyle.bg,
                    color: typeStyle.color,
                    borderColor: typeStyle.border,
                  }}
                >
                  {issue.issueType}
                </small>
              </div>
              {!ancestors.includes(issue.id) && issue.issueType !== 'SUBTASK' && (
                <details className="account-child-details">
                  <summary>Child issues</summary>
                  <LazyChildren
                    projectId={projectId}
                    parentId={issue.id}
                    ancestors={[...ancestors, issue.id]}
                  />
                </details>
              )}
            </li>
          );
        })}
      </ul>
      {query.data?.items?.length === 0 && (
        <p className="account-empty">No accessible issues in this branch yet.</p>
      )}
      {query.data?.totalPages > 1 && (
        <Pagination data={query.data} busy={query.isFetching} onPage={setPage} />
      )}
    </QueryState>
  );
}

function LazyChildren(props) {
  const [opened, setOpened] = useState(false);
  return opened ? (
    <IssueBranch {...props} />
  ) : (
    <button className="account-load" onClick={() => setOpened(true)}>
      <Icon name="issues" />
      Load issues
    </button>
  );
}

function ProjectBranch({ project, projects, seen = [], forceExpand }) {
  if (seen.includes(project.id)) return null;
  const childProjects = projects.filter((p) => p.parentProjectId === project.id);

  return (
    <li className="account-project">
      <details open={forceExpand !== undefined ? forceExpand : undefined}>
        <summary>
          <span className="account-folder">
            <Icon name="folder" />
          </span>
          <span className="account-project-info">
            <span className="account-project-name">{project.projectName || project.key}</span>
            <small className="account-project-meta">
              Project · {project.key}
            </small>
          </span>
          <div className="account-project-quick-actions" onClick={(e) => e.stopPropagation()}>
            <Link
              className="account-open-link"
              to={`/projects/${project.id}`}
              title={`Open ${project.projectName || project.key}`}
            >
              Open project <span aria-hidden="true">↗</span>
            </Link>
          </div>
          <span className="account-chevron" aria-hidden="true">
            ›
          </span>
        </summary>
        <div className="account-project-content">
          <div className="account-project-content-toolbar">
            <Link className="account-open" to={`/projects/${project.id}`}>
              Open project <span aria-hidden="true">↗</span>
            </Link>
          </div>
          <LazyChildren projectId={project.id} />
          {childProjects.length > 0 && (
            <ul className="account-tree account-subprojects-tree">
              {childProjects.map((p) => (
                <ProjectBranch
                  key={p.id}
                  project={p}
                  projects={projects}
                  seen={[...seen, project.id]}
                  forceExpand={forceExpand}
                />
              ))}
            </ul>
          )}
        </div>
      </details>
    </li>
  );
}

function AccountEditor({ account, done }) {
  const [create, creating] = useCreateAccountMutation();
  const [update, updating] = useUpdateAccountMutation();
  const [remove, removing] = useDeleteAccountMutation();
  const [error, setError] = useState(null);
  const [confirmed, confirm] = useState(false);
  const { can } = useAuth();
  const busy = creating.isLoading || updating.isLoading || removing.isLoading;

  async function save(e) {
    e.preventDefault();
    setError(null);
    const form = new FormData(e.currentTarget);
    const body = { name: form.get('name').trim(), status: form.get('status') };
    try {
      await (
        account ? update({ id: account.id, version: account.version, ...body }) : create(body)
      ).unwrap();
      done();
    } catch (failure) {
      setError(failure);
    }
  }

  return (
    <form
      onSubmit={save}
      className="panel account-editor"
      aria-label={account ? 'Edit account' : 'Create account'}
    >
      <div className="account-editor-heading">
        <span className="account-avatar">
          <Icon name={account ? 'settings' : 'plus'} />
        </span>
        <div>
          <h3>{account ? 'Manage account' : 'New account'}</h3>
          <p>
            {account
              ? 'Update account configuration and manage lifecycle status.'
              : 'Give your projects a home. Create an account to group related work and client portfolios.'}
          </p>
        </div>
      </div>
      <fieldset disabled={busy}>
        <div className="account-form-grid">
          <label>
            Account name
            <input
              name="name"
              required
              maxLength={200}
              placeholder="e.g. Green Dot"
              defaultValue={account?.name || ''}
            />
          </label>
          <label>
            Status
            <select name="status" defaultValue={account?.status || 'ACTIVE'}>
              <option value="ACTIVE">Active</option>
              <option value="ARCHIVED">Archived</option>
            </select>
          </label>
        </div>
        <p className="account-form-note">
          <Icon name="shield" />
          Archived accounts remain readable. New projects cannot be added to them.
        </p>
        <div className="account-form-actions">
          <button type="submit" className="primary">
            {busy ? 'Saving…' : 'Save account'}
          </button>{' '}
          <button type="button" onClick={done}>
            Cancel
          </button>
        </div>
        {account && can('PROJECT_DELETE') && (
          <div className="account-danger">
            <label>
              <input
                type="checkbox"
                checked={confirmed}
                onChange={(e) => confirm(e.target.checked)}
              />
              Confirm deletion of this empty account
            </label>
            <button
              type="button"
              className="account-delete"
              disabled={!confirmed || account.projectCount > 0}
              onClick={async () => {
                try {
                  await remove(account).unwrap();
                  done();
                } catch (failure) {
                  setError(failure);
                }
              }}
            >
              Delete account
            </button>
          </div>
        )}
        <ErrorState error={error} />
      </fieldset>
    </form>
  );
}

export default function Accounts() {
  const accounts = useAccountsQuery();
  const projects = useProjectsQuery();
  const { session, can } = useAuth();
  const [editing, setEditing] = useState(undefined);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('ALL');
  const [forceExpand, setForceExpand] = useState(undefined);

  const activeAccountsCount = useMemo(
    () => (accounts.data || []).filter((a) => a.status === 'ACTIVE').length,
    [accounts.data],
  );
  const archivedAccountsCount = useMemo(
    () => (accounts.data || []).filter((a) => a.status === 'ARCHIVED').length,
    [accounts.data],
  );

  const filtered = useMemo(
    () =>
      (accounts.data || []).filter(
        (a) =>
          a.name.toLowerCase().includes(search.trim().toLowerCase()) &&
          (status === 'ALL' || a.status === status),
      ),
    [accounts.data, search, status],
  );

  const admin = session?.user?.role === 'ADMIN' && can('PROJECT_MANAGE');

  return (
    <div className="accounts-workspace">
      <PageHeader
        title="Accounts & hierarchy"
        description="Organize your accounts, explore projects, and follow the work."
      >
        {admin && (
          <button className="primary" onClick={() => setEditing(null)}>
            <Icon name="plus" />
            New account
          </button>
        )}{' '}
        <Link className="button" to="/projects">
          Manage projects <span aria-hidden="true">↗</span>
        </Link>
      </PageHeader>

      {/* Modern Workspace Hierarchy & Insights Banner */}
      <div className="account-overview" aria-label="Workspace overview">
        <div className="account-overview-metrics">
          <div className="overview-metric-item">
            <span className="account-avatar metric-avatar-amber">
              <Icon name="folder" />
            </span>
            <div className="metric-info">
              <span className="metric-count-line">
                <strong>{accounts.data?.length ?? '—'}</strong>
                <span className="metric-title">Accounts</span>
              </span>
              <span className="metric-caption">
                {activeAccountsCount} active · {archivedAccountsCount} archived
              </span>
            </div>
          </div>

          <div className="overview-divider" aria-hidden="true" />

          <div className="overview-metric-item">
            <span className="account-avatar metric-avatar-blue">
              <Icon name="dashboard" />
            </span>
            <div className="metric-info">
              <span className="metric-count-line">
                <strong>{projects.data?.length ?? '—'}</strong>
                <span className="metric-title">Accessible projects</span>
              </span>
              <span className="metric-caption">Workspaces & boards</span>
            </div>
          </div>
        </div>

        {/* Hierarchy Breadcrumb Stepper */}
        <div className="account-hierarchy-stepper">
          <div className="account-hierarchy-crumb">
            <span className="hierarchy-step">
              <span className="step-num">1</span> Organization
            </span>
            <span className="hierarchy-sep" aria-hidden="true">›</span>
            <span className="hierarchy-step active-step">
              <span className="step-num">2</span> Accounts
            </span>
            <span className="hierarchy-sep" aria-hidden="true">›</span>
            <span className="hierarchy-step">
              <span className="step-num">3</span> Projects
            </span>
            <span className="hierarchy-sep" aria-hidden="true">›</span>
            <span className="hierarchy-step">
              <span className="step-num">4</span> Issues
            </span>
          </div>
          <span className="hierarchy-helper-text">
            Enterprise portfolio structure
          </span>
        </div>
      </div>

      {editing !== undefined && (
        <AccountEditor
          key={editing?.id || 'new'}
          account={editing}
          done={() => setEditing(undefined)}
        />
      )}

      <QueryState query={accounts}>
        <QueryState query={projects}>
          {!accounts.data?.length && (
            <div className="panel account-empty-onboarding">
              <div className="empty-onboarding-icon">
                <Icon name="folder" />
              </div>
              <h3>No accounts created yet</h3>
              <p>Create client portfolios or organization accounts to structure your projects.</p>
              {admin && (
                <button className="primary" onClick={() => setEditing(null)}>
                  <Icon name="plus" />
                  Create first account
                </button>
              )}
            </div>
          )}

          <div className="account-directory-heading">
            <div className="directory-header-meta">
              <div className="directory-title-line">
                <h2>Account directory</h2>
                <span className="directory-badge">
                  {filtered.length} {filtered.length === 1 ? 'account' : 'accounts'}
                </span>
              </div>
              <p>Expand a project to explore its issues and subprojects.</p>
            </div>

            <div className="account-filters">
              <label className="account-search">
                <Icon name="search" />
                <input
                  aria-label="Search accounts"
                  placeholder="Search accounts…"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
                {search && (
                  <button
                    type="button"
                    className="search-clear-inline"
                    onClick={() => setSearch('')}
                    aria-label="Clear search"
                  >
                    ×
                  </button>
                )}
              </label>

              <select
                aria-label="Filter account status"
                value={status}
                onChange={(e) => setStatus(e.target.value)}
                className="account-status-dropdown"
              >
                <option value="ALL">All statuses</option>
                <option value="ACTIVE">Active</option>
                <option value="ARCHIVED">Archived</option>
              </select>

              <button
                type="button"
                className="account-tree-toggle"
                onClick={() => setForceExpand((prev) => !prev)}
                title={forceExpand ? 'Collapse all project branches' : 'Expand all project branches'}
              >
                {forceExpand ? 'Collapse all' : 'Expand all'}
              </button>
            </div>
          </div>

          {accounts.data?.length > 0 && !filtered.length && (
            <div className="panel account-empty">
              No accounts match your filters.{' '}
              <button
                type="button"
                className="clear-filter-btn"
                onClick={() => {
                  setSearch('');
                  setStatus('ALL');
                }}
              >
                Clear filters
              </button>
            </div>
          )}

          {filtered.map((account) => {
            const list = (projects.data || []).filter((p) => p.accountId === account.id);
            const palette = getAvatarPalette(account.name);

            return (
              <section className="panel account-card" key={account.id}>
                <div className="account-card-heading">
                  <span
                    className="account-avatar"
                    style={{
                      background: palette.bg,
                      color: palette.color,
                      boxShadow: `0 4px 12px ${palette.shadow}`,
                      border: 'none',
                    }}
                  >
                    {account.name.slice(0, 2).toUpperCase()}
                  </span>
                  <div className="account-card-title">
                    <h2>
                      {account.name}
                      <span
                        className={`account-status ${account.status === 'ARCHIVED' ? 'archived' : ''}`}
                      >
                        {account.status === 'ARCHIVED' ? 'Archived' : 'Active'}
                      </span>
                    </h2>
                    <p>
                      <span className="account-project-counter">
                        {list.length} {list.length === 1 ? 'project' : 'projects'}
                      </span>
                      <span>·</span> Account
                    </p>
                  </div>
                  {admin && (
                    <button className="account-manage" onClick={() => setEditing(account)}>
                      <Icon name="settings" />
                      Manage account
                    </button>
                  )}
                </div>

                {!list.length && (
                  <div className="account-no-projects">
                    <p className="account-empty">No accessible projects in this account yet.</p>
                  </div>
                )}

                <ul className="account-tree">
                  {list
                    .filter((p) => !list.some((parent) => parent.id === p.parentProjectId))
                    .map((p) => (
                      <ProjectBranch
                        key={p.id}
                        project={p}
                        projects={list}
                        forceExpand={forceExpand}
                      />
                    ))}
                </ul>
              </section>
            );
          })}
        </QueryState>
      </QueryState>
    </div>
  );
}
