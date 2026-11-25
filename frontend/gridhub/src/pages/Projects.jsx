import '../../../src/styles/projects.scss';
import { visibleHierarchy } from '../../../src/features/projects/hierarchy';
import ProjectManageDialog from '../../../src/features/projects/ProjectManageDialog';
import { liveQueryOptions } from '../../../src/services/api/live';
import { useSavedFilters } from '../../../src/utils/useSavedFilters';
import { useState, useMemo } from 'react';
import ProjectCreateDialog from '../../../src/features/projects/ProjectCreateDialog';
import { Icon } from '../../../src/components/Icon';
import ProjectMembers from '../../../src/features/projects/ProjectMembers';
import Link from '../../../src/components/PortalLink';
import { useProjectsQuery } from '../../../src/services/api/api';
import { useAuth } from '../../../src/features/auth/context';
import { PageHeader, QueryState, Badge, DataTable, CustomSelect } from '../../../src/components/common';
import { label, userLabel } from '../../../src/utils/format';

const filterFields = ['Text', 'Account', 'Project', 'Status', 'Type'];

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

const PROJECT_TYPE_STYLES = {
  SOFTWARE: { bg: '#eff6ff', color: '#1d4ed8', border: '#bfdbfe' },
  SERVICE: { bg: '#ecfdf5', color: '#047857', border: '#a7f3d0' },
  BUSINESS: { bg: '#faf5ff', color: '#7c3aed', border: '#e9d5ff' },
  BANKING: { bg: '#fffbeb', color: '#b45309', border: '#fde68a' },
};

export default function Projects() {
  const query = useProjectsQuery(undefined, liveQueryOptions);
  const { can, session } = useAuth();
  const [collapsed, setCollapsed] = useState(new Set());
  const [hierarchy, setHierarchy] = useState(false);
  const [selectedProject, setSelectedProject] = useState(null);
  const [filters, setFilters] = useSavedFilters('projects', filterFields);
  const search = filters.get('Text') || '';
  const account = filters.get('Account') || '';
  const projectKey = filters.get('Project') || '';
  const updateFilter = (field, value) =>
    setFilters({ ...Object.fromEntries(filters), [field]: value });

  const accounts = useMemo(
    () =>
      [...new Set((query.data || []).map((project) => project.name).filter(Boolean))].sort((a, b) =>
        a.localeCompare(b),
      ),
    [query.data],
  );

  const projectKeys = useMemo(
    () =>
      [
        ...new Set(
          (query.data || [])
            .filter((project) => !account || project.name === account)
            .map((project) => project.key)
            .filter(Boolean),
        ),
      ].sort((a, b) => a.localeCompare(b)),
    [query.data, account],
  );

  const status = filters.get('Status') || '';
  const projectType = filters.get('Type') || '';
  const filtered = useMemo(
    () =>
      (query.data || []).filter(
        (project) =>
          (!account || project.name === account) &&
          (!projectKey || project.key === projectKey) &&
          (!status || project.status === status) &&
          (!projectType || project.projectType === projectType) &&
          `${project.key} ${project.name} ${project.description || ''}`
            .toLowerCase()
            .includes(search.trim().toLowerCase()),
      ),
    [query.data, account, projectKey, status, projectType, search],
  );

  const hasFilters = Boolean(search || account || projectKey || status || projectType);
  const [management, setManagement] = useState(null);
  const [creation, setCreation] = useState(null);
  const canCreate = session?.user?.role === 'ADMIN' && can('PROJECT_MANAGE');
  const canManageMembers =
    session?.user?.role === 'ADMIN' && can('PROJECT_MANAGE') && can('USER_VIEW');

  const activeCount = useMemo(
    () => (query.data || []).filter((p) => p.status === 'ACTIVE').length,
    [query.data],
  );

  return (
    <>
      <PageHeader title="Projects" description="Manage accounts and the projects within them.">
        {canCreate && (
          <button className="primary" onClick={() => setCreation({ parent: null })}>
            <Icon name="plus" />
            New project
          </button>
        )}
      </PageHeader>
      <section className="panel projects-panel" aria-label="Project directory">
        <div className="section-header">
          <div className="projects-panel-title">
            <span className="projects-heading-icon">
              <Icon name="folder" />
            </span>
            <div>
              <h2>All projects</h2>
              <p>Projects organized by account portfolio</p>
            </div>
          </div>
          {query.data && (
            <div className="projects-stats-ribbon">
              <span className="projects-count">
                <strong>{query.data.length}</strong> {query.data.length === 1 ? 'project' : 'projects'}
              </span>
              <span className="projects-status-badge">
                {activeCount} active
              </span>
            </div>
          )}
        </div>

        {/* Balanced Filter Toolbar */}
        <div className="project-filters" role="search" aria-label="Filter projects">
          <label className="project-filter-search">
            <span>Search projects or accounts</span>
            <div className="search-input-wrap">
              <Icon name="search" />
              <input
                name="projectSearch"
                type="search"
                value={search}
                onChange={(event) => updateFilter('Text', event.target.value)}
                placeholder="Search project, account, or description…"
                aria-label="Search projects or accounts"
              />
              {search && (
                <button
                  type="button"
                  className="search-clear-inline"
                  onClick={() => updateFilter('Text', '')}
                  aria-label="Clear search text"
                >
                  ×
                </button>
              )}
            </div>
          </label>
          <CustomSelect
            label="Account"
            name="accountFilter"
            value={account}
            placeholder="All accounts"
            allowEmpty={true}
            options={accounts.map((name) => ({ value: name, label: name }))}
            onChange={(event) => {
              const nextAccount = event.target.value;
              setFilters({
                ...Object.fromEntries(filters),
                Account: nextAccount,
                Project: (query.data || []).some(
                  (project) =>
                    project.key === projectKey && (!nextAccount || project.name === nextAccount),
                )
                  ? projectKey
                  : '',
              });
            }}
          />
          <CustomSelect
            label="Project"
            name="projectFilter"
            value={projectKey}
            placeholder="All projects"
            allowEmpty={true}
            options={projectKeys.map((key) => ({ value: key, label: key }))}
            onChange={(event) => updateFilter('Project', event.target.value)}
          />
          <CustomSelect
            label="Status"
            name="statusFilter"
            value={status}
            placeholder="All statuses"
            allowEmpty={true}
            options={[...new Set((query.data || []).map((project) => project.status).filter(Boolean))]
              .sort()
              .map((value) => ({ value, label: label(value) }))}
            onChange={(event) => updateFilter('Status', event.target.value)}
          />
          <CustomSelect
            label="Type"
            name="projectTypeFilter"
            value={projectType}
            placeholder="All types"
            allowEmpty={true}
            options={['SOFTWARE', 'SERVICE', 'BUSINESS', 'BANKING'].map((value) => ({
              value,
              label: label(value),
            }))}
            onChange={(event) => updateFilter('Type', event.target.value)}
          />
          <div className="project-filter-action-wrap">
            <button
              type="button"
              className="project-clear-filters-btn"
              disabled={!hasFilters}
              onClick={() => {
                setFilters({});
              }}
            >
              Clear filters
            </button>
          </div>
        </div>

        {/* View Switcher & Counter Toolbar */}
        <div className="project-toolbar-sub">
          <div className="project-summary-box">
            {query.data && (
              <p className="project-filter-summary" role="status">
                Showing {filtered.length} of {query.data.length} projects
              </p>
            )}
            {hasFilters && (
              <span className="project-active-filter-badge">
                Filtered view
              </span>
            )}
          </div>
          <div className="view-switch project-view-switch" role="group" aria-label="Project view">
            <button
              className={`view-toggle-btn ${!hierarchy ? 'active' : ''}`}
              aria-pressed={!hierarchy}
              onClick={() => setHierarchy(false)}
            >
              <Icon name="dashboard" />
              List
            </button>
            <button
              className={`view-toggle-btn ${hierarchy ? 'active' : ''}`}
              aria-pressed={hierarchy}
              onClick={() => setHierarchy(true)}
            >
              <Icon name="folder" />
              Hierarchy
            </button>
          </div>
        </div>

        {hierarchy && (
          <p className="project-filter-summary" style={{ padding: '8px 24px 12px' }}>
            Projects are grouped beneath their accessible parents. Filters show matching projects
            only.
          </p>
        )}

        <QueryState query={query}>
          <DataTable
            rows={
              hierarchy ? visibleHierarchy(filtered, hasFilters ? new Set() : collapsed) : filtered
            }
            renderExpandedRow={(p) =>
              canManageMembers &&
              selectedProject?.id === p.id && (
                <div id={`project-members-${p.id}`}>
                  <ProjectMembers
                    project={p}
                    onClose={() => {
                      setSelectedProject(null);
                      document.getElementById(`project-members-toggle-${p.id}`)?.focus();
                    }}
                  />
                </div>
              )
            }
            empty={hasFilters ? 'No projects match your filters' : 'No projects available'}
            emptyVariant="projects"
            columns={[
              {
                key: 'name',
                title: 'Account',
                render: (p) => {
                  const initials =
                    p.name
                      ?.trim()
                      .split(/\s+/)
                      .map((word) => word[0])
                      .join('')
                      .toUpperCase() || '—';
                  const palette = getAvatarPalette(p.name || '');

                  return (
                    <abbr
                      className="account-short"
                      title={p.name}
                      aria-label={p.name}
                      style={{
                        background: palette.bg,
                        color: palette.color,
                        boxShadow: `0 2px 6px ${palette.shadow}`,
                        border: 'none',
                      }}
                    >
                      {initials}
                    </abbr>
                  );
                },
              },
              {
                key: 'key',
                title: 'Project',
                render: (p) => (
                  <div
                    className="project-identity"
                    style={
                      hierarchy
                        ? { paddingInlineStart: `${Math.min(p.hierarchyDepth, 8) * 20}px` }
                        : undefined
                    }
                  >
                    <div>
                      {hierarchy &&
                        !hasFilters &&
                        (query.data || []).some(
                          (child) => String(child.parentProjectId) === String(p.id),
                        ) && (
                          <button
                            className="hierarchy-toggle"
                            aria-label={`${collapsed.has(String(p.id)) ? 'Expand' : 'Collapse'} ${p.key}`}
                            aria-expanded={!collapsed.has(String(p.id))}
                            onClick={() =>
                              setCollapsed((previous) => {
                                const next = new Set(previous);
                                if (next.has(String(p.id))) next.delete(String(p.id));
                                else next.add(String(p.id));
                                return next;
                              })
                            }
                          >
                            {collapsed.has(String(p.id)) ? '▸' : '▾'}
                          </button>
                        )}
                      <Link className="project-overview-link" to={`/projects/${p.id}`}>
                        {hierarchy && p.hierarchyDepth > 0 ? '↳ ' : ''}
                        <strong>{p.key}</strong>
                      </Link>
                      {hierarchy && <p className="cell-description">{p.name}</p>}
                      {p.description && <p className="cell-description">{p.description}</p>}
                      <p className="project-parent">
                        {p.parentProjectId == null
                          ? 'Top-level project'
                          : `Parent: ${query.data?.find((parent) => String(parent.id) === String(p.parentProjectId))?.name || `Project #${p.parentProjectId}`}`}
                      </p>
                    </div>
                  </div>
                ),
              },
              {
                key: 'projectType',
                title: 'Type',
                render: (p) => {
                  const type = (p.projectType || '').toUpperCase();
                  const style = PROJECT_TYPE_STYLES[type] || {
                    bg: '#f1f5f9',
                    color: '#475569',
                    border: '#cbd5e1',
                  };
                  return (
                    <span
                      className="project-type"
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        padding: '2px 9px',
                        borderRadius: '12px',
                        fontSize: '11px',
                        fontWeight: 500,
                        backgroundColor: style.bg,
                        color: style.color,
                        borderColor: style.border,
                        borderWidth: '1px',
                        borderStyle: 'solid',
                        textTransform: 'capitalize',
                      }}
                    >
                      {label(p.projectType)}
                    </span>
                  );
                },
              },
              { key: 'status', title: 'Status', render: (p) => <Badge value={p.status} /> },
              {
                key: 'projectLeadUserId',
                title: 'Lead',
                render: (p) => (
                  <span className={`project-lead ${p.projectLeadUserId ? 'assigned' : 'unassigned'}`}>
                    <Icon name="user" />
                    {userLabel(p.projectLeadUserId)}
                  </span>
                ),
              },
              {
                key: 'actions',
                title: 'Actions',
                render: (p) => (
                  <div className="project-actions">
                    <div className="project-primary-actions">
                      {can('ISSUE_VIEW') && (
                        <Link className="button project-open" to={`/issues?ProjectId=${p.id}`}>
                          View issues →
                        </Link>
                      )}
                      {canManageMembers && (
                        <button
                          className="project-members-action"
                          id={`project-members-toggle-${p.id}`}
                          aria-controls={
                            selectedProject?.id === p.id ? `project-members-${p.id}` : undefined
                          }
                          type="button"
                          aria-expanded={selectedProject?.id === p.id}
                          onClick={() =>
                            setSelectedProject(selectedProject?.id === p.id ? null : p)
                          }
                        >
                          <Icon name="user" />
                          Members
                          <span className="members-chevron" aria-hidden="true">
                            ⌄
                          </span>
                        </button>
                      )}
                    </div>
                    <div className="project-secondary-actions">
                      {can('AUDIT_VIEW') && (
                        <Link className="button" to={`/audit?EntityType=PROJECT&EntityId=${p.id}`}>
                          History
                        </Link>
                      )}
                      {canCreate && (
                        <button onClick={() => setManagement({ project: p, mode: 'edit' })}>
                          Edit
                        </button>
                      )}
                      {canCreate && (
                        <button onClick={() => setManagement({ project: p, mode: 'move' })}>
                          Move
                        </button>
                      )}
                      {session.user.role === 'ADMIN' && can('PROJECT_DELETE') && (
                        <button
                          className="project-delete-action"
                          onClick={() => setManagement({ project: p, mode: 'delete' })}
                        >
                          Delete
                        </button>
                      )}
                    </div>
                  </div>
                ),
              },
            ]}
          />
        </QueryState>
      </section>
      {management && (
        <ProjectManageDialog
          key={`${management.project.id}:${management.mode}`}
          {...management}
          projects={query.data || []}
          close={() => setManagement(null)}
        />
      )}
      {canCreate && creation && (
        <ProjectCreateDialog
          projects={query.data || []}
          parent={creation.parent}
          close={() => setCreation(null)}
        />
      )}
    </>
  );
}

