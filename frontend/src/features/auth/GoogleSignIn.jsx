import { useEffect, useRef, useState } from 'react';
import { apiBase, request } from '../../services/api/client';
import { loginErrorMessage } from './loginError';
import { useAuth } from './context';
import { useNotice } from '../../components/noticeContext';

let scriptPromise;
function loadGoogleIdentity() {
  if (window.google?.accounts?.id) return Promise.resolve(window.google.accounts.id);
  if (!scriptPromise) {
    scriptPromise = new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = 'https://accounts.google.com/gsi/client';
      script.async = true;
      const timer = setTimeout(() => fail(), 15000);
      function fail() {
        clearTimeout(timer);
        script.remove();
        scriptPromise = undefined;
        reject(new Error('Google sign-in could not load. Check your connection and try again.'));
      }
      script.onerror = fail;
      script.onload = () => {
        clearTimeout(timer);
        if (window.google?.accounts?.id) resolve(window.google.accounts.id);
        else fail();
      };
      document.head.appendChild(script);
    });
  }
  return scriptPromise;
}

export default function GoogleSignIn() {
  const container = useRef(null);
  const submitting = useRef(false);
  const { restore } = useAuth();
  const notice = useNotice();
  const callbacks = useRef({ restore, notice });
  useEffect(() => {
    callbacks.current = { restore, notice };
  }, [restore, notice]);
  const [attempt, setAttempt] = useState(0);
  const [state, setState] = useState('loading');
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    const element = container.current;
    async function setup() {
      try {
        const [google, config] = await Promise.all([
          loadGoogleIdentity(),
          request('/api/auth/google/configuration', { session: true, signal: controller.signal }),
        ]);
        if (!active) return;
        google.initialize({
          client_id: config.clientId,
          nonce: config.nonce,
          auto_select: false,
          ux_mode: 'popup',
          callback: async ({ credential }) => {
            if (!active || submitting.current) return;
            submitting.current = true;
            setState('submitting');
            try {
              if (!credential)
                throw new Error('Google did not return a sign-in credential. Please try again.');
              const response = await fetch(`${apiBase}/api/auth/google`, {
                method: 'POST',
                credentials: 'include',
                signal: controller.signal,
                headers: {
                  'Content-Type': 'application/json',
                  'X-Google-CSRF': config.csrfToken,
                  'ngrok-skip-browser-warning': 'true',
                },
                body: JSON.stringify({ credential, nonce: config.nonce }),
              });
              if (!response.ok) {
                const body = await response.json().catch(() => ({}));
                throw new Error(
                  response.status >= 500
                    ? 'Sign-in is temporarily unavailable. Please try again.'
                    : loginErrorMessage(body.code),
                );
              }
              if (active) {
                const restored = await callbacks.current.restore({ broadcast: true });
                if (!restored) throw new Error('Unable to restore your session. Please try again.');
              }
            } catch (error) {
              if (!active) return;
              callbacks.current.notice({
                type: 'error',
                message: error.message || 'Unable to sign in. Please try again.',
              });
              setState('loading');
              setAttempt((value) => value + 1);
            } finally {
              submitting.current = false;
            }
          },
        });
        const isDark = document.documentElement.dataset.theme === 'dark';
        const buttonWidth = Math.max(280, Math.min(400, container.current?.parentElement?.clientWidth || element.clientWidth || 396));
        google.renderButton(element, {
          theme: isDark ? 'filled_black' : 'outline',
          size: 'large',
          text: 'continue_with',
          shape: 'rectangular',
          width: buttonWidth,
        });
        setState('ready');
      } catch (error) {
        if (active) {
          setError(error.message || 'Unable to load Google sign-in.');
          setState('error');
        }
      }
    }
    setup();
    return () => {
      active = false;
      controller.abort();
      element?.replaceChildren();
    };
  }, [attempt]);
  return (
    <div
      className="google-identity-signin"
      aria-busy={state === 'loading' || state === 'submitting'}
    >
      <div ref={container} hidden={state !== 'ready'} />
      {state === 'loading' && <p role="status">Loading Google sign-in…</p>}
      {state === 'submitting' && <p role="status">Verifying your company account…</p>}
      {error && (
        <div role="alert">
          <p>{error}</p>
          <button
            onClick={() => {
              setError('');
              setState('loading');
              setAttempt((value) => value + 1);
            }}
          >
            Retry Google sign-in
          </button>
        </div>
      )}
      {/* {state !== 'submitting' && (
        <p>
          <a href={loginUrl}>Continue with Google in this tab</a>
        </p>
      )} */}
    </div>
  );
}
