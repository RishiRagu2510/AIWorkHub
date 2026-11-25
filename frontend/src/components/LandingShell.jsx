import { Suspense, lazy, useState, useEffect, useRef } from 'react';
import { Link } from 'react-router';
import PortalLauncher from './PortalLauncher';
import PortalLogo from './PortalLogo';
import { Icon } from './Icon';
import { useAuth } from '../features/auth/context';
import { request } from '../services/api/client';
import ProfileSettings from './ProfileSettings';
import '../styles/launch-pad.scss';

const NotificationBell = lazy(() => import('../features/notifications/NotificationBell'));

/**
 * The frame of every page on the landing site: brand, search, theme toggle, notifications,
 * a way to jump between portals, who is signed in and sign out.
 * Inner pages (such as the plan) also get a way back to the launch page.
 */
export default function LandingShell({ children, back = false }) {
  const { session, can, logout } = useAuth();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const tenantThemeKey = session?.user?.tenantId
    ? `GRIDHUB:theme:${session.user.tenantId}`
    : 'GRIDHUB:theme';

  const [theme, setTheme] = useState(() => {
    try {
      const tenantKey = session?.user?.tenantId
        ? `GRIDHUB:theme:${session.user.tenantId}`
        : 'GRIDHUB:theme';
      return (
        (typeof localStorage !== 'undefined' && typeof localStorage.getItem === 'function'
          ? localStorage.getItem(tenantKey) || localStorage.getItem('GRIDHUB:theme')
          : null) ||
        (typeof document !== 'undefined' ? document.documentElement.dataset.theme : null) ||
        'light'
      );
    } catch {
      return 'light';
    }
  });

  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const userMenuRef = useRef(null);

  const name = session.user.name || session.user.email;
  const initials = (name || 'U').split(' ').map((part) => part[0]).slice(0, 2).join('').toUpperCase();

  useEffect(() => {
    let active = true;

    // Load initial cached value
    try {
      if (typeof localStorage !== 'undefined' && typeof localStorage.getItem === 'function') {
        const saved = localStorage.getItem(tenantThemeKey) || localStorage.getItem('GRIDHUB:theme');
        if (saved) {
          if (typeof document !== 'undefined') {
            document.documentElement.dataset.theme = saved;
          }
          setTheme(saved);
        }
      }
    } catch {}

    // Fetch latest tenant theme from backend
    async function syncTenantTheme() {
      try {
        const res = await request('/api/tenants/theme');
        if (!active || !res?.theme) return;
        setTheme(res.theme);
        if (typeof document !== 'undefined') {
          document.documentElement.dataset.theme = res.theme;
        }
        try {
          localStorage.setItem(tenantThemeKey, res.theme);
          localStorage.setItem('GRIDHUB:theme', res.theme);
        } catch {}
      } catch {
        // Fallback to local theme if offline or endpoint unavailable
      }
    }

    if (session?.user) {
      syncTenantTheme();
    }

    const interval = setInterval(() => {
      if (session?.user && (typeof document === 'undefined' || document.visibilityState === 'visible')) {
        syncTenantTheme();
      }
    }, 60000);

    const handleWindowFocus = () => {
      if (session?.user && (typeof document === 'undefined' || document.visibilityState === 'visible')) {
        syncTenantTheme();
      }
    };

    if (typeof window !== 'undefined') {
      window.addEventListener('visibilitychange', handleWindowFocus);
      window.addEventListener('focus', handleWindowFocus);
    }

    return () => {
      active = false;
      clearInterval(interval);
      if (typeof window !== 'undefined') {
        window.removeEventListener('visibilitychange', handleWindowFocus);
        window.removeEventListener('focus', handleWindowFocus);
      }
    };
  }, [tenantThemeKey, session?.user]);

  useEffect(() => {
    const handleStorageChange = (e) => {
      if ((e.key === tenantThemeKey || e.key === 'GRIDHUB:theme') && e.newValue) {
        setTheme(e.newValue);
        if (typeof document !== 'undefined') {
          document.documentElement.dataset.theme = e.newValue;
        }
      }
    };
    window.addEventListener('storage', handleStorageChange);
    return () => window.removeEventListener('storage', handleStorageChange);
  }, [tenantThemeKey]);

  useEffect(() => {
    const handleClickOutside = (e) => {
      if (userMenuRef.current && !userMenuRef.current.contains(e.target)) {
        setUserMenuOpen(false);
      }
    };
    document.addEventListener('pointerdown', handleClickOutside);
    return () => document.removeEventListener('pointerdown', handleClickOutside);
  }, []);

  async function toggleTheme() {
    const next = theme === 'dark' ? 'light' : 'dark';
    setTheme(next);
    if (typeof document !== 'undefined') {
      document.documentElement.dataset.theme = next;
    }
    try {
      localStorage.setItem(tenantThemeKey, next);
      localStorage.setItem('GRIDHUB:theme', next);
    } catch {}

    // Persist to backend database so all portals reflect the update
    try {
      await request('/api/tenants/theme', {
        method: 'PUT',
        body: { theme: next },
      });
    } catch (err) {
      console.warn('Failed to persist tenant theme:', err);
    }
  }

  async function signOut() {
    setBusy(true);
    setError('');
    try {
      await logout();
    } catch {
      setError('Unable to sign out. Please try again.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="launch-pad">
      <header className="launch-pad-header topbar">
        <div className="launch-pad-brand topbar-left">
          <Link className="brand" to="/" aria-label="LaunchPad home" data-brand="LaunchPad">
            <PortalLogo portal="landing" />
            <span className="brand-title">LaunchPad</span>
          </Link>
        </div>

        <div className="topbar-center">
          <div
            className="global-search topbar-search-bar"
            role="search"
            aria-label="Search workspace, issues, projects"
          >
            <span className="search-icon-wrap" aria-hidden="true">
              <Icon name="search" />
            </span>
            <span className="search-placeholder-text">
              Search workspace, issues, projects...
            </span>
          </div>
        </div>

        <div className="topbar-actions">
          <button
            className="icon-button theme-toggle-btn"
            aria-label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
            title={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
            onClick={toggleTheme}
            type="button"
          >
            <Icon name={theme === 'dark' ? 'sun' : 'moon'} />
          </button>

          {can('ISSUE_VIEW') && (
            <Suspense fallback={null}>
              <NotificationBell key={`${session?.user?.tenantId}:${session?.user?.id}`} />
            </Suspense>
          )}

          <PortalLauncher forceEnable />

          <div className="user-menu-wrapper" ref={userMenuRef}>
            <button
              type="button"
              className="user-profile-trigger"
              aria-label={`User profile: ${name}`}
              title={name}
              onClick={() => setUserMenuOpen((prev) => !prev)}
            >
              <span className="avatar-chip">{initials}</span>
            </button>
            <div className={`user-popover launchpad-user-popover${userMenuOpen ? ' is-open' : ''}`}>
              <div className="profile-identity">
                <span className="profile-avatar" aria-hidden="true">{initials}</span>
                <div className="profile-identity-text">
                  <strong>{name}</strong>
                  <p>{session?.user?.email}</p>
                  {session?.user?.role && <span className="profile-role">{session?.user?.role}</span>}
                </div>
              </div>
              <div className="profile-company">
                <span className="profile-company-icon">
                  <Icon name="folder" />
                </span>
                <div>
                  <span>Company workspace</span>
                  <strong>{session?.user?.tenantName || 'Grid Dynamics'}</strong>
                </div>
              </div>
              <div className="profile-actions">
                {session?.user?.role === 'ADMIN' && (
                  <button
                    type="button"
                    className="profile-action-link"
                    onClick={() => {
                      setUserMenuOpen(false);
                      setSettingsOpen(true);
                    }}
                  >
                    <Icon name="settings" />
                    <span>Settings</span>
                    <span className="profile-action-arrow" aria-hidden="true">›</span>
                  </button>
                )}
                <Link
                  className="profile-action-link"
                  to="/plan"
                  onClick={() => setUserMenuOpen(false)}
                >
                  <Icon name="shield" />
                  <span>Your plan</span>
                  <span className="profile-action-arrow" aria-hidden="true">›</span>
                </Link>
                <button
                  type="button"
                  className="profile-signout"
                  disabled={busy}
                  onClick={signOut}
                >
                  <Icon name="logout" />
                  <span>{busy ? 'Signing out…' : 'Sign out'}</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      </header>
      {session?.user && (
        <ProfileSettings
          user={session.user}
          open={settingsOpen}
          close={() => setSettingsOpen(false)}
        />
      )}
      <main className="launch-pad-main">
        {back && <Link className="launch-pad-back" to="/">← All portals</Link>}
        {error && <p role="alert" className="launch-pad-error">{error}</p>}
        {children}
      </main>
    </div>
  );
}
