import PortalLink from './PortalLink';
import { useAuth } from '../features/auth/context';
import { ATHENNA } from '../features/auth/features';
import { featurePortal } from '../features/auth/session';
import { featureCatalog, portalCatalog } from '../features/auth/planCatalog';
import RequestAccess from './RequestAccess';
import '../styles/plan-restricted.scss';

const portals = portalCatalog;
const features = featureCatalog;

/**
 * Shown instead of a page or tool that the company's plan does not include. It names what is missing, says that
 * the company (not the user's role) decides it, and offers the portals the plan does include.
 * Pass `portal` for a locked portal or `feature` for a locked optional feature such as the AI chat.
 */
export default function PlanRestricted({ portal: lockedPortal, feature: lockedFeature }) {
  const { canAccessPortal, canUseFeature } = useAuth() || {};
  // A feature that comes with a portal (the AI chat comes with AIWorkHub) is missing because the portal is: say so and ask for the portal.
  const comesWith = lockedFeature ? featurePortal(lockedFeature) : null;
  const missingPortal = comesWith && canAccessPortal && !canAccessPortal(comesWith) ? comesWith : null;
  const portal = missingPortal || lockedPortal;
  const feature = missingPortal ? undefined : lockedFeature;
  const locked = feature
    ? features[feature] || { name: feature, what: '' }
    : portals.find((p) => p.id === portal) || { name: 'This area', what: '' };
  const yours = portals.filter((p) => p.id !== portal && (canAccessPortal ? canAccessPortal(p.id) : true));
  const hasAi = feature !== ATHENNA && (canUseFeature ? canUseFeature(ATHENNA) : true);
  return (
    <section className="plan-restricted" aria-labelledby="plan-restricted-title">
      <div className="plan-restricted-badge" aria-hidden="true">
        <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
          <rect x="4" y="11" width="16" height="10" rx="2" />
          <path d="M8 11V8a4 4 0 0 1 8 0v3" />
        </svg>
      </div>
      <h2 id="plan-restricted-title">{locked.name} isn&apos;t included in your plan</h2>
      <p>
        {locked.what ? `${locked.what}. ` : ''}Your company hasn&apos;t turned {locked.name} on, so this is not
        something your role can change. Ask your admin to enable it.
      </p>
      <RequestAccess portal={feature ? undefined : portal} feature={feature} />
      {yours.length > 0 && (
        <div className="plan-restricted-actions">
          {yours.slice(0, 3).map((p, index) => (
            <PortalLink key={p.id} to={p.to} className="button">
              {index === 0 ? `Go to ${p.name}` : `Open ${p.name}`}
            </PortalLink>
          ))}
        </div>
      )}
      {(yours.length > 0 || hasAi) && (
        <div className="plan-restricted-plan">
          <span id="plan-restricted-plan-label">Included in your plan</span>
          <ul aria-labelledby="plan-restricted-plan-label">
            {yours.map((p) => (
              <li key={p.id}>{p.name}</li>
            ))}
            {hasAi && <li>{features[ATHENNA].name}</li>}
          </ul>
        </div>
      )}
    </section>
  );
}
