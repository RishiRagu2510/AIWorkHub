import PortalLogo from './PortalLogo';
import { portalName, activePortal, portalLanding, portalKind } from '../utils/portals';
import PortalLauncher from './PortalLauncher';
import { useProjectsQuery } from '../services/api/api';
import { useUnsavedChanges } from './unsavedContext';
import { lazy, Suspense, useEffect, useRef, useState } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router';
import Link from './PortalLink';
import { useAuth } from '../features/auth/context';
import { ErrorState } from './common';
import ProfileSettings from './ProfileSettings';
import GlobalSearch from './GlobalSearch';
import { Icon } from './Icon';
import EmployeePhoto from '../features/employees/EmployeePhoto';
import { request } from '../services/api/client';
const NotificationBell = lazy(() => import('../features/notifications/NotificationBell'));
const links = [
  ['/dashboard', 'Dashboard', 'dashboard'],
  ['/accounts', 'Accounts & hierarchy', 'folder', 'PROJECT_VIEW'],
  ['/timesheets/my', 'My timesheet', 'clock', 'TIMESHEET_VIEW'],
  ['/timesheets/log-work', 'Log work', 'plus', 'WORKLOG_CREATE'],
  ['/timesheets/history', 'Timesheet history', 'history', 'TIMESHEET_VIEW'],
  ['/timesheets/team', 'Team timesheets', 'clock', 'TIMESHEET_TEAM_VIEW'],
  ['/projects', 'Projects', 'folder', 'PROJECT_VIEW'],
  // ['/invitations', 'Invitations', 'user', 'USER_VIEW'],
  ['/profile', 'My profile', 'user', 'EMPLOYEE_VIEW'],
  ['/employees/leave', 'Leave & WFH', 'clock', 'EMPLOYEE_VIEW'],
  ['/employees/org-chart', 'Organization chart', 'user', 'EMPLOYEE_VIEW'],
  ['/employees/admin', 'Employee management', 'user', 'EMPLOYEE_VIEW'],
  ['/employees/setup', 'Organization setup', 'settings', 'EMPLOYEE_CREATE'],
  ['/employees/reports', 'Employee reports', 'file', 'EMPLOYEE_VIEW'],
  ['/my-work', 'My work', 'user', 'ISSUE_VIEW'],
  ['/issues', 'Issues', 'issues', 'ISSUE_VIEW'],
  ['/audit', 'Audit logs', 'shield', 'AUDIT_VIEW'],
];
function SidebarProjects({ onNavigate }) {
  const location = useLocation();
  const recentProjects = useProjectsQuery();
  const allProjects = recentProjects.data || [];

  if (allProjects.length === 0) return null;

  // Extract current project ID from /projects/:projectId or query params (?ProjectId=...)
  const projectPathMatch = location.pathname.match(/^\/projects\/([^/]+)/);
  const pathProjectId = projectPathMatch ? projectPathMatch[1] : null;
  const searchParams = new URLSearchParams(location.search);
  const queryProjectId = searchParams.get('ProjectId') || searchParams.get('projectId');

  const activeId = pathProjectId || queryProjectId;
  const currentProject = activeId
    ? allProjects.find(
        (p) =>
          String(p.id) === String(activeId) ||
          String(p.key).toLowerCase() === String(activeId).toLowerCase(),
      )
    : null;

  const remainingProjects = currentProject
    ? allProjects.filter((p) => String(p.id) !== String(currentProject.id))
    : allProjects;

  const displayKey = (p) => (p.key || p.projectName || p.name || 'P').slice(0, 3).toUpperCase();
  const displayName = (p) => p.name || p.projectName || p.key || 'Project';

  return (
    <section
      className="sidebar-projects"
      aria-label={currentProject ? 'Remaining projects' : 'Recent projects'}
    >
      {currentProject && (
        <div className="sidebar-current-project">
          <div className="sidebar-project-eyebrow">Current project</div>
          <Link
            to={`/projects/${currentProject.id}`}
            className="sidebar-current-pill"
            onClick={() => onNavigate()}
          >
            <span className="project-monogram tone-active">
              {displayKey(currentProject)}
            </span>
            <div className="sidebar-current-info">
              <span className="sidebar-current-title">{displayName(currentProject)}</span>
              {currentProject.key && <span className="sidebar-current-key">{currentProject.key}</span>}
            </div>
            <span className="sidebar-current-dot" aria-hidden="true" />
          </Link>
        </div>
      )}

      <div className="sidebar-remaining-section">
        <div className="sidebar-section-heading">
          <h2>{currentProject ? 'Remaining projects' : 'Projects'}</h2>
          <span className="sidebar-count-badge">{remainingProjects.length}</span>
        </div>
        {remainingProjects.length === 0 ? (
          <p className="sidebar-empty-hint">No other projects</p>
        ) : (
          <div className="sidebar-projects-list">
            {remainingProjects.map((project, index) => (
              <Link
                key={project.id}
                to={`/projects/${project.id}`}
                className="sidebar-project-link"
                title={`${displayName(project)}${project.key ? ` (${project.key})` : ''}`}
                onClick={() => onNavigate()}
              >
                <span className={`project-monogram tone-${index % 5}`}>
                  {displayKey(project)}
                </span>
                <span className="sidebar-project-label">{displayName(project)}</span>
                {project.key && (
                  <span className="sidebar-project-key" title={`Project key: ${project.key}`}>
                    {project.key}
                  </span>
                )}
              </Link>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
export function AppLayout() {
  const { confirmLeave } = useUnsavedChanges();
  const { session, can, canAccessPortal, logout } = useAuth();
  const initials = (session?.user?.name || session?.user?.email || 'U')
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((word) => word[0])
    .join('')
    .toUpperCase();
  const [open, setOpen] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [searchPaletteOpen, setSearchPaletteOpen] = useState(false);
  const tenantThemeKey = session?.user?.tenantId
    ? `GRIDHUB:theme:${session.user.tenantId}`
    : 'GRIDHUB:theme';
  const isAdmin =
    session?.user?.role === 'ADMIN' ||
    (Array.isArray(session?.user?.roles) && session.user.roles.includes('ADMIN'));

  const [theme, setTheme] = useState(() => {
    try {
      const tenantKey = session?.user?.tenantId
        ? `GRIDHUB:theme:${session.user.tenantId}`
        : 'GRIDHUB:theme';
      return (
        (typeof localStorage !== 'undefined' && typeof localStorage.getItem === 'function'
          ? localStorage.getItem(tenantKey) || localStorage.getItem('GRIDHUB:theme')
          : null) || 'light'
      );
    } catch {
      return 'light';
    }
  });

  const profileRef = useRef(null);
  const [signingOut, setSigningOut] = useState(false);
  const [error, setError] = useState(null);
  const location = useLocation();
  const dashboard = location.pathname === '/dashboard';
  const currentKind = portalKind(location.pathname);
  const hubPortal = currentKind === 'hub' || activePortal === 'hub' || location.pathname === '/home' || location.pathname.startsWith('/home');
  const employeePortal = currentKind === 'employee';
  const timesheetPortal = currentKind === 'timesheet';

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

    // Periodically poll (every 60 seconds) to keep users in sync across different machines/browsers
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

    // Persist to backend database for the entire tenant organization
    try {
      await request('/api/tenants/theme', {
        method: 'PUT',
        body: { theme: next },
      });
    } catch (err) {
      console.warn('Failed to persist tenant theme:', err);
    }
  }

  useEffect(() => {
    // Escape closes whatever is open. There are no other keyboard shortcuts.
    const closeOnEscape = (e) => {
      if (e.key !== 'Escape') return;
      setOpen(false);
      setSearchPaletteOpen(false);
      if (profileRef.current?.open) {
        profileRef.current.open = false;
        profileRef.current.querySelector('summary')?.focus();
      }
    };
    const dismissProfile = (event) => {
      if (profileRef.current?.open && !profileRef.current.contains(event.target))
        profileRef.current.open = false;
    };
    window.addEventListener('pointerdown', dismissProfile);
    window.addEventListener('keydown', closeOnEscape);
    return () => {
      window.removeEventListener('keydown', closeOnEscape);
      window.removeEventListener('pointerdown', dismissProfile);
    };
  }, []);
  async function signOut() {
    setSigningOut(true);
    try {
      await logout();
    } catch (e) {
      setError(e);
    } finally {
      setSigningOut(false);
    }
  }
  return (
    <div className={`app-shell reference-dashboard-shell${dashboard ? '' : ' workspace-screens'}${hubPortal ? ' hub-no-sidebar' : ''}`}>
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      <header className="topbar">
        <div className="topbar-left">
          {!hubPortal && (
            <button
              className="mobile-toggle icon-button"
              aria-label="Toggle navigation"
              aria-expanded={open}
              aria-controls="workspace-navigation"
              onClick={() => setOpen(!open)}
            >
              <Icon name="menu" />
            </button>
          )}
          <Link className="brand" to={hubPortal ? '/home' : portalLanding() || '/dashboard'}>
            <PortalLogo portal={hubPortal ? 'hub' : activePortal} />
            {hubPortal ? 'AIWorkHub' : portalName}
          </Link>
          <div className="topbar-divider" />
          <span className="tenant-chip">
            <Icon name="shield" />
            {session?.user?.tenantName || 'Workspace'}
            <span className="tenant-chip-label"> / Workspace</span>
          </span>
          {hubPortal && (
            <>
              <div className="topbar-divider" />
              <div className="hub-topbar-pill-group">
                <NavLink
                  to="/home"
                  className={({ isActive }) => `hub-nav-pill ${isActive ? 'active' : ''}`}
                >
                  <Icon name="home" />
                  <span>AI Chat</span>
                </NavLink>
                <NavLink
                  to="/knowledge"
                  className={({ isActive }) => `hub-nav-pill ${isActive ? 'active' : ''}`}
                >
                  <Icon name="file" />
                  <span>Knowledge Base</span>
                </NavLink>
              </div>
            </>
          )}
        </div>
        <div className="topbar-center">
          {employeePortal || portalKind(location.pathname) === 'employee' ? (
            can('EMPLOYEE_VIEW') && (
              <div
                className="global-search topbar-search-bar"
                role="button"
                tabIndex={0}
                onClick={() => setSearchPaletteOpen(true)}
                onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') setSearchPaletteOpen(true); }}
                aria-label="Search people, directory, employees"
                title="Search people, directory, employees"
              >
                <span className="search-icon-wrap" aria-hidden="true">
                  <Icon name="search" />
                </span>
                <span className="search-placeholder-text">
                  Search people, directory, employees…
                </span>
              </div>
            )
          ) : (
            can('ISSUE_VIEW') && (
              <div
                className="global-search topbar-search-bar"
                role="button"
                tabIndex={0}
                onClick={() => setSearchPaletteOpen(true)}
                onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') setSearchPaletteOpen(true); }}
                aria-label="Quick search"
                title="Search workspace, issues, projects"
              >
                <span className="search-icon-wrap" aria-hidden="true">
                  <Icon name="search" />
                </span>
                <span className="search-placeholder-text">
                  Search workspace, issues, projects…
                </span>
              </div>
            )
          )}
        </div>
        <div className="topbar-actions">
          {isAdmin && (
            <button
              className="icon-button theme-toggle-btn"
              aria-label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
              title={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
              onClick={toggleTheme}
              style={{ background: 'transparent', border: 'none', cursor: 'pointer', padding: '6px', color: 'var(--theme-text)' }}
            >
              <Icon name={theme === 'dark' ? 'sun' : 'moon'} />
            </button>
          )}
          {can('ISSUE_VIEW') && (
            <Suspense fallback={null}>
              <NotificationBell key={`${session?.user?.tenantId}:${session?.user?.id}`} />
            </Suspense>
          )}
          <PortalLauncher />
          <details className="user-menu" ref={profileRef}>
            <summary
              className="user-avatar-trigger"
              aria-label={`User profile: ${session?.user?.name || session?.user?.email || 'User'}`}
              title={session?.user?.name || session?.user?.email || 'User profile'}
            >
              {session?.user?.profilePhotoUrl ? (
                <EmployeePhoto
                  src={session.user.profilePhotoUrl}
                  alt=""
                  className="avatar avatar-img"
                  style={{ width: '32px', height: '32px', borderRadius: '50%', objectFit: 'cover' }}
                />
              ) : (
                <span className="avatar">{initials}</span>
              )}
              <span className="sr-only">{session?.user?.name || session?.user?.email}</span>
            </summary>
            <div className="user-popover">
              <div className="profile-identity">
                {session?.user?.profilePhotoUrl ? (
                  <EmployeePhoto
                    src={session.user.profilePhotoUrl}
                    alt=""
                    className="profile-avatar profile-avatar-img"
                    style={{ width: '40px', height: '40px', borderRadius: '50%', objectFit: 'cover' }}
                  />
                ) : (
                  <span className="profile-avatar" aria-hidden="true">
                    {initials}
                  </span>
                )}
                <div className="profile-identity-text">
                  <strong>{session?.user?.name || 'Your account'}</strong>
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
                  <strong>{session?.user?.tenantName || 'Workspace'}</strong>
                </div>
              </div>
              <div className="profile-actions">
                {session?.user?.role === 'ADMIN' && (
                  <button
                    className="profile-settings-link"
                    onClick={() => {
                      profileRef.current.open = false;
                      profileRef.current.querySelector('summary')?.focus();
                      setSettingsOpen(true);
                    }}
                  >
                    <Icon name="settings" />
                    <span>Settings</span>
                    <span className="profile-action-arrow" aria-hidden="true">
                      ›
                    </span>
                  </button>
                )}
                <NavLink
                  className="profile-settings-link"
                  to="/plan"
                  onClick={() => {
                    profileRef.current.open = false;
                  }}
                >
                  <Icon name="shield" />
                  <span>Your plan</span>
                  <span className="profile-action-arrow" aria-hidden="true">
                    ›
                  </span>
                </NavLink>
                <button
                  className="profile-signout"
                  onClick={() => confirmLeave(signOut)}
                  disabled={signingOut}
                >
                  <Icon name="logout" />
                  <span>{signingOut ? 'Signing out…' : 'Sign out'}</span>
                </button>
              </div>
              <ErrorState error={error} />
            </div>
          </details>
        </div>
      </header>
      <ProfileSettings
        user={session.user}
        open={settingsOpen}
        close={() => setSettingsOpen(false)}
      />
      {!hubPortal && (
        <button
          className={open ? 'nav-backdrop is-open' : 'nav-backdrop'}
          aria-label="Close navigation"
          onClick={() => setOpen(false)}
          tabIndex={open ? 0 : -1}
        />
      )}
      {!hubPortal && (
        <aside id="workspace-navigation" className={open ? 'sidebar is-open' : 'sidebar'}>
        <Link className="reference-brand" to={portalLanding() || '/dashboard'}>
          <PortalLogo />
          {portalName}
        </Link>
        <div className="workspace-label">
          {hubPortal ? 'Central Home' : employeePortal ? 'Employee portal' : timesheetPortal ? 'Timesheet portal' : 'Workspace'}
        </div>
        {hubPortal ? (
          <nav aria-label="Hub navigation">
            <NavLink to="/home" end onClick={() => setOpen(false)}>
              <Icon name="home" />
              <span>AI Copilot</span>
            </NavLink>
            <NavLink to="/dashboard" onClick={() => setOpen(false)}>
              <Icon name="dashboard" />
              <span>Dashboard</span>
            </NavLink>
            <NavLink to="/issues" onClick={() => setOpen(false)}>
              <Icon name="issues" />
              <span>Issues</span>
            </NavLink>
            <NavLink to="/projects" onClick={() => setOpen(false)}>
              <Icon name="folder" />
              <span>Projects</span>
            </NavLink>
            <NavLink to="/timesheets/my" onClick={() => setOpen(false)}>
              <Icon name="clock" />
              <span>Timesheets</span>
            </NavLink>
          </nav>
        ) : (
          <nav aria-label="Main navigation">
            {links
              .filter(
                ([path, , , permission]) =>
                  !employeePortal &&
                  !timesheetPortal &&
                  path !== '/profile' &&
                  !path.startsWith('/employees') &&
                  !path.startsWith('/timesheets') &&
                  (!permission || can(permission)) &&
                  (path !== '/invitations' || session?.user?.role === 'ADMIN'),
              )
              .map(([path, title, icon]) => (
                <NavLink
                  key={path}
                  to={path}
                  end={path === '/employees'}
                  onClick={() => setOpen(false)}
                >
                  <Icon name={icon} />
                  {title}
                </NavLink>
              ))}
          </nav>
        )}
        {timesheetPortal && (canAccessPortal ? canAccessPortal('timesheet') : true) && (
          <nav aria-label="Timesheet navigation">
            {links
              .filter(
                ([path, , , permission]) =>
                  path.startsWith('/timesheets') && can('TIMESHEET_VIEW') && can(permission),
              )
              .map(([path, title, icon]) => (
                <NavLink key={path} to={path} onClick={() => setOpen(false)}>
                  <Icon name={icon} />
                  {title}
                </NavLink>
              ))}
          </nav>
        )}
        {employeePortal && (canAccessPortal ? canAccessPortal('employee') : true) && can('EMPLOYEE_VIEW') && (
          <>
            <div className="workspace-label">Employee</div>
            <nav aria-label="Employee navigation">
              {links
                .filter(
                  ([path, , , permission]) =>
                    (path === '/profile' || path.startsWith('/employees')) &&
                    (!permission || can(permission)) &&
                    (path !== '/employees/reports' || ['ADMIN', 'HR'].includes(session?.user?.role)) &&
                    (path !== '/employees/setup' || [session?.user?.role, ...(session?.user?.roles || [])].some(role => ['ADMIN', 'HR'].includes(role))),
                )
                .map(([path, title, icon]) => (
                  <NavLink
                    key={path}
                    to={path}
                    end={path === '/employees'}
                    onClick={() => setOpen(false)}
                  >
                    <Icon name={icon} />
                    {title}
                  </NavLink>
                ))}
            </nav>
          </>
        )}
        {can('PROJECT_VIEW') && !employeePortal && !timesheetPortal && !hubPortal && (
          <SidebarProjects onNavigate={() => setOpen(false)} />
        )}
        {!hubPortal && session?.user?.role === 'ADMIN' && (
          <button
            className="sidebar-settings"
            aria-label="Workspace settings"
            onClick={() => {
              setSettingsOpen(true);
              setOpen(false);
            }}
          >
            <Icon name="settings" />
            Settings
          </button>
        )}
        {!hubPortal && (
          <div className="sidebar-foot">
            <Icon name="shield" />
            <div>
              <strong>Workspace access</strong>
              <small>{session?.user?.role || 'Authenticated session'}</small>
            </div>
          </div>
        )}
        </aside>
      )}
      <div className="workspace">
        <main id="main" tabIndex="-1">
          <Outlet />
        </main>
        {!hubPortal && (
          <footer className="app-footer">
            {portalName}<span>Enterprise workspace · {session?.user?.tenantName || 'Workspace'}</span>
          </footer>
        )}
      </div>
      {searchPaletteOpen && (
        <GlobalSearch open={searchPaletteOpen} onClose={() => setSearchPaletteOpen(false)} />
      )}
    </div>
  );
}
