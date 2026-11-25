import { useState } from 'react';
import Link from '../components/PortalLink';
import { PageHeader, QueryState, Pagination, EmptyState } from '../components/common';
import {
  useNotificationsQuery,
  useUnreadNotificationsQuery,
  useReadNotificationMutation,
  useReadAllNotificationsMutation,
} from '../services/api/api';
import { liveQueryOptions } from '../services/api/live';
import { dateTime } from '../utils/format';
import {
  PORTAL_NAMES,
  describeNotification,
  notificationTarget,
  notificationTitle,
  timeAgo,
} from '../features/notifications/notificationPresentation';
import '../styles/notifications-page.scss';

export default function NotificationsPage() {
  const [page, setPage] = useState(1);
  const [filterPortal, setFilterPortal] = useState('ALL'); // 'ALL' | 'GRIDHUB' | 'CHRONOS' | 'PERSONA'
  const [unreadOnly, setUnreadOnly] = useState(false);

  const count = useUnreadNotificationsQuery(undefined, liveQueryOptions);
  const inbox = useNotificationsQuery(
    { page, pageSize: 25, unreadOnly },
    { ...liveQueryOptions, refetchOnMountOrArgChange: true }
  );

  const [read, reading] = useReadNotificationMutation();
  const [readAll, readingAll] = useReadAllNotificationsMutation();

  const items = inbox.data?.items || [];

  const filteredItems = items.filter((item) => {
    if (filterPortal === 'ALL') return true;
    if (filterPortal === 'CHRONOS') {
      return item.kind?.startsWith('TIMESHEET') || Boolean(item.timesheetPeriodId);
    }
    if (filterPortal === 'PERSONA') {
      return item.kind?.startsWith('EMPLOYEE');
    }
    if (filterPortal === 'GRIDHUB') {
      return !item.kind?.startsWith('TIMESHEET') && !item.kind?.startsWith('EMPLOYEE');
    }
    return true;
  });

  return (
    <div className="notifications-workspace">
      <PageHeader
        title="Notifications Center"
        description="Unified cross-portal notification stream across GridHub, Persona, and Chronos."
      >
        <button
          className="button"
          disabled={readingAll.isLoading || !count.data?.unreadCount}
          onClick={() => readAll()}
        >
          {readingAll.isLoading ? 'Marking all read…' : 'Mark all read'}
        </button>
      </PageHeader>

      <div className="notifications-controls-bar">
        <div className="portal-filter-tabs">
          <button
            type="button"
            className={`tab-btn ${filterPortal === 'ALL' ? 'active' : ''}`}
            onClick={() => setFilterPortal('ALL')}
          >
            All Portals
          </button>
          <button
            type="button"
            className={`tab-btn ${filterPortal === 'GRIDHUB' ? 'active' : ''}`}
            onClick={() => setFilterPortal('GRIDHUB')}
          >
            GridHub
          </button>
          <button
            type="button"
            className={`tab-btn ${filterPortal === 'PERSONA' ? 'active' : ''}`}
            onClick={() => setFilterPortal('PERSONA')}
          >
            Persona
          </button>
          <button
            type="button"
            className={`tab-btn ${filterPortal === 'CHRONOS' ? 'active' : ''}`}
            onClick={() => setFilterPortal('CHRONOS')}
          >
            Chronos
          </button>
        </div>

        <div className="read-toggle-wrapper">
          <label className="checkbox-toggle">
            <input
              type="checkbox"
              checked={unreadOnly}
              onChange={(e) => {
                setUnreadOnly(e.target.checked);
                setPage(1);
              }}
            />
            <span>Unread only</span>
          </label>
        </div>
      </div>

      <QueryState state={inbox}>
        {filteredItems.length > 0 ? (
          <ul className="notifications-full-list">
            {filteredItems.map((item) => {
              const look = describeNotification(item);
              return (
                <li key={item.id} className={`notification-card tone-${look.tone} ${!item.readAt ? 'is-unread' : ''}`}>
                  <span className="notification-card-icon" aria-hidden="true">
                    {look.symbol}
                  </span>
                  <div className="notification-card-content">
                    <div className="notification-card-header">
                      <span className={`portal-tag ${look.portal}-tag`}>{PORTAL_NAMES[look.portal]}</span>
                      <span className="notification-kind-label">{look.label}</span>
                      <time dateTime={item.createdAt} className="notification-time" title={dateTime(item.createdAt)}>
                        {timeAgo(item.createdAt)}
                      </time>
                    </div>

                    <div className="notification-card-body">
                      <Link
                        to={notificationTarget(item)}
                        className="notification-target-link"
                        onClick={() => {
                          if (!item.readAt) read(item.id);
                        }}
                      >
                        {notificationTitle(item)}
                      </Link>
                      {item.rejectionReason && (
                        <div className="rejection-box">
                          <strong>Reason: </strong>
                          {item.rejectionReason}
                        </div>
                      )}
                    </div>
                  </div>

                  {!item.readAt && (
                    <div className="notification-card-actions">
                      <button className="button-link mark-read-btn" disabled={reading.isLoading} onClick={() => read(item.id)}>
                        Mark as read
                      </button>
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        ) : (
          <EmptyState
            title={unreadOnly ? 'All caught up' : 'No notifications'}
            variant="notifications"
          >
            {unreadOnly
              ? 'No unread notifications to review in this category.'
              : 'You have no notifications across the portals yet.'}
          </EmptyState>
        )}

        <Pagination data={inbox.data} busy={inbox.isFetching} onPage={setPage} />
      </QueryState>
    </div>
  );
}
