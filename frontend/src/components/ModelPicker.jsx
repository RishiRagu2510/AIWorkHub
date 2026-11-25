import React, { useState } from 'react';
import { formatReset } from './athennaModelUsage';

const NOTICES = {
  OpenAI: "Answered by OpenAI with the server's key. If OpenAI cannot answer, Gemini replies instead and the chat says so.",
  Groq: "Answered by Groq with the server's key. If Groq cannot answer, Gemini replies instead and the chat says so.",
};

const plural = (n) => `${n} message${n === 1 ? '' : 's'}`;

function UsageLine({ label, window: w }) {
  const tone = w.left <= 10 ? 'low' : w.left <= 30 ? 'warn' : 'ok';
  return (
    <div className="model-usage-line">
      <span>{label}</span>
      <span className="model-usage-remaining">
        <strong title={`${plural(w.used)} of ${w.limit}`}>{w.left}% left</strong>
        <span className={`model-usage-ring ${tone}`} style={{ '--left': `${w.left}%` }} aria-hidden="true" />
      </span>
      {w.resetsInMs != null && <span className="model-usage-reset">Resets in {formatReset(w.resetsInMs)}</span>}
    </div>
  );
}

/**
 * Compact model list: name, a short tag, a check on the active one, a Notice badge where there is a caveat,
 * greyed-out rows for a provider this server cannot call, and a "View Usage" panel with the user's own counts.
 * `availability` maps provider name to { available, reason }; a provider that is not listed is treated as available.
 */
export default function ModelPicker({ models, selectedId, availability = {}, usage = {}, onSelect }) {
  const [showUsage, setShowUsage] = useState(false);
  const providers = [...new Set(models.map((m) => m.provider))];

  return (
    <div className="intelligence-model-popover model-menu" role="menu" aria-label="Model">
      <div className="model-menu-title">Model</div>
      <div className="model-menu-list">
        {models.map((model) => {
          const status = availability[model.provider];
          const unavailable = status?.available === false;
          const selected = model.id === selectedId;
          return (
            <button
              key={model.id}
              type="button"
              role="menuitemradio"
              aria-checked={selected}
              disabled={unavailable}
              className={`model-menu-row ${selected ? 'selected' : ''} ${unavailable ? 'unavailable' : ''}`}
              title={unavailable ? status.reason : model.desc}
              onClick={() => onSelect(model.id)}
            >
              <span className="model-menu-name">{model.name}</span>
              <span className="model-menu-tag">{model.badge}</span>
              {unavailable ? (
                <span className="model-menu-pill" title={status.reason}>Unavailable ⓘ</span>
              ) : (
                NOTICES[model.provider] && <span className="model-menu-pill" title={NOTICES[model.provider]}>Notice ⓘ</span>
              )}
              {selected && <span className="model-menu-check" aria-hidden="true">✓</span>}
            </button>
          );
        })}
      </div>

      <div className="model-menu-divider" />
      <button
        type="button"
        className={`model-menu-row usage-toggle ${showUsage ? 'selected' : ''}`}
        aria-expanded={showUsage}
        onClick={() => setShowUsage((open) => !open)}
      >
        <span className="model-menu-name">View Usage</span>
        <span className="model-menu-chevron" aria-hidden="true">›</span>
      </button>

      {showUsage && (
        <div className="model-usage-panel" role="region" aria-label="Usage">
          <div className="model-menu-title">Usage</div>
          {providers.map((provider) => {
            const row = usage[provider];
            if (!row) return null;
            return (
              <div key={provider} className="model-usage-group">
                <div className="model-usage-heading">{provider} models</div>
                <UsageLine label="5h limit" window={row.fiveHour} />
                <UsageLine label="Weekly limit" window={row.week} />
              </div>
            );
          })}
          <p className="model-usage-note">Measured against this workspace's allowance for your messages on this device. The providers' own limits can differ.</p>
        </div>
      )}
    </div>
  );
}
