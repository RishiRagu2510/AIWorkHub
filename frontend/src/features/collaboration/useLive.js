import { useEffect, useRef, useState, createContext, useContext } from 'react';
import { useDispatch } from 'react-redux';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { api } from '../../services/api/api';
import { apiBase, getAccessToken } from '../../services/api/client';
import { useAuth } from '../auth/context';
import { startCollaborationStream } from './stream';
export const PresenceContext = createContext({ typing: () => {} });
export const usePresence = () => useContext(PresenceContext);
export function useCollaborationLive(kind, id, { editing = false, enabled = true } = {}) {
  const dispatch = useDispatch();
  const { session, can } = useAuth();
  const transport = useRef(null);
  const lastTyping = useRef(0);
  const [state, setState] = useState('Connecting…');
  const [presence, setPresence] = useState([]);
  const user = session?.user;
  const identity = `${user?.tenantId}:${user?.id}`;
  const mayEdit = can('ISSUE_UPDATE'),
    mayType = can('ISSUE_COMMENT_CREATE');
  useEffect(() => {
    if (!enabled) return;
    let timer,
      heartbeat,
      ready = false;
    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBase}${kind === 'BOARD' ? `/hubs/boards/${id}` : '/hubs/issues'}`, {
        accessTokenFactory: getAccessToken,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build();
    function refresh() {
      if (!timer)
        timer = setTimeout(() => {
          timer = null;
          dispatch(
            api.util.invalidateTags(
              kind === 'ISSUE'
                ? [
                    { type: 'Issue', id: String(id) },
                    { type: 'Comments', id: String(id) },
                    { type: 'Attachments', id: String(id) },
                    'Activity',
                    'Watchers',
                    'Issues',
                  ]
                : kind === 'BOARD'
                  ? ['Boards', 'Issues']
                  : ['Activity', 'Audit'],
            ),
          );
        }, 200);
    }
    async function pulse(typing = false) {
      if (!ready || kind !== 'ISSUE') return;
      try {
        await connection.invoke('Pulse', Number(id), editing && mayEdit, typing && mayType);
      } catch {
        setPresence([]);
        refresh();
      }
    }
    transport.current = pulse;
    const stop = startCollaborationStream(
      connection,
      kind === 'ISSUE' ? 'WatchIssue' : kind === 'BOARD' ? 'WatchBoard' : 'WatchActivity',
      kind === 'ACTIVITY' ? [] : [Number(id)],
      (event) => {
        setPresence(event?.presence || []);
        if (event?.changed) refresh();
      },
      (status) => {
        setState(status);
        ready = status === 'Connected';
        if (ready) void pulse();
      },
      () => {
        ready = false;
        setPresence([]);
        refresh();
      },
    );
    if (kind === 'ISSUE') heartbeat = setInterval(() => void pulse(), 15000);
    return () => {
      transport.current = null;
      clearTimeout(timer);
      clearInterval(heartbeat);
      stop();
    };
  }, [kind, id, enabled, editing, mayEdit, mayType, identity, dispatch]);
  return {
    state,
    presence,
    typing: (active = true) => {
      const now = Date.now();
      if (!active || now - lastTyping.current > 2000) {
        lastTyping.current = now;
        void transport.current?.(active);
      }
    },
  };
}
