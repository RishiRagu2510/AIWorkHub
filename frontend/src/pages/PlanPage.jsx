import { ErrorState, PageHeader, QueryState } from '../components/common';
import CancelPlan from '../components/CancelPlan';
import RequestAccess from '../components/RequestAccess';
import { featureCatalog, portalCatalog } from '../features/auth/planCatalog';
import { useCancelAccessRequestMutation, usePlanQuery } from '../services/api/plan';
import { dateTime } from '../utils/format';
import '../styles/plan-page.scss';

// Visual icon indicators for known portals and tools
function ItemIcon({ id }) {
  if (id === 'workspace') {
    return (
      <div className="plan-item-icon plan-icon-gridhub" aria-hidden="true">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <rect x="3" y="3" width="7" height="7" rx="1.5" />
          <rect x="14" y="3" width="7" height="7" rx="1.5" />
          <rect x="3" y="14" width="7" height="7" rx="1.5" />
          <rect x="14" y="14" width="7" height="7" rx="1.5" />
        </svg>
      </div>
    );
  }
  if (id === 'employee') {
    return (
      <div className="plan-item-icon plan-icon-persona" aria-hidden="true">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" />
          <circle cx="12" cy="7" r="4" />
        </svg>
      </div>
    );
  }
  if (id === 'timesheet') {
    return (
      <div className="plan-item-icon plan-icon-chronos" aria-hidden="true">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <circle cx="12" cy="12" r="10" />
          <polyline points="12 6 12 12 16 14" />
        </svg>
      </div>
    );
  }
  if (id === 'hub') {
    return (
      <div className="plan-item-icon plan-icon-hub" aria-hidden="true">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
          <polyline points="9 22 9 12 15 12 15 22" />
        </svg>
      </div>
    );
  }
  return (
    <div className="plan-item-icon plan-icon-athenna" aria-hidden="true">
      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="m12 3 1.9 4.8L18.7 9.7l-4.8 1.9L12 16.4l-1.9-4.8L5.3 9.7l4.8-1.9L12 3z" />
        <path d="M5 3v4M3 5h4M19 17v4M17 19h4" />
      </svg>
    </div>
  );
}

/** Takes a request back: it leaves the list, and the item can be requested again. The server checks who may do this. */
function CancelRequest({ id, name, withdraw }) {
  const [cancel, { isLoading, error }] = useCancelAccessRequestMutation();
  return (
    <>
      <button type="button" className="button plan-request-cancel" disabled={isLoading} aria-label={`${withdraw ? 'Withdraw' : 'Cancel'} request for ${name}`} onClick={() => cancel(id)}>
        {isLoading ? (withdraw ? 'Withdrawing…' : 'Cancelling…') : withdraw ? 'Withdraw request' : 'Cancel request'}
      </button>
      <ErrorState error={error} />
    </>
  );
}

function Row({ item, included, portal, feature, isAdmin, pendingCancel, canCancel, alsoRemoves }) {
  const itemId = portal || feature;
  return (
    <li className={`plan-item ${included ? 'is-included' : 'is-not-included'}`}>
      <div className="plan-item-main">
        <ItemIcon id={itemId} type={portal ? 'portal' : 'tool'} />
        <div className="plan-item-details">
          <strong>{item.name}</strong>
          <span>{item.what}</span>
        </div>
      </div>
      {included ? (
        <div className="plan-item-request">
          <span className="plan-status plan-status-on">
            <svg className="plan-status-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <polyline points="20 6 9 17 4 12" />
            </svg>
            Included
          </span>
          {/* Only an admin can ask to end part of the plan, and a plan keeps at least one portal. */}
          {isAdmin && (pendingCancel || canCancel) && (
            <CancelPlan portal={portal} feature={feature} name={item.name} pending={pendingCancel} alsoRemoves={alsoRemoves} />
          )}
        </div>
      ) : (
        <div className="plan-item-request">
          <span className="plan-status plan-status-off">
            <svg className="plan-status-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <circle cx="12" cy="12" r="10" />
              <line x1="4.93" y1="4.93" x2="19.07" y2="19.07" />
            </svg>
            Not included
          </span>
          <RequestAccess portal={portal} feature={feature} link={false} />
        </div>
      )}
    </li>
  );
}

const nameOf = (request) => (request.kind === 'portal' ? portalCatalog.find((p) => p.id === request.name) : featureCatalog[request.name])?.name || request.name;
const isCancellation = (request) => request.action === 'remove';

/**
 * What the company's plan includes, with a way to ask for the rest. Everyone sees their own requests; an admin sees
 * everyone's. The plan itself is set by whoever operates the platform, not on this page.
 */
export default function PlanPage() {
  const plan = usePlanQuery();
  const data = plan.data;

  const isPortalIncluded = (id) => data?.portals.find((x) => x.id === id)?.included ?? true;
  // A feature that comes with a portal (the AI chat comes with AIWorkHub) is not offered on its own: without the portal it is not listed,
  // and asking for the portal is the way to get it.
  const visibleFeatures = data
    ? Object.entries(featureCatalog).filter(([id]) => {
        const requires = data.features.find((x) => x.id === id)?.requires;
        return !requires || isPortalIncluded(requires);
      })
    : [];

  const pendingCancel = (kind, id) => data?.requests.some((r) => r.action === 'remove' && r.kind === kind && r.name === id) ?? false;

  const totalPortals = portalCatalog.length;
  const totalFeatures = visibleFeatures.length;
  const totalItems = totalPortals + totalFeatures;

  const includedPortals = data ? portalCatalog.filter((p) => isPortalIncluded(p.id)).length : 0;
  const includedFeatures = data ? visibleFeatures.filter(([id]) => (data.features.find((x) => x.id === id)?.included ?? true)).length : 0;
  const totalIncluded = includedPortals + includedFeatures;

  return (
    <div className="plan-page">
      <PageHeader eyebrow="Company" title="Your plan" description="The portals and tools your company has turned on." />
      <QueryState query={plan}>
        {data && (
          <>
            <div className="plan-hero-banner">
              <div className="plan-hero-content">
                <div className="plan-hero-badge">Enterprise Workspace Plan</div>
                <h3>Active Features & Services</h3>
                <p>Your subscription includes access to core workspace portals and productivity tools. Request extensions to expand your team&apos;s capabilities.</p>
              </div>
              <div className="plan-hero-metrics">
                <div className="plan-metric-pill">
                  <span className="metric-number">{totalIncluded} <small>/ {totalItems}</small></span>
                  <span className="metric-label">Enabled Capabilities</span>
                </div>
                {data.requests.length > 0 && (
                  <div className="plan-metric-pill pending-metric">
                    <span className="metric-number">{data.requests.length}</span>
                    <span className="metric-label">Pending Requests</span>
                  </div>
                )}
              </div>
            </div>

            <section aria-labelledby="plan-portals" className="plan-section">
              <div className="plan-section-header">
                <div>
                  <h2 id="plan-portals">Portals</h2>
                  <p className="plan-section-desc">Standalone workspace modules for projects, people, and time tracking.</p>
                </div>
                <span className="plan-section-count">{includedPortals} of {totalPortals} included</span>
              </div>
              <ul className="plan-list">
                {portalCatalog.map((p) => (
                  <Row
                    key={p.id}
                    item={p}
                    portal={p.id}
                    included={data.portals.find((x) => x.id === p.id)?.included ?? true}
                    isAdmin={data.isAdmin}
                    pendingCancel={pendingCancel('portal', p.id)}
                    canCancel={includedPortals > 1}
                    alsoRemoves={p.id === 'hub' ? 'The Athenna AI chat goes with it.' : undefined}
                  />
                ))}
              </ul>
            </section>

            {visibleFeatures.length > 0 && (
            <section aria-labelledby="plan-tools" className="plan-section">
              <div className="plan-section-header">
                <div>
                  <h2 id="plan-tools">Tools</h2>
                  <p className="plan-section-desc">AI assistants and automated tools powered across your workspace.</p>
                </div>
                <span className="plan-section-count">{includedFeatures} of {totalFeatures} included</span>
              </div>
              <ul className="plan-list">
                {visibleFeatures.map(([id, f]) => (
                  <Row
                    key={id}
                    item={f}
                    feature={id}
                    included={data.features.find((x) => x.id === id)?.included ?? true}
                    isAdmin={data.isAdmin}
                    pendingCancel={pendingCancel('feature', id)}
                    canCancel
                  />
                ))}
              </ul>
            </section>
            )}

            <section aria-labelledby="plan-requests" className="plan-section">
              <div className="plan-section-header">
                <div>
                  <h2 id="plan-requests">{data.isAdmin ? 'Requests from your company' : 'Your requests'}</h2>
                  <p className="plan-section-desc">Track access requests submitted to expand your organization&apos;s plan.</p>
                </div>
                {data.requests.length > 0 && (
                  <span className="plan-section-count">{data.requests.length} request{data.requests.length === 1 ? '' : 's'}</span>
                )}
              </div>
              {data.requests.length === 0 ? (
                <div className="plan-empty-card">
                  <div className="plan-empty-icon" aria-hidden="true">
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <circle cx="12" cy="12" r="10" />
                      <line x1="12" y1="8" x2="12" y2="12" />
                      <line x1="12" y1="16" x2="12.01" y2="16" />
                    </svg>
                  </div>
                  <p className="plan-empty">No open requests.</p>
                  <span>Request access to any excluded portal or tool above to request an expansion.</span>
                </div>
              ) : (
                <ul className="plan-list plan-requests-list">
                  {data.requests.map((r) => (
                    <li key={r.id} className="plan-item plan-request-item">
                      <div className="plan-item-main">
                        <div className="plan-request-status-dot" aria-hidden="true" />
                        <div className="plan-item-details">
                          <strong>{isCancellation(r) ? `Cancel ${nameOf(r)}` : nameOf(r)}</strong>
                          <span className="plan-request-meta">
                            {data.isAdmin && !r.mine && <strong className="plan-request-user">{r.userName} · </strong>}
                            <time>{dateTime(r.createdAt)}</time>
                          </span>
                          {r.note && <span className="plan-note">{r.note}</span>}
                        </div>
                      </div>
                      <div className="plan-request-actions">
                        <div className="plan-request-badge">
                          <span className="plan-badge-pulse" aria-hidden="true" />
                          {isCancellation(r) ? 'Cancellation under review' : 'Under review'}
                        </div>
                        {(r.mine || data.isAdmin) && <CancelRequest id={r.id} name={nameOf(r)} withdraw={isCancellation(r)} />}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
              {data.isAdmin && data.requests.length > 0 && (
                <div className="plan-admin-guidance">
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <circle cx="12" cy="12" r="10" />
                    <line x1="12" y1="16" x2="12" y2="12" />
                    <line x1="12" y1="8" x2="12.01" y2="8" />
                  </svg>
                  <p className="plan-empty">Your AIWorkHub representative reviews these. The plan on this page changes once they approve.</p>
                </div>
              )}
            </section>
          </>
        )}
      </QueryState>
    </div>
  );
}
