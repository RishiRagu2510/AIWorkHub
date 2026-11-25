import { Component, Suspense, lazy, useState, useEffect } from 'react';
import { createBrowserRouter, createRoutesFromElements, RouterProvider, Navigate, Outlet, Route, useLocation, useRouteError } from 'react-router';
import { Provider } from 'react-redux';
import { store } from './app/store';
import { AuthProvider, PermissionGuard, PortalGuard } from './features/auth/AuthProvider';
import { useAuth } from './features/auth/context';
import { LoadingState, NoticeProvider, EmptyState } from './components/common';
import PlanRestricted from './components/PlanRestricted';
import { AppLayout } from './components/AppLayout';
import { UnsavedChangesProvider } from './components/UnsavedChanges';
import PortalLink from './components/PortalLink';
import { portalLanding, foreignPortalUrl } from './utils/portals';
import { landingPage } from './utils/preferences';
import Login from './pages/Login';
import AuthCallback from './pages/AuthCallback';
import VerifyEmail from './pages/VerifyEmail';
const PlanPage = lazy(() => import('./pages/PlanPage'));
import NotificationsPage from './pages/NotificationsPage';
function Landing() {
 const { session, can } = useAuth();
 return <Navigate to={landingPage(session.user, can, session)} replace />;
}
function PendingAccess() {
 const { session, restore, logout } = useAuth();
 const [error, setError] = useState('');
 const [busy, setBusy] = useState(false);
 async function signOut() {
  setBusy(true);
  try { await logout(); } catch { setError('Unable to sign out. Please try again.'); }
  finally { setBusy(false); }
 }
 return <div className="sso-page"><main className="sso-main"><section className="sso-card">
  <h1>Welcome, {session.user.name || session.user.email}</h1>
  <p>You are signed in. Your administrator can now assign your role to enable access to your workspace.</p>
  <button onClick={() => restore()}>Check access</button>
  <button disabled={busy} onClick={signOut}>Sign out</button>
  {error && <p role="alert">{error}</p>}
 </section></main></div>;
}
export function ProtectedRoute() {
 const { session, loading } = useAuth();
 return loading ? <LoadingState /> : session ? (session.user.role ? <Outlet /> : <PendingAccess />) : <Navigate to="/login" replace />;
}
export function Access({ permission, portal, children }) {
  const content = permission ? (
    <PermissionGuard permission={permission} fallback={<EmptyState title="Access restricted">You do not have permission to view this page. <PortalLink to={portalLanding() || '/dashboard'}>Return home</PortalLink></EmptyState>}>
      {children}
    </PermissionGuard>
  ) : (
    children
  );

  if (portal) {
    return (
      <PortalGuard portal={portal} fallback={<PlanRestricted portal={portal} />}>
        {content}
      </PortalGuard>
    );
  }
  return content;
}
function OtherPortal() {
 const location = useLocation();
 const elsewhere = foreignPortalUrl(`${location.pathname}${location.search}${location.hash}`);
 useEffect(() => {
  if (elsewhere) window.location.replace(elsewhere);
 }, [elsewhere]);
 if (elsewhere) return <LoadingState />;
 return (
  <EmptyState title="Page not found">
   <PortalLink to="/dashboard">Return to Dashboard</PortalLink>
  </EmptyState>
 );
}
export function RouteErrorFallback() {
 const error = useRouteError();
 const isChunkError =
  error?.name === 'TypeError' ||
  error?.message?.includes('Failed to fetch dynamically imported module') ||
  error?.message?.includes('Importing a module script failed');

 return (
  <div className="panel error-state-panel" style={{ padding: '2.5rem', textAlign: 'center', margin: '2rem auto', maxWidth: '600px' }}>
   <h2>{isChunkError ? 'Unable to load page module' : 'Something went wrong'}</h2>
   <p style={{ margin: '1rem 0', color: 'var(--color-text-muted, #666)' }}>
    {isChunkError
     ? 'The requested page module could not be fetched. A newer version of the application may be available or the dev server was restarted.'
     : error?.statusText || error?.message || 'An unexpected error occurred while loading this view.'}
   </p>
   <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'center', marginTop: '1.5rem' }}>
    <button className="primary" onClick={() => window.location.reload()}>
     Reload Page
    </button>
    <PortalLink to={portalLanding() || '/dashboard'} className="button">
     Return Home
    </PortalLink>
   </div>
  </div>
 );
}
class ErrorBoundary extends Component {
 state = { failed: false };
 static getDerivedStateFromError() { return { failed: true }; }
 render() { return this.state.failed ? <div className="error"><h1>Something went wrong</h1><button onClick={() => window.location.reload()}>Reload</button></div> : this.props.children; }
}
export default function App({ routes }) {
 const [router] = useState(() => createBrowserRouter(createRoutesFromElements(
  <Route errorElement={<RouteErrorFallback />}>
   <Route path="/verify-email" element={<VerifyEmail />} errorElement={<RouteErrorFallback />} />
   <Route path="/login" element={<Login />} errorElement={<RouteErrorFallback />} />
   <Route path="/auth/callback" element={<AuthCallback />} errorElement={<RouteErrorFallback />} />
   <Route element={<ProtectedRoute />} errorElement={<RouteErrorFallback />}>
    <Route element={<UnsavedChangesProvider><AppLayout /></UnsavedChangesProvider>} errorElement={<RouteErrorFallback />}>
     <Route index element={<Landing />} errorElement={<RouteErrorFallback />} />
     <Route path="notifications" element={<NotificationsPage />} errorElement={<RouteErrorFallback />} />
     <Route path="plan" element={<PlanPage />} errorElement={<RouteErrorFallback />} />
     {routes}
     <Route path="*" element={<OtherPortal />} errorElement={<RouteErrorFallback />} />
    </Route>
   </Route>
  </Route>
 )));
 useEffect(() => () => router.dispose(), [router]);
 return <ErrorBoundary><Provider store={store}><AuthProvider><NoticeProvider><Suspense fallback={<LoadingState />}><RouterProvider router={router} /></Suspense></NoticeProvider></AuthProvider></Provider></ErrorBoundary>;
}
