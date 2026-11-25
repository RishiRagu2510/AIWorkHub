import { useState, useMemo } from 'react';
import { useAuth } from '../../../src/features/auth/context';
import { useSearchIssuesQuery } from '../../../src/services/api/api';
import { liveQueryOptions } from '../../../src/services/api/live';
import { PageHeader, QueryState, Pagination } from '../../../src/components/common';
import { LiveStatus } from '../../../src/components/LiveStatus';
import { Icon } from '../../../src/components/Icon';
import Link from '../../../src/components/PortalLink';
import WorkIssueList from '../../../src/features/issues/WorkIssueList';

const GROUP_CONFIGS = {
  OVERDUE: {
    name: 'Overdue',
    icon: 'clock',
    color: '#dc2626',
    borderAccent: '#ef4444',
    bgLight: '#fef2f2',
    description: 'Issues whose due date has passed. Need immediate action or rescheduled date.',
    badgeText: 'Action required',
  },
  DUE_SOON: {
    name: 'Due soon',
    icon: 'clock',
    color: '#d97706',
    borderAccent: '#f59e0b',
    bgLight: '#fffbeb',
    description: 'Assignments due today or within the next seven calendar days.',
    badgeText: 'Upcoming 7 days',
  },
  IN_PROGRESS: {
    name: 'In progress',
    icon: 'dashboard',
    color: '#2563eb',
    borderAccent: '#3b82f6',
    bgLight: '#eff6ff',
    description: 'Work items currently being implemented, reviewed, or tested.',
    badgeText: 'Active',
  },
  OTHER: {
    name: 'Other open work',
    icon: 'folder',
    color: '#475569',
    borderAccent: '#64748b',
    bgLight: '#f8fafc',
    description: 'Open issues waiting in your queue or scheduled for future sprints.',
    badgeText: 'Queue',
  },
};

function WorkGroup({ name, group, userId, today }) {
  const [page, setPage] = useState(1);
  const cfg = GROUP_CONFIGS[group] || GROUP_CONFIGS.OTHER;

  const query = useSearchIssuesQuery(
    { AssigneeUserId: userId, WorkGroup: group, Today: today, Page: page, PageSize: 10 },
    liveQueryOptions,
  );

  return (
    <section
      className={`panel work-group-card is-${group.toLowerCase()}`}
      style={{
        borderTop: `4px solid ${cfg.borderAccent}`,
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <div className="section-header" style={{ padding: '16px 20px', alignItems: 'flex-start' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <div
            className={`work-group-icon-${group.toLowerCase().replace('_', '')}`}
            style={{
              width: '32px',
              height: '32px',
              borderRadius: '8px',
              background: cfg.bgLight,
              color: cfg.color,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <Icon name={cfg.icon} />
          </div>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <h2 style={{ margin: 0, fontSize: '16px' }}>{name}</h2>
              <span
                className={`work-group-badge-${group.toLowerCase().replace('_', '')}`}
                style={{
                  background: cfg.bgLight,
                  color: cfg.color,
                  fontWeight: 500,
                  fontSize: '12px',
                  padding: '2px 8px',
                  borderRadius: '12px',
                }}
              >
                {query.data?.totalCount ?? 0}
              </span>
            </div>
            <p className="hint" style={{ margin: '2px 0 0', fontSize: '11px', color: 'var(--theme-muted, #64748b)' }}>
              {cfg.description}
            </p>
          </div>
        </div>

        <span
          className={`badge work-group-badge-${group.toLowerCase().replace('_', '')}`}
          style={{
            fontSize: '10px',
            textTransform: 'uppercase',
            letterSpacing: '0.04em',
            background: cfg.bgLight,
            color: cfg.color,
            border: `1px solid ${cfg.color}30`,
          }}
        >
          {cfg.badgeText}
        </span>
      </div>

      <div className="work-refresh" style={{ padding: '0 20px 8px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <LiveStatus query={query} />
        {query.data?.totalCount > 0 && (
          <span className="muted" style={{ fontSize: '11px' }}>
            Page {page} of {query.data.totalPages}
          </span>
        )}
      </div>

      <div style={{ flex: 1 }}>
        <QueryState query={query}>
          <WorkIssueList
            issues={query.data?.items || []}
            group={group}
            today={today}
          />
          {query.data?.totalPages > 1 && (
            <div style={{ padding: '12px 20px' }}>
              <Pagination data={query.data} busy={query.isFetching} onPage={setPage} />
            </div>
          )}
        </QueryState>
      </div>
    </section>
  );
}

export default function MyWork() {
  const { session, can } = useAuth();
  const [activeFilter, setActiveFilter] = useState('ALL'); // 'ALL' | 'OVERDUE' | 'DUE_SOON' | 'IN_PROGRESS' | 'OTHER'

  const now = new Date();
  const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;

  // Lightweight summary queries to populate top KPI cards
  const overdueQuery = useSearchIssuesQuery(
    { AssigneeUserId: session.user.id, WorkGroup: 'OVERDUE', Today: today, Page: 1, PageSize: 1 },
    liveQueryOptions,
  );
  const dueSoonQuery = useSearchIssuesQuery(
    { AssigneeUserId: session.user.id, WorkGroup: 'DUE_SOON', Today: today, Page: 1, PageSize: 1 },
    liveQueryOptions,
  );
  const inProgressQuery = useSearchIssuesQuery(
    { AssigneeUserId: session.user.id, WorkGroup: 'IN_PROGRESS', Today: today, Page: 1, PageSize: 1 },
    liveQueryOptions,
  );
  const otherQuery = useSearchIssuesQuery(
    { AssigneeUserId: session.user.id, WorkGroup: 'OTHER', Today: today, Page: 1, PageSize: 1 },
    liveQueryOptions,
  );

  const overdueCount = overdueQuery.data?.totalCount ?? 0;
  const dueSoonCount = dueSoonQuery.data?.totalCount ?? 0;
  const inProgressCount = inProgressQuery.data?.totalCount ?? 0;
  const otherCount = otherQuery.data?.totalCount ?? 0;
  const totalOpen = overdueCount + dueSoonCount + inProgressCount + otherCount;

  const groupsToDisplay = useMemo(() => {
    const all = [
      ['Overdue', 'OVERDUE'],
      ['Due soon', 'DUE_SOON'],
      ['In progress', 'IN_PROGRESS'],
      ['Other open work', 'OTHER'],
    ];
    if (activeFilter === 'ALL') return all;
    return all.filter(([, g]) => g === activeFilter);
  }, [activeFilter]);

  return (
    <div className="my-work-page" style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      <PageHeader
        title="My work"
        description={`Open work assigned to ${session.user.name || session.user.email}.`}
      >
        {can('ISSUE_CREATE') && (
          <Link className="button primary" to="/issues/create">
            <Icon name="plus" />
            New issue
          </Link>
        )}
        {can('TIMESHEET_VIEW') && (
          <Link className="button" to="/timesheets/my">
            <Icon name="clock" />
            My timesheet
          </Link>
        )}
      </PageHeader>


      {/* Top KPI Summary Ribbon Cards */}
      <div
        className="my-work-kpi-grid"
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
          gap: '14px',
        }}
      >
        {/* Overdue Card */}
        <div
          role="button"
          tabIndex={0}
          onClick={() => setActiveFilter(activeFilter === 'OVERDUE' ? 'ALL' : 'OVERDUE')}
          style={{
            cursor: 'pointer',
            padding: '16px',
            background: 'var(--theme-surface, #ffffff)',
            borderRadius: '10px',
            border: activeFilter === 'OVERDUE' ? '2px solid #ef4444' : '1px solid var(--theme-border, #e2e8f0)',
            boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
            display: 'flex',
            alignItems: 'center',
            gap: '14px',
            transition: 'all 0.15s ease',
          }}
        >
          <div
            className="my-work-metric-tile-overdue"
            style={{
              width: '44px',
              height: '44px',
              borderRadius: '10px',
              background: '#fee2e2',
              color: '#dc2626',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <Icon name="clock" />
          </div>
          <div>
            <div style={{ fontSize: '11px', textTransform: 'uppercase', fontWeight: 500, color: '#dc2626', letterSpacing: '0.04em' }}>
              Overdue
            </div>
            <div style={{ fontSize: '24px', fontWeight: 500, color: overdueCount > 0 ? '#dc2626' : 'var(--theme-text, #111827)', lineHeight: 1.2 }}>
              {overdueCount}
            </div>
            <small style={{ fontSize: '11px', color: 'var(--theme-muted, #64748b)' }}>
              {overdueCount > 0 ? 'Requires attention' : 'Zero overdue'}
            </small>
          </div>
        </div>

        {/* Due Soon Card */}
        <div
          role="button"
          tabIndex={0}
          onClick={() => setActiveFilter(activeFilter === 'DUE_SOON' ? 'ALL' : 'DUE_SOON')}
          style={{
            cursor: 'pointer',
            padding: '16px',
            background: 'var(--theme-surface, #ffffff)',
            borderRadius: '10px',
            border: activeFilter === 'DUE_SOON' ? '2px solid #f59e0b' : '1px solid var(--theme-border, #e2e8f0)',
            boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
            display: 'flex',
            alignItems: 'center',
            gap: '14px',
            transition: 'all 0.15s ease',
          }}
        >
          <div
            className="my-work-metric-tile-duesoon"
            style={{
              width: '44px',
              height: '44px',
              borderRadius: '10px',
              background: '#fef3c7',
              color: '#d97706',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <Icon name="clock" />
          </div>
          <div>
            <div style={{ fontSize: '11px', textTransform: 'uppercase', fontWeight: 500, color: '#d97706', letterSpacing: '0.04em' }}>
              Due Soon
            </div>
            <div style={{ fontSize: '24px', fontWeight: 500, lineHeight: 1.2 }}>
              {dueSoonCount}
            </div>
            <small style={{ fontSize: '11px', color: 'var(--theme-muted, #64748b)' }}>
              Next 7 calendar days
            </small>
          </div>
        </div>

        {/* In Progress Card */}
        <div
          role="button"
          tabIndex={0}
          onClick={() => setActiveFilter(activeFilter === 'IN_PROGRESS' ? 'ALL' : 'IN_PROGRESS')}
          style={{
            cursor: 'pointer',
            padding: '16px',
            background: 'var(--theme-surface, #ffffff)',
            borderRadius: '10px',
            border: activeFilter === 'IN_PROGRESS' ? '2px solid #3b82f6' : '1px solid var(--theme-border, #e2e8f0)',
            boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
            display: 'flex',
            alignItems: 'center',
            gap: '14px',
            transition: 'all 0.15s ease',
          }}
        >
          <div
            className="my-work-metric-tile-inprogress"
            style={{
              width: '44px',
              height: '44px',
              borderRadius: '10px',
              background: '#eff6ff',
              color: '#2563eb',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <Icon name="dashboard" />
          </div>
          <div>
            <div style={{ fontSize: '11px', textTransform: 'uppercase', fontWeight: 500, color: '#2563eb', letterSpacing: '0.04em' }}>
              In Progress
            </div>
            <div style={{ fontSize: '24px', fontWeight: 500, lineHeight: 1.2 }}>
              {inProgressCount}
            </div>
            <small style={{ fontSize: '11px', color: 'var(--theme-muted, #64748b)' }}>
              Actively in flight
            </small>
          </div>
        </div>

        {/* Other Open Work Card */}
        <div
          role="button"
          tabIndex={0}
          onClick={() => setActiveFilter(activeFilter === 'OTHER' ? 'ALL' : 'OTHER')}
          style={{
            cursor: 'pointer',
            padding: '16px',
            background: 'var(--theme-surface, #ffffff)',
            borderRadius: '10px',
            border: activeFilter === 'OTHER' ? '2px solid #64748b' : '1px solid var(--theme-border, #e2e8f0)',
            boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
            display: 'flex',
            alignItems: 'center',
            gap: '14px',
            transition: 'all 0.15s ease',
          }}
        >
          <div
            className="my-work-metric-tile-other"
            style={{
              width: '44px',
              height: '44px',
              borderRadius: '10px',
              background: '#f8fafc',
              color: '#475569',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <Icon name="folder" />
          </div>
          <div>
            <div style={{ fontSize: '11px', textTransform: 'uppercase', fontWeight: 500, color: '#475569', letterSpacing: '0.04em' }}>
              Other Open
            </div>
            <div style={{ fontSize: '24px', fontWeight: 500, lineHeight: 1.2 }}>
              {otherCount}
            </div>
            <small style={{ fontSize: '11px', color: 'var(--theme-muted, #64748b)' }}>
              Total open: {totalOpen}
            </small>
          </div>
        </div>
      </div>

      {/* View & Filter Navigation Bar */}
      <div
        className="my-work-filter-bar"
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: '12px',
          flexWrap: 'wrap',
          borderBottom: '1px solid var(--theme-border, #e2e8f0)',
          paddingBottom: '12px',
        }}
      >
        <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
          <button
            type="button"
            className={activeFilter === 'ALL' ? 'primary' : 'button'}
            onClick={() => setActiveFilter('ALL')}
            style={{ fontSize: '12px', minHeight: '32px' }}
          >
            All Work ({totalOpen})
          </button>
          <button
            type="button"
            className={activeFilter === 'OVERDUE' ? 'primary' : 'button'}
            onClick={() => setActiveFilter('OVERDUE')}
            style={{ fontSize: '12px', minHeight: '32px' }}
          >
            Overdue ({overdueCount})
          </button>
          <button
            type="button"
            className={activeFilter === 'DUE_SOON' ? 'primary' : 'button'}
            onClick={() => setActiveFilter('DUE_SOON')}
            style={{ fontSize: '12px', minHeight: '32px' }}
          >
            Due Soon ({dueSoonCount})
          </button>
          <button
            type="button"
            className={activeFilter === 'IN_PROGRESS' ? 'primary' : 'button'}
            onClick={() => setActiveFilter('IN_PROGRESS')}
            style={{ fontSize: '12px', minHeight: '32px' }}
          >
            In Progress ({inProgressCount})
          </button>
          <button
            type="button"
            className={activeFilter === 'OTHER' ? 'primary' : 'button'}
            onClick={() => setActiveFilter('OTHER')}
            style={{ fontSize: '12px', minHeight: '32px' }}
          >
            Other Open ({otherCount})
          </button>
        </div>

        <span className="muted" style={{ fontSize: '12px' }}>
          Today: <strong>{today}</strong>
        </span>
      </div>

      {/* Work Groups Grid */}
      <div
        className="work-groups"
        style={{
          display: 'grid',
          gridTemplateColumns: activeFilter === 'ALL' ? 'repeat(auto-fit, minmax(460px, 1fr))' : '1fr',
          gap: '20px',
        }}
      >
        {groupsToDisplay.map(([name, group]) => (
          <WorkGroup
            key={`${group}:${today}`}
            name={name}
            group={group}
            userId={session.user.id}
            today={today}
          />
        ))}
      </div>
    </div>
  );
}
