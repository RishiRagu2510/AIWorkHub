import { useEffect, useRef, useState } from 'react';
import { useDispatch } from 'react-redux';
import Link from '../../components/PortalLink';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import {
  api,
  useNotificationsQuery,
  useUnreadNotificationsQuery,
  useReadNotificationMutation,
  useReadAllNotificationsMutation,
} from '../../services/api/api';
import { apiBase, getAccessToken } from '../../services/api/client';
import { liveQueryOptions } from '../../services/api/live';
import { QueryState, Pagination, ErrorState, EmptyState } from '../../components/common';
import { Icon } from '../../components/Icon';
import { dateTime } from '../../utils/format';
import { startInboxStream } from './inboxStream';
import { describeNotification, notificationTarget, notificationTitle, timeAgo } from './notificationPresentation';
export default function NotificationBell() {
  const dispatch = useDispatch();
  const [open, setOpen] = useState(false);
  const [page, setPage] = useState(1);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [connectionState, setConnectionState] = useState('Connecting live notifications…');
  const ref = useRef(null);
  const trigger = useRef(null);
  const count = useUnreadNotificationsQuery(undefined, liveQueryOptions);
  const inbox = useNotificationsQuery(
    { page, pageSize: 20, unreadOnly },
    { ...liveQueryOptions, skip: !open, refetchOnMountOrArgChange: true },
  );
  const [read, reading] = useReadNotificationMutation();
  const [readAll, readingAll] = useReadAllNotificationsMutation();
  useEffect(() => {
    let refresh;
    const tags = new Set();
    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBase}/hubs/notifications`, { accessTokenFactory: getAccessToken })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build();
    const stop = startInboxStream(
      connection,
      (event) => {
        if (event?.changed) {
          tags.add('Notifications');
          tags.add('Timesheets');
        }
        if (event?.issuesChanged) ['Issues', 'Issue'].forEach((tag) => tags.add(tag));
        if (!refresh)
          refresh = setTimeout(() => {
            refresh = null;
            dispatch(api.util.invalidateTags([...tags]));
            tags.clear();
          }, 150);
      },
      setConnectionState,
    );
    return () => {
      clearTimeout(refresh);
      stop();
    };
  }, [dispatch]);
  useEffect(() => {
    if (!open) return;
    const dismiss = (event) => {
      if (!ref.current?.contains(event.target)) setOpen(false);
    };
    const escape = (event) => {
      if (event.key === 'Escape') {
        setOpen(false);
        trigger.current?.focus();
      }
    };
    document.addEventListener('pointerdown', dismiss);
    document.addEventListener('keydown', escape);
    return () => {
      document.removeEventListener('pointerdown', dismiss);
      document.removeEventListener('keydown', escape);
    };
  }, [open]);
  const total = count.error ? null : count.data?.unreadCount;
  return (
    <div className="notification-menu" ref={ref}>
      <button
        ref={trigger}
        className="notification-trigger"
        aria-label={total ? `Notifications, ${total} unread` : 'Notifications'}
        aria-expanded={open}
        aria-controls={open ? 'notification-inbox' : undefined}
        onClick={() => {
          setOpen(!open);
          count.refetch();
        }}
      >
        <Icon name="bell" />
        {total > 0 && <span>{total > 99 ? '99+' : total}</span>}
      </button>
      {open && (
        <section id="notification-inbox" className="notification-inbox" aria-label="Notifications">
          <header className="section-header">
            <h2>Notifications</h2>
            <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <Link
                to="/notifications"
                className="button-link"
                style={{ fontSize: '0.8125rem' }}
                onClick={() => setOpen(false)}
              >
                View all
              </Link>
              <button
                onClick={() => {
                  setOpen(false);
                  trigger.current?.focus();
                }}
              >
                Close
              </button>
            </div>
          </header>
          <div className="notification-controls">
            <label>
              <input
                name="unreadOnly"
                type="checkbox"
                checked={unreadOnly}
                onChange={(e) => {
                  setUnreadOnly(e.target.checked);
                  setPage(1);
                }}
              />
              Unread only
            </label>
            <button disabled={readingAll.isLoading || !total} onClick={() => readAll()}>
              Mark all read
            </button>
            <button
              disabled={inbox.isFetching}
              onClick={() => {
                inbox.refetch();
                count.refetch();
              }}
            >
              Refresh
            </button>
          </div>
          <p className="hint notification-connection">{connectionState}</p>
          <ErrorState error={reading.error || readingAll.error || count.error} />
          <QueryState query={inbox}>
            {inbox.data?.items.length ? (
              <ul>
                {inbox.data.items.map((item) => {
                  const look = describeNotification(item);
                  return (
                    <li key={item.id} className={`notification-item tone-${look.tone}${item.readAt ? '' : ' notification-unread'}`}>
                      <span className="notification-icon" aria-hidden="true">
                        {look.symbol}
                      </span>
                      <div className="notification-main">
                        <div className="notification-meta">
                          <small>{look.label}</small>
                          <time dateTime={item.createdAt} title={dateTime(item.createdAt)}>
                            {timeAgo(item.createdAt)}
                          </time>
                        </div>
                        <Link
                          to={notificationTarget(item)}
                          onClick={() => {
                            if (!item.readAt) read(item.id);
                            setOpen(false);
                          }}
                        >
                          {notificationTitle(item)}
                        </Link>
                        {item.rejectionReason && <p className="notification-reason">{item.rejectionReason}</p>}
                      </div>
                      {!item.readAt && (
                        <button className="notification-read" disabled={reading.isLoading} onClick={() => read(item.id)}>
                          Mark read
                        </button>
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
                {unreadOnly ? 'No unread notifications to review.' : 'Activity and assignments will appear here.'}
              </EmptyState>
            )}
            <Pagination data={inbox.data} busy={inbox.isFetching} onPage={setPage} />
          </QueryState>
        </section>
      )}
    </div>
  );
}
