import { useWorkflowsQuery } from '../../../src/services/api/collaboration';
import { LiveStatus } from '../../../src/components/LiveStatus';
import { liveQueryOptions } from '../../../src/services/api/live';
import { IssueTable } from '../../../src/features/issues/IssueTable';
import { useSavedFilters } from '../../../src/utils/useSavedFilters';
import IssueBoard from '../../../src/features/issues/IssueBoard';
import Link from '../../../src/components/PortalLink';
import { useSearchIssuesQuery, useProjectsQuery } from '../../../src/services/api/api';
import { PermissionGuard } from '../../../src/features/auth/AuthProvider';
import { useAuth } from '../../../src/features/auth/context';
import { PageHeader, QueryState, Field, SelectField, Pagination } from '../../../src/components/common';
import { IssueList } from '../../../src/features/issues/IssueList';
import IssueDetails from '../../../src/pages/IssueDetails';
import { label } from '../../../src/utils/format';
import { Icon } from '../../../src/components/Icon';
import { issueTypes, priorities, statuses, sortFields } from '../../../src/features/issues/contracts';
import { formValues } from '../../../src/utils/format';
const allowed = [
  'ProjectId',
  'IssueKey',
  'IssueType',
  'Status',
  'Priority',
  'AssigneeUserId',
  'ReporterUserId',
  'Text',
  'Label',
  'ComponentId',
  'FixVersionId',
  'AffectsVersionId',
  'Page',
  'PageSize',
  'SortBy',
  'SortDirection',
];
const savedFields = [...allowed.filter((key) => key !== 'Page'), 'View'];
export default function Issues() {
  const [search, setSearch] = useSavedFilters('issues', savedFields);
  const { can } = useAuth();
  const params = Object.fromEntries([...search].filter(([key]) => allowed.includes(key)));
  const query = useSearchIssuesQuery({ Page: 1, PageSize: 20, ...params }, liveQueryOptions);
  const projects = useProjectsQuery(undefined, { skip: !can('PROJECT_VIEW') });
  const workflows = useWorkflowsQuery(
    (projects.data || []).map((p) => p.id),
    { skip: !projects.data },
  );
  const availableStatuses = [
    ...new Set([
      ...statuses,
      ...Object.entries(workflows.data || {})
        .filter(([id]) => !params.ProjectId || String(id) === String(params.ProjectId))
        .flatMap(([, w]) => w.definition.statuses),
      ...(query.data?.items || []).map((i) => i.statusCode),
    ]),
  ];
  const selected = search.get('selected') || query.data?.items?.[0]?.id;
  function apply(e) {
    e.preventDefault();
    setSearch(
      Object.fromEntries(
        Object.entries({
          ...formValues(e.currentTarget),
          View: search.get('View') || 'list',
          Page: 1,
        }).filter(([, value]) => value !== ''),
      ),
    );
  }
  return (
    <>
      <PageHeader title="Issues" description="Find, prioritize, and move your team’s work forward.">
        <PermissionGuard permission="ISSUE_CREATE">
          <Link className="button primary" to="/issues/create">
            ＋ Create issue
          </Link>
        </PermissionGuard>
      </PageHeader>
      <div className="issues-toolbar">
        <div className="view-switch" aria-label="Issue view">
          <button
            type="button"
            aria-pressed={!['board', 'table'].includes(search.get('View'))}
            onClick={() => setSearch({ ...params, View: 'list' })}
          >
            <Icon name="menu" />
            List view
          </button>
          <button
            type="button"
            aria-pressed={search.get('View') === 'board'}
            onClick={() => setSearch({ ...params, View: 'board' })}
          >
            <Icon name="dashboard" />
            Board view
          </button>
          <button
            type="button"
            aria-pressed={search.get('View') === 'table'}
            onClick={() => setSearch({ ...params, View: 'table' })}
          >
            <Icon name="folder" />
            Table view
          </button>
        </div>
        <LiveStatus query={query} refresh={query.refetch} />
      </div>
      <section className="panel issue-workspace">
        <form key={JSON.stringify(params)} className="filters" onSubmit={apply}>
          <Field
            label="Search issues"
            name="Text"
            type="search"
            placeholder="Search summary or description…"
            defaultValue={params.Text}
          />
          <SelectField
            label="Project"
            name="ProjectId"
            empty="All projects"
            values={(projects.data || []).map((p) => ({
              value: p.id,
              label: `${p.key} · ${p.name}`,
            }))}
            defaultValue={params.ProjectId || ''}
          />
          <SelectField
            label="Status"
            name="Status"
            empty="All statuses"
            values={availableStatuses}
            defaultValue={params.Status || ''}
          />
          <SelectField
            label="Priority"
            name="Priority"
            empty="All priorities"
            values={priorities}
            defaultValue={params.Priority || ''}
          />
          <details className="advanced-filters">
            <summary>More filters</summary>
            <div className="form-grid">
              <Field label="Label" name="Label" defaultValue={params.Label} />
              <Field
                label="Component ID"
                name="ComponentId"
                type="number"
                min="1"
                defaultValue={params.ComponentId}
              />
              <Field
                label="Fix version ID"
                name="FixVersionId"
                type="number"
                min="1"
                defaultValue={params.FixVersionId}
              />
              <Field
                label="Affects version ID"
                name="AffectsVersionId"
                type="number"
                min="1"
                defaultValue={params.AffectsVersionId}
              />
              <Field label="Issue key" name="IssueKey" defaultValue={params.IssueKey} />
              <SelectField
                label="Type"
                name="IssueType"
                empty="All types"
                values={issueTypes}
                defaultValue={params.IssueType || ''}
              />
              <Field
                label="Assignee user ID"
                type="number"
                min="1"
                name="AssigneeUserId"
                defaultValue={params.AssigneeUserId}
              />
              <Field
                label="Reporter user ID"
                type="number"
                min="1"
                name="ReporterUserId"
                defaultValue={params.ReporterUserId}
              />
              <SelectField
                label="Sort by"
                name="SortBy"
                values={sortFields}
                defaultValue={params.SortBy || 'issueNumber'}
              />
              <SelectField
                label="Direction"
                name="SortDirection"
                values={['asc', 'desc']}
                defaultValue={params.SortDirection || 'asc'}
              />
              <SelectField
                label="Results per page"
                name="PageSize"
                values={['20', '50', '100']}
                defaultValue={params.PageSize || '20'}
              />
            </div>
          </details>
          <div className="actions">
            <button className="primary" disabled={query.isFetching}>
              Apply filters
            </button>
            <button type="button" onClick={() => setSearch({})}>
              Reset
            </button>
          </div>
        </form>
        <div className="status-tabs" aria-label="Filter by issue status">
          {['', ...availableStatuses].map((status) => (
            <button
              key={status}
              aria-pressed={(params.Status || '') === status}
              onClick={() =>
                setSearch({
                  ...params,
                  View: search.get('View') || 'list',
                  Status: status,
                  Page: 1,
                })
              }
            >
              {status ? label(status) : 'All states'}
            </button>
          ))}
        </div>
        {search.get('View') === 'table' ? (
          <QueryState query={query}>
            <IssueTable key={JSON.stringify(params)} issues={query.data?.items} manage />
            <Pagination
              data={query.data}
              busy={query.isFetching}
              onPage={(Page) => setSearch({ ...params, View: 'table', Page })}
            />
          </QueryState>
        ) : search.get('View') === 'board' ? (
          <QueryState query={query}>
            <IssueBoard issues={query.data?.items || []} busy={query.isFetching} />
            <Pagination
              data={query.data}
              busy={query.isFetching}
              onPage={(Page) => setSearch({ ...params, View: 'board', Page })}
            />
          </QueryState>
        ) : (
          <div className="issue-split">
            <div className="issue-list-pane">
              <div className="section-header">
                <span className="eyebrow">Issue queue</span>
                <span className="muted">{query.data?.totalCount ?? '—'} results</span>
              </div>
              <QueryState query={query}>
                <IssueList
                  issues={query.data?.items}
                  selected={selected}
                  select={(id) => setSearch({ ...params, selected: String(id) })}
                />
                <Pagination
                  data={query.data}
                  busy={query.isFetching}
                  onPage={(Page) => setSearch({ ...params, Page })}
                />
              </QueryState>
            </div>
            <div className="issue-inspector" aria-label="Selected issue details">
              {selected ? (
                <IssueDetails key={selected} selectedId={selected} />
              ) : (
                <div className="empty">
                  <div className="empty-inspector-illustration" aria-hidden="true">
                    <svg width="48" height="48" viewBox="0 0 48 48" fill="none">
                      <rect x="8" y="6" width="32" height="36" rx="6" fill="#f8fafc" stroke="#cbd5e1" strokeWidth="2" />
                      <path d="M16 16h16M16 22h10M16 28h12" stroke="#94a3b8" strokeWidth="2" strokeLinecap="round" />
                      <circle cx="34" cy="32" r="8" fill="#fef3c7" stroke="#f59e0b" strokeWidth="2" />
                      <path d="M31.5 32l1.5 1.5 3.5-3.5" stroke="#b45309" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </div>
                  <h2>Select an issue</h2>
                  <p>Choose an issue from the queue on the left to see its details and activity.</p>
                  <span className="empty-hint">Tip: Click any issue row to view full context</span>
                </div>
              )}
            </div>
          </div>
        )}
      </section>
    </>
  );
}
