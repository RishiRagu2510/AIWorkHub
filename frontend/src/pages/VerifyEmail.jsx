import { useEffect, useRef, useState } from 'react';
import Link from '../components/PortalLink';
import { passwordRequest } from '../features/auth/passwordApi';
export default function VerifyEmail() {
  const [token] = useState(
    () => new URLSearchParams(window.location.hash.slice(1)).get('token') || '',
  );
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState('');
  const lock = useRef(false);
  useEffect(() => {
    window.history.replaceState(
      window.history.state,
      '',
      window.location.pathname + window.location.search,
    );
  }, []);
  async function submit(event) {
    event.preventDefault();
    if (lock.current) return;
    const form = event.currentTarget;
    const values = Object.fromEntries(new FormData(form));
    if (values.password !== values.confirmPassword) {
      setError('Passwords do not match.');
      return;
    }
    if (!values.password.trim()) {
      setError('Choose a password containing more than spaces.');
      return;
    }
    lock.current = true;
    setBusy(true);
    setError('');
    try {
      await passwordRequest('verify', { token, password: values.password });
      form.reset();
      setDone(true);
    } catch (failure) {
      setError(failure.message);
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  return (
    <main className="sso-main">
      <section className="sso-card password-signin" aria-labelledby="verify-title">
        <h1 id="verify-title">
          {done ? 'Your email is verified' : 'Verify email and set password'}
        </h1>
        {done ? (
          <p role="status">Your password is ready. You can now sign in with your company email.</p>
        ) : !token ? (
          <p role="alert">
            Open the verification link from your email. You can request a new link on the sign-in
            page.
          </p>
        ) : (
          <form onSubmit={submit}>
            <p>Choose a password with 12–128 characters. Spaces and passphrases are welcome.</p>
            <fieldset disabled={busy}>
              <label>
                New password
                <input
                  name="password"
                  type="password"
                  autoComplete="new-password"
                  required
                  minLength={12}
                  maxLength={128}
                />
              </label>
              <label>
                Confirm password
                <input
                  name="confirmPassword"
                  type="password"
                  autoComplete="new-password"
                  required
                  minLength={12}
                  maxLength={128}
                />
              </label>
              <button className="primary" type="submit">
                {busy ? 'Verifying…' : 'Verify and set password'}
              </button>
            </fieldset>
            {error && (
              <p role="alert" className="password-auth-error">
                {error}
              </p>
            )}
          </form>
        )}
        <Link to="/login">Return to sign in</Link>
      </section>
    </main>
  );
}
