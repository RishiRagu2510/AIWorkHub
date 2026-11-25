import PortalLogo from '../components/PortalLogo';
import { portalName, activePortal } from '../utils/portals';
import PasswordSignIn from '../features/auth/PasswordSignIn';
import { landingPage } from '../utils/preferences';
import { useEffect } from 'react';
import GoogleSignIn from '../features/auth/GoogleSignIn';
import { Navigate, useSearchParams } from 'react-router';
import { useNotice } from '../components/noticeContext';
import { loginErrorMessage } from '../features/auth/loginError';
import { useAuth } from '../features/auth/context';
import { LoadingState } from '../components/common';
const portalCopy = {
 landing: ['AIWorkHub', 'Sign in once, then open the portals your company has.', 'Connected in one place.'],
 hub: ['AIWorkHub Central Portal', 'Your unified launchpad for all workspaces.', 'Connected in one place.'],
 workspace: ['Enterprise work management', 'Your projects, issues, and team activity.', 'Connected in one workspace.'],
 employee: ['Employee portal', 'Your profile, career, and organization.', 'Connected through Persona.'],
 timesheet: ['Time management', 'Your timesheets and work logs.', 'Track your work with Chronos.'],
}[activePortal] || ['AIWorkHub Portal', 'Connected in one place.', 'All workspaces unified.'];
export default function Login() {
  const { session, loading } = useAuth();
  const notice = useNotice();
  const [params, setParams] = useSearchParams();
  const loginError = params.get('error');
  useEffect(() => {
    if (!loginError) return;
    notice({ type: 'error', message: loginErrorMessage(loginError) });
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        next.delete('error');
        return next;
      },
      { replace: true },
    );
  }, [loginError, notice, setParams]);
  if (session)
    return (
      <Navigate
        to={landingPage(session.user, (permission) => session.permissions.includes(permission), session)}
        replace
      />
    );
  return (
    <div className="sso-page">
      <header className="sso-header">
        <a className="brand" href="/login" aria-label={`${portalName} home`}>
          <PortalLogo />
          {portalName}
        </a>
      </header>
      <main className="sso-main">
        <div className="sso-center">
          <section className="sso-card" aria-labelledby="sign-in-title">
            <div className="brand sso-card-brand">
              <PortalLogo />
              {portalName}
            </div>
            <div className="sso-badge">{portalCopy[0]}</div>
            <h1 id="sign-in-title">Sign in to your organization</h1>
            <p className="sso-intro">
              {portalCopy[1]}
              <br />
              {portalCopy[2]}
            </p>
            {loading ? (
              <div role="status">
                <LoadingState />
                <p>Restoring your session…</p>
              </div>
            ) : (
              <div className="sso-actions">
                <PasswordSignIn />
                <div className="signin-divider">
                  <span>or continue with Google</span>
                </div>
                <GoogleSignIn />
                <p className="sso-account-note">
                  Use your organization’s authorized account.
                  <br />
                </p>
              </div>
            )}
            <div className="sso-help">
              <details>
                <summary>
                  <span aria-hidden="true">ⓘ</span> Trouble signing in?
                </summary>
                <p>
                  Use the Google account provided by your organization. If your account is not
                  authorized, contact your company administrator to request access.
                </p>
              </details>
            </div>
          </section>
          <p className="sso-caption">Access is managed by your organization.</p>
        </div>
      </main>
      <footer className="sso-footer">
        <span>{portalName}</span>
        <span>{portalCopy[0]}</span>
      </footer>
    </div>
  );
}
