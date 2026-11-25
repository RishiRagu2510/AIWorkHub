import { Link } from 'react-router';
import { planItems } from '../features/auth/planCatalog';
import { usePlanQuery } from '../services/api/plan';

function Item({ item }) {
  return (
    <li className={`plan-summary-item ${item.included ? 'is-included' : 'is-not-included'}`}>
      <span className="plan-summary-text">
        <strong>{item.name}</strong>
        <span className="module-detail-text">{item.what}</span>
      </span>

      <div className="plan-item-right">
        <span className={`plan-summary-status ${item.included ? 'is-on' : 'is-off'}`}>
          {item.included ? 'Included' : 'Not included'}
        </span>
      </div>
    </li>
  );
}

/**
 * Clean, production-accurate plan summary for AIWorkHub portals and tools.
 */
export default function PlanSummary() {
  const plan = usePlanQuery();
  const data = plan.data;

  let body;
  if (plan.isLoading || plan.isUninitialized) {
    body = <p className="plan-summary-note" role="status">Loading your plan…</p>;
  } else if (!data) {
    body = <p className="plan-summary-note">Your plan details could not be loaded right now.</p>;
  } else {
    const { portals, tools } = planItems(data);
    const includedPortals = portals.filter((p) => p.included).length;
    const includedTools = tools.filter((t) => t.included).length;
    const open = data.requests.length;

    body = (
      <>
        <p className="plan-summary-count">
          {includedPortals} of {portals.length} portals{tools.length > 0 && ` · ${includedTools} of ${tools.length} tool${tools.length === 1 ? '' : 's'}`} included
          {open > 0 && ` · ${open} request${open === 1 ? '' : 's'} under review`}
        </p>

        <h3>Portals</h3>
        <ul className="plan-summary-list">
          {portals.map((p) => <Item key={p.id} item={p} />)}
        </ul>

        {tools.length > 0 && (
          <>
            <h3>Tools</h3>
            <ul className="plan-summary-list">
              {tools.map((t) => <Item key={t.id} item={t} />)}
            </ul>
          </>
        )}

        <p className="plan-summary-note">Changes to your plan reach your portals the next time you sign in.</p>
      </>
    );
  }

  return (
    <section className="plan-summary" aria-label="Your plan" aria-labelledby="plan-summary-title">
      <div className="plan-summary-head">
        <div className="plan-title-group">
          <div className="title-row">
            <h2 id="plan-summary-title">Your plan</h2>
            <span className="tier-tag">AIWorkHub Plan</span>
          </div>
          <p className="plan-subtitle-meta">
            Overview of your organization&apos;s subscribed portals, AI tools, and feature access.
          </p>
        </div>
        <Link className="plan-summary-manage button-manage" to="/plan">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <circle cx="12" cy="12" r="3" />
            <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" />
          </svg>
          <span>Manage plan</span>
        </Link>
      </div>
      {body}
    </section>
  );
}
