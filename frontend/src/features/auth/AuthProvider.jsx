import { hydratePreferences, stopPreferenceSync } from '../../utils/preferenceSync';
import { useCallback, useEffect, useRef, useState } from 'react';
import { request, setSessionToken, onSessionExpired } from '../../services/api/client';
import { sessionFromToken, hasPermission, hasPortalAccess, hasFeatureAccess } from './session';
import { store } from '../../app/store';
import { api } from '../../services/api/api';
import { AuthContext, useAuth } from './context';

const sessionEventKey = `GRIDHUB:${import.meta.env.VITE_PORTAL || 'workspace'}:session-event`;
function announce(type) {
  try {
    // Only an event is shared. Cookies stay HttpOnly and bearer tokens stay in memory.
    window.localStorage.setItem(sessionEventKey, JSON.stringify({ type, id: crypto.randomUUID() }));
  } catch {
    // Storage may be disabled; foreground checks still verify the server session.
  }
}

export function AuthProvider({ children }) {
  const generation = useRef(0);
  const pendingRestore = useRef(null);
  const loggingOut = useRef(false);
  const [session, setSession] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const invalidate = useCallback(() => {
    generation.current++;
    pendingRestore.current?.abort();
    pendingRestore.current = null;
  }, []);
  const clear = useCallback(() => {
    invalidate();
    stopPreferenceSync();
    setSessionToken(null);
    setSession(null);
    store.dispatch(api.util.resetApiState());
  }, [invalidate]);
  const restore = useCallback(
    async ({ broadcast = false } = {}) => {
      if (loggingOut.current) return false;
      clear();
      setLoading(true);
      setError(null);
      const expectedGeneration = generation.current;
      const controller = new AbortController();
      pendingRestore.current = controller;
      const current = () => generation.current === expectedGeneration && !controller.signal.aborted;
      try {
        const identity = await request('/api/auth/me', {
          session: true,
          signal: controller.signal,
        });
        if (!current()) return false;
        const { accessToken } = await request('/api/auth/token', {
          method: 'POST',
          session: true,
          signal: controller.signal,
        });
        if (!current()) return false;
        const next = sessionFromToken(accessToken, identity.user);
        setSessionToken(accessToken);
        await hydratePreferences(next.user);
        if (!current()) return false;
        setSession(next);
        if (broadcast) announce('signed-in');
        return true;
      } catch (e) {
        if (current()) {
          setSessionToken(null);
          setError(
            e.status === 401 ? null : e.message || 'Unable to restore your session. Try again.',
          );
        }
        return false;
      } finally {
        if (current()) {
          pendingRestore.current = null;
          setLoading(false);
        }
      }
    },
    [clear],
  );

  const updateUser = useCallback((updatedFields) => {
    setSession((current) => {
      if (!current || !current.user) return current;
      return {
        ...current,
        user: {
          ...current.user,
          ...updatedFields,
        },
      };
    });
  }, []);

  useEffect(() => {
    // Restore is synchronization with the external HttpOnly cookie session.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    restore();
    return () => {
      invalidate();
      stopPreferenceSync();
      setSessionToken(null);
    };
  }, [restore, invalidate]);

  useEffect(() => {
    function changed(event) {
      if (event.key !== sessionEventKey || !event.newValue) return;
      let type;
      try {
        ({ type } = JSON.parse(event.newValue));
      } catch {
        return;
      }
      if (type === 'signed-out') {
        clear();
        setLoading(false);
        setError('You signed out in another tab.');
      } else if (type === 'signed-in') {
        // An event is only a hint: verify the HttpOnly cookie with the backend.
        void restore();
      }
    }
    window.addEventListener('storage', changed);
    return () => window.removeEventListener('storage', changed);
  }, [clear, restore]);

  useEffect(() => {
    let active = true;
    let renewing = false;
    let timer;
    const controller = new AbortController();
    const expectedGeneration = generation.current;
    const current = () =>
      active && generation.current === expectedGeneration && !loggingOut.current;
    const expire = () => {
      if (!current()) return;
      clear();
      setLoading(false);
      setError('Your session has expired. Please sign in again.');
    };
    onSessionExpired(expire);
    async function renew() {
      if (!session || renewing || !current()) return;
      renewing = true;
      clearTimeout(timer);
      try {
        const identity = await request('/api/auth/me', {
          session: true,
          signal: controller.signal,
        });
        if (!current()) return;
        if (
          String(identity.user.id) !== String(session.user.id) ||
          String(identity.user.tenantId) !== String(session.user.tenantId)
        ) {
          // Another tab switched accounts. Drop the previous account's cached data first.
          void restore();
          return;
        }
        const { accessToken } = await request('/api/auth/token', {
          method: 'POST',
          session: true,
          signal: controller.signal,
        });
        if (!current()) return;
        const next = sessionFromToken(accessToken, identity.user);
        setSessionToken(accessToken);
        setError(null);
        // A same-second token must not cause an immediate renewal loop.
        if (next.expiresAt <= session.expiresAt) {
          timer = setTimeout(renew, Math.max(1000, Math.min(15000, next.expiresAt - Date.now())));
        } else {
          setSession(next);
        }
      } catch (e) {
        if (!current()) return;
        if (e.status === 401 || e.status === 403 || session.expiresAt <= Date.now()) {
          expire();
        } else {
          // A brief network failure should not throw away a still-valid login.
          timer = setTimeout(
            renew,
            Math.max(1000, Math.min(15000, session.expiresAt - Date.now())),
          );
        }
      } finally {
        renewing = false;
      }
    }
    const wake = () => {
      if (document.visibilityState !== 'hidden') void renew();
    };
    if (session) timer = setTimeout(renew, Math.max(1000, session.expiresAt - Date.now() - 30000));
    window.addEventListener('focus', wake);
    window.addEventListener('online', wake);
    document.addEventListener('visibilitychange', wake);
    return () => {
      active = false;
      controller.abort();
      clearTimeout(timer);
      onSessionExpired(() => {});
      window.removeEventListener('focus', wake);
      window.removeEventListener('online', wake);
      document.removeEventListener('visibilitychange', wake);
    };
  }, [session, clear, restore]);

  async function logout() {
    if (loggingOut.current) return;
    loggingOut.current = true;
    // Invalidate all in-flight restore/renew responses before contacting the server.
    invalidate();
    try {
      await request('/api/auth/logout', { method: 'POST', session: true });
      clear();
      setLoading(false);
      setError(null);
      announce('signed-out');
    } catch (e) {
      // Re-establish renewal after a failed logout; do not claim server revocation succeeded.
      loggingOut.current = false;
      setSession((current) => (current ? { ...current } : current));
      setError('Sign out failed. Please try again.');
      throw e;
    } finally {
      loggingOut.current = false;
    }
  }
  return (
    <AuthContext.Provider
      value={{
        session,
        loading,
        error,
        restore,
        logout,
        updateUser,
        can: (permission) => hasPermission(session, permission),
        canAccessPortal: (portal) => hasPortalAccess(session, portal),
        canUseFeature: (feature) => hasFeatureAccess(session, feature),
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
export function PermissionGuard({ permission, children, fallback = null }) {
  return useAuth().can(permission) ? children : fallback;
}
export function PortalGuard({ portal, children, fallback = null }) {
  return useAuth().canAccessPortal(portal) ? children : fallback;
}
