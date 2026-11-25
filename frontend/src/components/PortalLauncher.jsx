import { useEffect, useRef, useState } from 'react';
import { useAuth } from '../features/auth/context';
import PortalLogo from './PortalLogo';
import { getActivePortal, portalKind, portalUrl } from '../utils/portals';
import '../styles/portal-launcher.scss';

export default function PortalLauncher({ forceEnable = false }) {
  const currentPortal = getActivePortal();
  if (!forceEnable && currentPortal !== 'hub') return null;

  const { can, canAccessPortal } = useAuth();
  const [open, setOpen] = useState(false);
  const root = useRef(null);
  const trigger = useRef(null);

  useEffect(() => {
    if (!open) return;
    const dismiss = (event) => {
      if (event.type === 'keydown' && event.key === 'Escape') {
        setOpen(false);
        trigger.current?.focus();
      } else if (event.type === 'pointerdown' && !root.current?.contains(event.target)) {
        setOpen(false);
      }
    };
    document.addEventListener('pointerdown', dismiss);
    document.addEventListener('keydown', dismiss);
    return () => {
      document.removeEventListener('pointerdown', dismiss);
      document.removeEventListener('keydown', dismiss);
    };
  }, [open]);
  const portals = [
    ['PROJECT_VIEW', '/dashboard', 'GridHub', 'folder', 'Your project workspace'],
    ['EMPLOYEE_VIEW', '/profile', 'Persona', 'user', 'Your profile and career'],
    ['TIMESHEET_VIEW', '/timesheets/my', 'Chronos', 'clock', 'Track your work'],
  ].filter(
    ([permission, path]) => {
      const kind = portalKind(path);
      if (canAccessPortal && !canAccessPortal(kind)) return false;
      return (permission === 'ALL' || !permission || can(permission)) && kind !== currentPortal;
    },
  );
  return (
    <div
      className="portal-launcher"
      ref={root}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) setOpen(false);
      }}
    >
      <button
        ref={trigger}
        type="button"
        className="icon-button portal-launcher-trigger"
        aria-label="Open portals"
        aria-expanded={open}
        aria-controls="portal-launcher-panel"
        onClick={() => setOpen((value) => !value)}
      >
        <svg viewBox="0 0 24 24" aria-hidden="true" width="24" height="24">
          {[3, 10, 17].flatMap((x) =>
            [3, 10, 17].map((y) => (
              <rect
                key={`${x}-${y}`}
                x={x}
                y={y}
                width="4"
                height="4"
                rx="0.5"
                fill="currentColor"
              />
            )),
          )}
        </svg>
      </button>
      {open && (
        <nav id="portal-launcher-panel" className="portal-launcher-panel" aria-label="Portals">
          <div className="portal-tiles">
            {portals.map(([, path, title, , description]) => (
              <a
                key={path}
                href={portalUrl(path)}
                target="_blank"
                rel="noopener noreferrer"
                onClick={() => setOpen(false)}
              >
                <span className="portal-tile-icon">
                  <PortalLogo portal={portalKind(path)} />
                </span>
                <strong>{title}</strong>
                <small>{description}</small>
              </a>
            ))}
          </div>
        </nav>
      )}
    </div>
  );
}
