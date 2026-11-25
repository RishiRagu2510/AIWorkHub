import { LiveStatus } from '../../../src/components/LiveStatus';
import { liveQueryOptions } from '../../../src/services/api/live';
import { useSearchParams } from 'react-router';
import { useAuditQuery } from '../../../src/services/api/api';
import { PageHeader, QueryState, Field, Pagination, SelectField } from '../../../src/components/common';
import { AuditTable } from '../../../src/features/audit/AuditTable';
import { Icon } from '../../../src/components/Icon';
import { formValues } from '../../../src/utils/format';
const keys = [
  'EntityType',
  'EntityId',
  'Action',
  'ActorUserId',
  'Source',
  'From',
  'To',
  'Page',
  'PageSize',
];
export default function AuditLogs() {
  const [search, setSearch] = useSearchParams();
  const params = Object.fromEntries([...search].filter(([key]) => keys.includes(key)));
  const query = useAuditQuery({ Page: 1, PageSize: 20, ...params }, liveQueryOptions);
  function apply(e) {
    e.preventDefault();
    const values = formValues(e.currentTarget);
    if (values.From && values.To && values.From > values.To) {
      e.currentTarget.elements.To.setCustomValidity('End time must be after start time.');
      e.currentTarget.reportValidity();
      return;
    }
    for (const key of ['From', 'To'])
      if (values[key]) values[key] = new Date(values[key]).toISOString();
    setSearch(
      Object.fromEntries(Object.entries({ ...values, Page: 1 }).filter(([, v]) => v !== '')),
    );
  }
  function localTime(value) {
    if (!value) return '';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
  }

  function setEntityTypeFilter(type) {
    const next = { ...params, Page: 1 };
    if (type) {
      next.EntityType = type;
    } else {
      delete next.EntityType;
    }
    setSearch(Object.fromEntries(Object.entries(next).filter(([, v]) => v !== '')));
  }

  const activeFilterCount = Object.entries(params).filter(
    ([k, v]) => k !== 'Page' && k !== 'PageSize' && v !== '',
  ).length;

  const totalEvents = query.data?.totalCount ?? 0;
  const currentPage = query.data?.page ?? 1;
  const pageSize = Number(params.PageSize || 20);
  const totalPages = Math.max(1, Math.ceil(totalEvents / pageSize));
  const pageEventsCount = query.data?.items?.length ?? 0;

  return (
    <div className="audit-page">
      <div className="access-banner">
        <span className="banner-icon">
          <Icon name="shield" />
        </span>
        <div className="banner-content">
          <strong>Audit viewer access</strong>
          <span>Review activity and inspect changes across your workspace.</span>
        </div>
        <span className="badge badge-active">Read-only view</span>
      </div>
      <PageHeader
        eyebrow="SECURITY & TRACEABILITY"
        title="Audit logs"
        description="A traceable record of activity across your workspace."
      >
        <button onClick={query.refetch} disabled={query.isFetching}>
          Refresh activity
        </button>
      </PageHeader>
      <LiveStatus query={query} />
      {params.EntityType === 'PROJECT' && (
        <p className="hint">
          Project history appears after event processing. Recent changes may take a moment to
          arrive; no historical backfill is available. Inherited access changes are recorded against
          the source project.
        </p>
      )}

      {/* Security & Audit Anomaly Alerts */}
      {query.data?.items?.some((i) => (i.action || '').toUpperCase().includes('DELETE') || (i.action || '').toUpperCase().includes('FAILED')) && (
        <div style={{
          margin: '0 0 16px 0',
          padding: '12px 16px',
          borderRadius: '8px',
          background: '#fff1f2',
          border: '1px solid #fecdd3',
          color: '#9f1239',
          fontSize: '0.86rem',
          display: 'flex',
          alignItems: 'center',
          gap: '10px'
        }}>
          <span style={{ fontSize: '1.2rem' }}>🛡️</span>
          <div>
            <strong>Security & Audit Alert Detected:</strong> Potential anomalous activity identified (destructive mutations or failed authorization events recorded in recent log entries).
          </div>
        </div>
      )}
      <div className="audit-summary">
        <div className="audit-kpi-card kpi-card-emerald">
          <div className="kpi-card-header">
            <span className="eyebrow">Matching events</span>
            <span className="kpi-icon-pill icon-emerald">
              <Icon name="shield" />
            </span>
          </div>
          <strong>{query.data?.totalCount ?? '—'}</strong>
          <span className="kpi-sub">Total records across workspace</span>
        </div>
        <div className="audit-kpi-card kpi-card-blue">
          <div className="kpi-card-header">
            <span className="eyebrow">Current page</span>
            <span className="kpi-icon-pill icon-blue">
              <Icon name="dashboard" />
            </span>
          </div>
          <strong>{query.data?.page ?? '—'}</strong>
          <span className="kpi-sub">
            {query.data ? `Page ${currentPage} of ${totalPages}` : 'Active page index'}
          </span>
        </div>
        <div className="audit-kpi-card kpi-card-amber">
          <div className="kpi-card-header">
            <span className="eyebrow">Events on page</span>
            <span className="kpi-icon-pill icon-amber">
              <Icon name="clock" />
            </span>
          </div>
          <strong>{query.data?.items.length ?? '—'}</strong>
          <span className="kpi-sub">
            {pageEventsCount > 0 ? `Displaying ${pageEventsCount} log entries` : 'Entries on current page'}
          </span>
        </div>
      </div>
      <section className="panel audit-panel">
        <div className="section-header">
          <div className="audit-panel-title">
            <div className="title-row">
              <h2>Audit query parameters</h2>
              {activeFilterCount > 0 && (
                <span className="active-filters-pill">
                  {activeFilterCount} active filter{activeFilterCount > 1 ? 's' : ''}
                </span>
              )}
            </div>
            <span className="muted">Filter the event history across entities, actors, and dates</span>
          </div>
          <div className="audit-quick-presets">
            <span className="preset-label">Quick filter:</span>
            <button
              type="button"
              className={`preset-btn ${!params.EntityType ? 'active' : ''}`}
              onClick={() => setEntityTypeFilter('')}
            >
              All
            </button>
            <button
              type="button"
              className={`preset-btn ${params.EntityType === 'ISSUE' ? 'active' : ''}`}
              onClick={() => setEntityTypeFilter('ISSUE')}
            >
              Issues
            </button>
            <button
              type="button"
              className={`preset-btn ${params.EntityType === 'PROJECT' ? 'active' : ''}`}
              onClick={() => setEntityTypeFilter('PROJECT')}
            >
              Projects
            </button>
            <button
              type="button"
              className={`preset-btn ${params.EntityType === 'EMPLOYEE' ? 'active' : ''}`}
              onClick={() => setEntityTypeFilter('EMPLOYEE')}
            >
              Employees
            </button>
          </div>
        </div>
        <form key={search.toString()} className="filters audit-filters" onSubmit={apply}>
          <div className="audit-filters-grid">
            {[
              ['EntityType', 'Entity type', 'e.g. ISSUE, PROJECT, EMPLOYEE'],
              ['EntityId', 'Entity ID', 'e.g. 7'],
              ['Action', 'Action', 'e.g. ISSUE_UPDATE'],
              ['ActorUserId', 'Actor user ID', 'e.g. 42'],
              ['Source', 'Source', 'e.g. JIRA, IDENTITY_TENANT'],
            ].map(([name, title, placeholder]) => (
              <Field
                key={name}
                label={title}
                name={name}
                placeholder={placeholder}
                defaultValue={params[name]}
                type={name.endsWith('Id') ? 'number' : 'text'}
                min={name.endsWith('Id') ? '1' : undefined}
              />
            ))}
            <Field
              label="From (local time)"
              type="datetime-local"
              name="From"
              defaultValue={localTime(params.From)}
              onInput={(e) => e.currentTarget.form.elements.To.setCustomValidity('')}
            />
            <Field
              label="To (local time)"
              type="datetime-local"
              name="To"
              defaultValue={localTime(params.To)}
              onInput={(e) => e.currentTarget.setCustomValidity('')}
            />
            <SelectField
              label="Results per page"
              name="PageSize"
              values={['20', '50', '100']}
              defaultValue={params.PageSize || '20'}
            />
          </div>
          <div className="audit-filters-actions">
            <span className="audit-filters-hint">
              {activeFilterCount > 0
                ? `Filtering by ${activeFilterCount} active condition${activeFilterCount > 1 ? 's' : ''}.`
                : 'Narrow down system activity logs using exact filters or date ranges.'}
            </span>
            <div className="actions">
              <button type="button" className="audit-reset-btn" onClick={() => setSearch({})}>
                Reset
              </button>
              <button className="primary" disabled={query.isFetching}>
                Apply filters
              </button>
            </div>
          </div>
        </form>
        <QueryState query={query}>
          <AuditTable items={query.data?.items} />
          <Pagination
            data={query.data}
            busy={query.isFetching}
            onPage={(Page) => setSearch({ ...params, Page })}
          />
        </QueryState>
      </section>
    </div>
  );
}
