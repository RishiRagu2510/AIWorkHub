import LandingShell from '../components/LandingShell';
import PlanSummary from '../components/PlanSummary';
import PortalLogo from '../components/PortalLogo';
import { useAuth } from '../features/auth/context';
import { portalCatalog } from '../features/auth/planCatalog';
import { portalUrl } from '../utils/portals';
import '../styles/launch-pad.scss';

// What a person needs, besides the company's plan, to be useful in each portal. AIWorkHub needs nothing more than the plan.
const needs = { workspace: 'PROJECT_VIEW', employee: 'EMPLOYEE_VIEW', timesheet: 'TIMESHEET_VIEW', hub: null };

// Portal color scheme, badges, telemetry tags, and footer info matching our actual workspace portals
const portalMeta = {
  workspace: {
    version: 'v4.2',
    tag: 'Projects & Sprints',
    badgeClass: 'tag-gridhub',
    accentClass: 'accent-gridhub',
    telemetry: 'Active Sprints, Issues & Kanban Boards',
    telemetryDot: 'telemetry-dot-purple',
    submeta: 'GridHub Workspace',
    actionText: 'Open GridHub',
  },
  employee: {
    version: 'Directory',
    tag: 'HR & Directory',
    badgeClass: 'tag-persona',
    accentClass: 'accent-persona',
    telemetry: 'Org Charts, Profiles & Leave Requests',
    telemetryDot: 'telemetry-dot-green',
    submeta: 'Persona Portal',
    actionText: 'Open Persona',
  },
  timesheet: {
    version: 'Timecards',
    tag: 'Time & Attendance',
    badgeClass: 'tag-chronos',
    accentClass: 'accent-chronos',
    telemetry: 'Timesheet Approvals & Project Work Logs',
    telemetryDot: 'telemetry-dot-amber',
    submeta: 'Chronos Portal',
    actionText: 'Open Chronos',
  },
  hub: {
    version: 'AI Copilot',
    tag: 'AI Assistant & Central Hub',
    badgeClass: 'tag-hub',
    accentClass: 'accent-hub',
    telemetry: 'Athenna AI Chat, Copilot & Cross-Portal Hub',
    telemetryDot: 'telemetry-dot-blue',
    submeta: 'AIWorkHub Central',
    actionText: 'Launch AIWorkHub',
  },
};

/**
 * The portals to offer: the ones the company's plan includes, and that this person can actually use. A portal the company did not buy
 * is not shown at all, and neither is one the person has no permission in, so nobody is sent to an "access restricted" page.
 */
export function availablePortals({ canAccessPortal, can }) {
  return portalCatalog.filter((p) => canAccessPortal(p.id) && (!needs[p.id] || can(needs[p.id])));
}

export default function LaunchPad() {
  const { session, can, canAccessPortal } = useAuth();
  const portals = availablePortals({ canAccessPortal, can });
  const name = session.user.name || session.user.email;

  return (
    <LandingShell>
      <div className="launch-pad-hero-section">
        <h1 className="hero-greeting">Welcome, {name}</h1>
        <p className="hero-description">
          Choose a workspace portal to manage sprints, view the employee directory, record billable timecards, or collaborate with Athenna AI.
        </p>
      </div>

      {portals.length === 0 ? (
        <section className="launch-pad-empty" aria-label="No portals available">
          <h2>Nothing to open yet</h2>
          <p>
            Your account does not have access to any portal. Ask your company administrator to give you a role, or to check your company&apos;s plan.
          </p>
        </section>
      ) : (
        <nav aria-label="Portals">
          <ul className="launch-pad-grid">
            {portals.map((p) => {
              const meta = portalMeta[p.id] || {
                tag: 'Workspace',
                badgeClass: '',
                accentClass: '',
                actionText: `Open ${p.name}`,
                submeta: '',
                telemetry: '',
              };
              return (
                <li key={p.id}>
                  <a className={`launch-pad-card ${meta.accentClass}`} href={portalUrl(p.to)}>
                    <div className="launch-pad-card-top">
                      <div className="portal-logo-wrapper">
                        <PortalLogo portal={p.id} />
                      </div>
                      <span className={`portal-tag ${meta.badgeClass}`}>{meta.tag}</span>
                    </div>

                    <div className="launch-pad-card-body">
                      <div className="card-title-row">
                        <strong>{p.name}</strong>
                      </div>
                      <span className="card-desc">{p.what}</span>
                    </div>

                    <div className="launch-pad-card-footer">
                      <div className="action-link-group">
                        <span className="launch-link-label">{meta.actionText || `Open ${p.name}`}</span>
                        <span className="launch-arrow" aria-hidden="true">→</span>
                      </div>
                    </div>
                  </a>
                </li>
              );
            })}
          </ul>
        </nav>
      )}

      <PlanSummary />
    </LandingShell>
  );
}
