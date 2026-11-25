import WorkspacePreferences from './WorkspacePreferences';
import { useEffect, useRef } from 'react';
import { Icon } from './Icon';

export default function ProfileSettings({ user, open, close }) {
  const ref = useRef(null);
  useEffect(() => {
    const dialog = ref.current;
    if (!open || user?.role !== 'ADMIN') return;
    const previous = document.activeElement;
    dialog.showModal();
    return () => {
      dialog.close();
      previous?.focus();
    };
  }, [open, user?.role]);

  if (user?.role !== 'ADMIN') return null;
  return (
    <dialog
      className="profile-settings"
      ref={ref}
      aria-labelledby="profile-settings-title"
      onCancel={(event) => {
        event.preventDefault();
        close();
      }}
    >
      <div className="section-header">
        <div>
          <h2 id="profile-settings-title">Settings</h2>
          <p>Your account and workspace preferences.</p>
        </div>
        <button type="button" aria-label="Close settings" onClick={close}>
          <Icon name="close" />
        </button>
      </div>
      <div className="profile-settings-body">
        <section aria-labelledby="settings-account-title" className="settings-section">
          <h3 id="settings-account-title">Account</h3>
          <dl className="settings-account-details">
            <div>
              <dt>Name</dt>
              <dd>{user.name || '—'}</dd>
            </div>
            <div>
              <dt>Email</dt>
              <dd>{user.email || '—'}</dd>
            </div>
            <div>
              <dt>Company</dt>
              <dd>{user.tenantName || 'Workspace'}</dd>
            </div>
            <div>
              <dt>Role</dt>
              <dd>
                <span className="settings-role-badge">{user.role || '—'}</span>
              </dd>
            </div>
          </dl>
          <p className="hint">Your account details and access are managed by your organization.</p>
        </section>
        <WorkspacePreferences key={`preferences:${user.tenantId}:${user.id}`} user={user} />
      </div>
      <div className="form-footer">
        <button type="button" className="primary" onClick={close}>
          Done
        </button>
      </div>
    </dialog>
  );
}
