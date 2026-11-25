import PreferenceSyncReview from './PreferenceSyncReview';
import { useEffect, useState } from 'react';
import { useAuth } from '../features/auth/context';
import { SelectField } from './common';
import { preferenceKey, readPreference, writePreference } from '../utils/preferences';

export default function WorkspacePreferences({ user }) {
  const [syncStatus, setSyncStatus] = useState('Preferences sync to your account when available.');
  useEffect(() => {
    const changed = (e) => setSyncStatus(e.detail);
    window.addEventListener('preference-sync', changed);
    return () => window.removeEventListener('preference-sync', changed);
  }, []);
  const { can } = useAuth();
  const key = preferenceKey(user, 'preferences');
  const [preferences, setPreferences] = useState(() => readPreference(key));
  useEffect(() => {
    document.documentElement.dataset.density =
      preferences.density === 'compact' ? 'compact' : 'comfortable';
    return () => {
      delete document.documentElement.dataset.density;
    };
  }, [preferences.density]);
  function update(field, value) {
    const next = { ...preferences, [field]: value };
    setPreferences(next);
    writePreference(key, next);
  }
  const pages = [
    { value: '/dashboard', label: 'Dashboard' },
    ...(can('ISSUE_VIEW') ? [{ value: '/issues', label: 'Issues' }] : []),
    ...(can('PROJECT_VIEW') ? [{ value: '/projects', label: 'Projects' }] : []),
  ];
  return (
    <section aria-labelledby="workspace-preferences-title" className="settings-section">
      <h3 id="workspace-preferences-title">Workspace preferences</h3>
      <div className="form-grid">
        <SelectField
          label="Layout and table density"
          values={[
            { value: 'comfortable', label: 'Comfortable' },
            { value: 'compact', label: 'Compact' },
          ]}
          value={preferences.density === 'compact' ? 'compact' : 'comfortable'}
          onChange={(event) => update('density', event.target.value)}
        />
        <SelectField
          label="Default landing page"
          values={pages}
          value={
            pages.some((page) => page.value === preferences.landing)
              ? preferences.landing
              : '/dashboard'
          }
          onChange={(event) => update('landing', event.target.value)}
        />
      </div>
      <p className="hint">{syncStatus} Your landing page applies the next time you sign in.</p>
      <PreferenceSyncReview user={user} />
    </section>
  );
}
