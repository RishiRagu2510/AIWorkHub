import { useRef, useState } from 'react';
import { useAuth } from './context';
import { passwordRequest } from './passwordApi';
export default function PasswordSignIn() {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const lock = useRef(false);
  const { restore } = useAuth();
  async function submit(event) {
    event.preventDefault();
    if (lock.current) return;
    lock.current = true;
    setBusy(true);
    setError('');
    const form = event.currentTarget;
    const values = Object.fromEntries(new FormData(form));
    try {
      await passwordRequest('login', {
        email: values.email.trim(),
        password: values.password,
      });
      form.reset();
      await restore();
    } catch (failure) {
      setError(failure.message);
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  return (
    <section className="password-signin" aria-label="Email sign-in">
      <form onSubmit={submit}>
        <fieldset disabled={busy}>
          <label>
            Company email
            <input
              name="email"
              type="email"
              autoComplete="username"
              required
              maxLength={320}
              placeholder="you@company.com"
            />
          </label>
          <label>
            Password
            <input
              name="password"
              type="password"
              autoComplete="current-password"
              required
              maxLength={128}
            />
          </label>
          <button className="primary" type="submit">
            {busy ? 'Please wait…' : 'Sign in with email'}
          </button>
        </fieldset>
      </form>
      {error && (
        <p role="alert" className="password-auth-error">
          {error}
        </p>
      )}
    </section>
  );
}
