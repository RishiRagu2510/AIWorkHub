import { portalLanding, activePortal } from './portals';
import { syncPreference } from './preferenceSync.js';
import { hasPortalAccess } from '../features/auth/session';

export function preferenceKey(user, name) {
  return `GRIDHUB:${name}:${user.tenantId}:${user.id}`;
}
export function readPreference(key, fallback = {}) {
  try {
    const value = JSON.parse(localStorage.getItem(key));
    return value && typeof value === 'object' && !Array.isArray(value) ? value : fallback;
  } catch {
    return fallback;
  }
}
export function writePreference(key, value) {
  try {
    localStorage.setItem(key, JSON.stringify(value));
    syncPreference(key, value);
  } catch {
    /* Storage is optional. */
  }
}
export function landingPage(user, can, session) {
  // The landing site is the page that offers the portals: signing in always ends there.
  if (activePortal === 'landing') return '/';
  const portal = activePortal;
  // If the user does not have license/access to the current portal, redirect to an authorized landing
  if (session && !hasPortalAccess(session, portal)) {
    if (hasPortalAccess(session, 'workspace')) return '/dashboard';
    if (hasPortalAccess(session, 'employee')) return '/profile';
    if (hasPortalAccess(session, 'timesheet')) return '/timesheets/my';
    if (hasPortalAccess(session, 'hub')) return '/home';
  }

  if (portalLanding()) return portalLanding();
  const { landing } = readPreference(preferenceKey(user, 'preferences'));
  if (landing === '/issues' && can('ISSUE_VIEW')) return landing;
  if (landing === '/projects' && can('PROJECT_VIEW')) return landing;
  return '/dashboard';
}
