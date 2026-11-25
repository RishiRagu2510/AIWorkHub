import React from 'react';

const MAX_SHOWN = 4;
const LABEL = { high: 'Urgent', medium: 'Soon', low: 'FYI' };

/**
 * "Needs your attention": what Athenna found on the person's own data (overdue work, an unsubmitted timesheet, requests
 * waiting for them). Each line is a button that asks Athenna about it. Nothing is drawn while there is nothing to say,
 * while the answer is loading, or if it could not be fetched, so the empty chat never shows an error for this.
 */
export default function AthennaNudges({ nudges, onAsk }) {
  const items = Array.isArray(nudges) ? nudges : [];
  if (items.length === 0) return null;
  const shown = items.slice(0, MAX_SHOWN);
  const more = items.length - shown.length;

  return (
    <section className="athenna-nudges" aria-label="Needs your attention">
      <h2 className="athenna-nudges-title">Needs your attention</h2>
      <ul className="athenna-nudges-list">
        {shown.map((nudge) => (
          <li key={nudge.id}>
            <button type="button" className={`athenna-nudge ${nudge.severity}`} onClick={() => onAsk?.(nudge.prompt)}>
              <span className="athenna-nudge-level">{LABEL[nudge.severity] || 'FYI'}</span>
              <span className="athenna-nudge-text">
                <span className="athenna-nudge-headline">{nudge.title}</span>
                {nudge.detail && <span className="athenna-nudge-detail">{nudge.detail}</span>}
              </span>
              <span className="athenna-nudge-arrow" aria-hidden="true">→</span>
            </button>
          </li>
        ))}
      </ul>
      {more > 0 && <p className="athenna-nudges-more">and {more} more. Ask Athenna what needs your attention.</p>}
    </section>
  );
}
