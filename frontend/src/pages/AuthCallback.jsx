import { useEffect } from 'react';
import { Navigate, useSearchParams } from 'react-router';
import { useAuth } from '../features/auth/context';
import { landingPage } from '../utils/preferences';
import { LoadingState } from '../components/common';

export default function AuthCallback() {
  const [params] = useSearchParams();
  const error = params.get('error');
  const { session, loading, error: sessionError, restore } = useAuth();
  useEffect(() => {
    // This message is only a completion signal; the main window verifies the
    // cookie session with the backend before granting access to any route.
    if (window.opener && (error || (!loading && session))) {
      window.opener.postMessage(
        error ? { type: 'GRIDHUB:auth-error', code: error } : { type: 'GRIDHUB:auth-complete' },
        window.location.origin,
      );
      window.close();
    }
  }, [error, loading, session]);
  // Full-page sign-in and browsers that sever the opener continue normally.
  if (error) return <Navigate to={`/login?error=${encodeURIComponent(error)}`} replace />;
  if (loading)
    return (
      <div role="status">
        <LoadingState />
        <p>Completing sign-in…</p>
      </div>
    );
  if (!session)
    return (
      <div className="sso-page">
        <main className="sso-main">
          <section className="sso-card">
            <h1>Sign-in could not be completed</h1>
            <p role="alert">
              {sessionError || 'Your session could not be restored. Please sign in again.'}
            </p>
            <button onClick={() => restore()}>Retry existing session</button>
            <p>
              <a href="/login">Return to sign in</a>
            </p>
          </section>
        </main>
      </div>
    );
  return (
    <Navigate
      to={landingPage(session.user, (permission) => session.permissions.includes(permission))}
      replace
    />
  );
}
