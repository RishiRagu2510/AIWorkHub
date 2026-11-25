export const getActivePortal = () => import.meta.env.VITE_PORTAL || 'workspace';
export const activePortal = import.meta.env.VITE_PORTAL || 'workspace';
export const portalName = { hub: 'AIWorkHub', workspace: 'GridHub', employee: 'Persona', timesheet: 'Chronos', landing: 'LaunchPad' }[import.meta.env.VITE_PORTAL] || 'GridHub';

export function portalKind(path) {
  path = path.split(/[?#]/, 1)[0];
  if (path === '/home' || path === '/knowledge') return 'hub';
  if (path === '/profile' || path.startsWith('/employees')) return 'employee';
  if (path.startsWith('/timesheets')) return 'timesheet';
  return 'workspace';
}
export function portalUrl(
  path,
  location = typeof window !== 'undefined'
    ? window.location
    : { hostname: 'localhost', origin: 'http://localhost:5173' },
) {
  const kind = portalKind(path);
  const configured = {
    hub: import.meta.env.VITE_HUB_PORTAL_URL,
    employee: import.meta.env.VITE_EMPLOYEE_PORTAL_URL,
    timesheet: import.meta.env.VITE_TIMESHEET_PORTAL_URL,
    workspace: import.meta.env.VITE_WORKSPACE_URL,
  }[kind];
  if (configured) return new URL(path, configured).href;
  if (['localhost', '127.0.0.1', '[::1]'].includes(location.hostname)) {
    const url = new URL(location.origin);
    url.port = { hub: '5172', workspace: '5173', employee: '5174', timesheet: '5175' }[kind];
    return new URL(path, url).href;
  }
  return path;
}
export function portalLanding() {
  return { hub: '/home', employee: '/profile', timesheet: '/timesheets/my' }[import.meta.env.VITE_PORTAL] || null;
}

export function portalHome(path) {
  return { hub: '/home', employee: '/profile', timesheet: '/timesheets/my', workspace: '/dashboard' }[portalKind(path)];
}

export function portalLinkProps(to, current = getActivePortal()) {
  if (to === '/login') return { to };
  const targetPortal = portalKind(to);
  if (targetPortal === current) {
    return { to };
  }
  const href = portalUrl(to);
  return {
    to: href,
    target: '_blank',
    rel: 'noopener noreferrer',
  };
}

/**
 * Where to send a visitor whose address belongs to another portal.
 * Every portal is its own site, so a path that another portal owns (for example /timesheets/my on GridHub) is opened on that
 * portal's site instead of showing "page not found". Returns null when the path is this portal's own, or when no other
 * site is known (the target would be this same origin), so the caller shows its normal not-found page and cannot loop.
 */
export function foreignPortalUrl(path, location = typeof window !== 'undefined' ? window.location : undefined, current = getActivePortal()) {
  if (!location || portalKind(path) === current) return null;
  const target = portalUrl(path, location);
  try {
    const url = new URL(target, location.origin);
    return url.origin === location.origin ? null : url.href;
  } catch {
    return null;
  }
}
