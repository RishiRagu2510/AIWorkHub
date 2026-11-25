import { useState } from 'react';
import { reviewPreferences, resolvePreference } from '../utils/preferenceSync';
import { ErrorState, JsonViewer } from './common';
export default function PreferenceSyncReview({ user }) {
  const [records, setRecords] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  async function load() {
    setBusy(true);
    setError(null);
    try {
      setRecords(await reviewPreferences(user));
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  async function resolve(item, local) {
    setBusy(true);
    setError(null);
    try {
      await resolvePreference(user, item, local);
      setRecords(records.filter((r) => r.key !== item.key));
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section>
      <button onClick={load} disabled={busy}>
        Review account sync
      </button>
      <ErrorState error={error} />
      {records && (
        <>
          <p className="hint">
            Compare this browser with your account before choosing which version to keep. Loaded
            account values apply to existing screens when you next sign in.
          </p>
          {!records.length && <p>All reviewed preferences are saved.</p>}
          {records.map((item) => (
            <details key={item.key}>
              <summary>{item.key}</summary>
              <strong>This browser</strong>
              <JsonViewer value={JSON.stringify(item.local)} />
              <strong>Your account</strong>
              <JsonViewer value={JSON.stringify(item.value)} />
              <div className="actions">
                <button disabled={busy} onClick={() => resolve(item, true)}>
                  Keep browser version
                </button>
                <button disabled={busy} onClick={() => resolve(item, false)}>
                  Use account version
                </button>
              </div>
            </details>
          ))}
        </>
      )}
    </section>
  );
}
