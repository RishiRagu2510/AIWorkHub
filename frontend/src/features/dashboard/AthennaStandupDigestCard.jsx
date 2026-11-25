import React, { useState } from 'react';
import { Icon } from '../../components/Icon';
import './AthennaStandupDigestCard.scss';

function parseStandupDigest(rawText) {
  if (!rawText) return { intro: [], workload: [], question: '', suggestions: [], closing: '' };

  const lines = rawText.split('\n').map((l) => l.trim()).filter(Boolean);

  const intro = [];
  const workload = [];
  let question = '';
  const suggestions = [];
  let closing = '';

  let section = 'intro';

  for (let line of lines) {
    // Workload item match: e.g. * **Completed:** ... or - **In Progress:** ...
    const workloadMatch = line.match(/^[\*\-•]?\s*\*\*(.*?)\*\*:?\s*(.*)$/);
    if (workloadMatch) {
      section = 'workload';
      const label = workloadMatch[1].replace(/:$/, '').trim();
      const fullContent = workloadMatch[2].trim();

      // Check if note is in parentheses at the end
      const noteMatch = fullContent.match(/^(.*?)\s*\((.*?)\)$/);
      if (noteMatch) {
        workload.push({
          label,
          main: noteMatch[1].trim(),
          note: noteMatch[2].trim(),
        });
      } else {
        workload.push({
          label,
          main: fullContent,
          note: null,
        });
      }
      continue;
    }

    // Question line: e.g. "How can I help you manage this workload today? Would you like to:"
    if (line.toLowerCase().includes('how can i help') || line.toLowerCase().includes('would you like to:')) {
      section = 'suggestions';
      question = line;
      continue;
    }

    // Numbered suggestion: e.g. "1. Prioritize which bug to tackle first?"
    const suggestionMatch = line.match(/^\d+\.\s*(.*)$/);
    if (suggestionMatch) {
      section = 'suggestions';
      suggestions.push(suggestionMatch[1].trim());
      continue;
    }

    // Closing statement: "Just let me know what you need!"
    if (line.toLowerCase().includes('let me know') || line.toLowerCase().includes('happy to help')) {
      closing = line;
      continue;
    }

    if (section === 'intro') {
      if (!line.toLowerCase().includes('here is a quick summary')) {
        intro.push(line);
      }
    } else if (section === 'suggestions') {
      if (!closing) closing = line;
    }
  }

  return { intro, workload, question, suggestions, closing };
}

function getWorkloadBadgeMeta(label) {
  const normalized = label.toLowerCase();
  if (normalized.includes('complete') || normalized.includes('done') || normalized.includes('resolved')) {
    return {
      type: 'completed',
      icon: 'check',
      label: 'Completed',
    };
  }
  if (normalized.includes('progress') || normalized.includes('ongoing')) {
    return {
      type: 'in-progress',
      icon: 'clock',
      label: 'In Progress',
    };
  }
  if (normalized.includes('assigned') || normalized.includes('you') || normalized.includes('to do')) {
    return {
      type: 'assigned',
      icon: 'user',
      label: 'Assigned to You',
    };
  }
  return {
    type: 'default',
    icon: 'folder',
    label,
  };
}

function getSuggestionIcon(text) {
  const lower = text.toLowerCase();
  if (lower.includes('prioritize') || lower.includes('bug')) return '🎯';
  if (lower.includes('draft') || lower.includes('status') || lower.includes('update')) return '📝';
  if (lower.includes('break down') || lower.includes('troubleshoot') || lower.includes('camera')) return '🔍';
  return '⚡';
}

export default function AthennaStandupDigestCard({
  digestText,
  onClose,
  onRegenerate,
  isRegenerating,
  runCopilot,
}) {
  const [activeAction, setActiveAction] = useState(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [actionResult, setActionResult] = useState(null);
  const [copied, setCopied] = useState(false);

  const parsed = parseStandupDigest(digestText);

  const handleActionClick = async (suggestion) => {
    if (activeAction === suggestion && actionResult) {
      // Toggle close if already open
      setActiveAction(null);
      setActionResult(null);
      return;
    }

    setActiveAction(suggestion);
    setActionLoading(true);
    setActionResult(null);

    try {
      if (runCopilot) {
        const res = await runCopilot({
          feature: 'DailyStandupDigest',
          inputText: `Please execute this action: "${suggestion}". Context of my recent work:\n${digestText}`,
        }).unwrap();
        setActionResult(res?.outputText || 'Here is the detailed response for your selected action.');
      } else {
        setActionResult(`Completed action: ${suggestion}`);
      }
    } catch {
      setActionResult('Unable to execute this action at this time. Please try again.');
    } finally {
      setActionLoading(false);
    }
  };

  const handleCopy = () => {
    if (!actionResult) return;
    navigator.clipboard?.writeText(actionResult);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="athenna-standup-digest-card" role="region" aria-label="Athenna Daily Standup Digest">
      {/* Card Header */}
      <div className="standup-digest-header">
        <div className="standup-digest-brand">
          <div className="standup-brand-avatar" aria-hidden="true">
            ✦
          </div>
          <div>
            <div className="standup-title-row">
              <h3 className="standup-digest-title">Athenna Daily Standup Digest</h3>
              <span className="standup-digest-pill">Executive Brief</span>
            </div>
            <p className="standup-digest-subtitle">Past 24 hours workspace status summary</p>
          </div>
        </div>

        <div className="standup-header-actions">
          {onRegenerate && (
            <button
              type="button"
              className="standup-action-btn"
              onClick={onRegenerate}
              disabled={isRegenerating}
              title="Regenerate digest"
              aria-label="Regenerate standup digest"
            >
              <Icon name="refresh" />
              <span>{isRegenerating ? 'Refreshing…' : 'Refresh'}</span>
            </button>
          )}
          <button
            type="button"
            className="standup-close-btn"
            onClick={onClose}
            title="Dismiss digest"
            aria-label="Dismiss standup digest"
          >
            ✕
          </button>
        </div>
      </div>

      {/* Card Body */}
      <div className="standup-digest-body">
        {/* Intro Greeting */}
        {parsed.intro.length > 0 && (
          <div className="standup-intro-block">
            {parsed.intro.map((p, idx) => (
              <p key={idx} className={idx === 0 ? 'intro-headline' : 'intro-subtext'}>
                {p}
              </p>
            ))}
          </div>
        )}

        {/* Workload Status Cards */}
        {parsed.workload.length > 0 ? (
          <div className="standup-workload-section">
            <h4 className="workload-section-heading">
              <span className="heading-icon">📋</span> Workload Breakdown
            </h4>
            <div className="workload-cards-grid">
              {parsed.workload.map((item, idx) => {
                const meta = getWorkloadBadgeMeta(item.label);
                return (
                  <div key={idx} className={`workload-item-card status-${meta.type}`}>
                    <div className="workload-card-badge">
                      <span className="badge-dot" aria-hidden="true"></span>
                      <span className="badge-text">{meta.label}</span>
                    </div>
                    <div className="workload-card-content">
                      <div className="workload-main-text">{item.main}</div>
                      {item.note && <div className="workload-note-pill">{item.note}</div>}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        ) : (
          <div className="standup-raw-fallback">
            <p>{digestText}</p>
          </div>
        )}

        {/* Interactive Suggestions Section */}
        {parsed.suggestions.length > 0 && (
          <div className="standup-suggestions-section">
            <div className="suggestions-prompt-title">
              <span className="prompt-sparkle">✦</span>
              <span>{parsed.question || 'Recommended Next Steps:'}</span>
            </div>
            <div className="suggestions-actions-row">
              {parsed.suggestions.map((sug, idx) => {
                const icon = getSuggestionIcon(sug);
                const isSelected = activeAction === sug;
                return (
                  <button
                    key={idx}
                    type="button"
                    className={`standup-suggestion-chip ${isSelected ? 'is-active' : ''}`}
                    onClick={() => handleActionClick(sug)}
                    disabled={actionLoading}
                  >
                    <span className="chip-icon">{icon}</span>
                    <span className="chip-label">{sug}</span>
                    <span className="chip-arrow">→</span>
                  </button>
                );
              })}
            </div>

            {/* Interactive Action Response Drawer */}
            {(actionLoading || actionResult) && (
              <div className="standup-action-response-box">
                <div className="response-header">
                  <div className="response-title">
                    <span className="response-sparkle">✦</span>
                    <strong>{activeAction}</strong>
                  </div>
                  {actionResult && (
                    <button type="button" className="copy-action-btn" onClick={handleCopy}>
                      {copied ? '✓ Copied' : 'Copy'}
                    </button>
                  )}
                </div>
                {actionLoading ? (
                  <div className="response-loading-state">
                    <span className="typing-dots">
                      <span></span>
                      <span></span>
                      <span></span>
                    </span>
                    <span>Athenna is processing your request…</span>
                  </div>
                ) : (
                  <div className="response-content-text">{actionResult}</div>
                )}
              </div>
            )}
          </div>
        )}

        {/* Friendly Closing */}
        {parsed.closing && <p className="standup-closing-note">{parsed.closing}</p>}
      </div>
    </div>
  );
}
