import React, { useState } from 'react';
import { dayLabel } from './athennaChatHistory';

/**
 * The "Chat History" card above Issue Completion. It asks first: nothing is saved until the user accepts.
 * `consent` is 'yes', 'no', or null when they have not been asked.
 */
export default function ChatHistoryCard({
  consent,
  chats,
  activeId,
  disabled,
  loading = false,
  error = null,
  hasCurrentChat = false,
  onConsent,
  onOpen,
  onDelete,
  onClear,
  onNew,
  onSaveAndNew,
}) {
  // With saving off, a conversation that is about to be left behind is offered a save first.
  const [asking, setAsking] = useState(false);
  const [busy, setBusy] = useState(false);
  const startNew = () => {
    if (consent === 'no' && hasCurrentChat && onSaveAndNew) setAsking(true);
    else onNew();
  };
  const leaveWithoutSaving = () => {
    setAsking(false);
    onNew();
  };
  const saveThenLeave = async () => {
    setBusy(true);
    try {
      await onSaveAndNew();
    } finally {
      setBusy(false);
      setAsking(false);
    }
  };

  return (
    <div className="agentic-card chat-history-card">
      <div className="agentic-card-header">
        <div className="agent-identity">
          <div className="agent-icon-badge">
            <span>🕘</span>
          </div>
          <div className="agent-info">
            <h4 className="agent-name">Chat History</h4>
            <span className="agent-dept">Saved to your account • Athenna</span>
          </div>
        </div>
        {!loading && consent !== null && (
          <div className="agent-status-badge analyzing">
            <span className="status-dot-purple"></span>
            <span>{consent === 'yes' ? `${chats.length} saved` : `Off • ${chats.length} kept`}</span>
          </div>
        )}
      </div>

      <div className="agentic-card-body chat-history-body">
        {loading && <p className="chat-history-empty">Loading your chats…</p>}

        {error && (
          <div className="chat-history-error" role="alert">
            {error}
          </div>
        )}

        {!loading && consent === null && (
          <div className="chat-history-consent" role="group" aria-label="Save chat history">
            <p>
              Save your conversations with Athenna so you can reopen them later? They are stored on the server under your
              account, only you can see them, and you can delete them at any time.
            </p>
            <div className="chat-history-actions">
              <button type="button" className="chat-history-btn primary" onClick={() => onConsent('yes')}>
                Yes, save my chats
              </button>
              <button type="button" className="chat-history-btn" onClick={() => onConsent('no')}>
                No thanks
              </button>
            </div>
          </div>
        )}

        {!loading && consent !== null && (
          <>
            {consent === 'no' && (
              <div className="chat-history-consent chat-history-consent-banner">
                <p>Saving is off, so new conversations are not saved. {chats.length > 0 ? 'Your earlier chats are kept below.' : ''}</p>
                <div className="chat-history-actions">
                  <button type="button" className="chat-history-btn primary chat-history-turn-on" onClick={() => onConsent('yes')}>
                    Turn on
                  </button>
                </div>
              </div>
            )}

            <button type="button" className="chat-history-btn primary wide chat-history-new-btn" onClick={startNew} disabled={disabled || asking}>
              + New chat
            </button>

            {asking && (
              <div className="chat-history-consent" role="group" aria-label="Save this conversation">
                <p>Save this conversation before starting a new one? Saving will be turned on for your account.</p>
                <div className="chat-history-actions">
                  <button type="button" className="chat-history-btn primary" onClick={saveThenLeave} disabled={busy}>
                    {busy ? 'Saving…' : 'Save & start new'}
                  </button>
                  <button type="button" className="chat-history-btn" onClick={leaveWithoutSaving} disabled={busy}>
                    Don&apos;t save
                  </button>
                  <button type="button" className="chat-history-link" onClick={() => setAsking(false)} disabled={busy}>
                    Cancel
                  </button>
                </div>
              </div>
            )}

            {chats.length === 0 ? (
              <p className="chat-history-empty">
                {consent === 'yes' ? 'Your conversations will appear here once you start chatting.' : 'No saved chats.'}
              </p>
            ) : (
              <ul className="chat-history-list">
                {chats.map((chat, idx) => {
                  const label = dayLabel(chat.updatedAt);
                  const showLabel = idx === 0 || dayLabel(chats[idx - 1].updatedAt) !== label;
                  return (
                    <React.Fragment key={chat.id}>
                      {showLabel && <li className="chat-history-day">{label}</li>}
                      <li className={`chat-history-row ${chat.id === activeId ? 'active' : ''}`}>
                        <button
                          type="button"
                          className="chat-history-open"
                          onClick={() => onOpen(chat)}
                          disabled={disabled}
                          title={chat.title}
                        >
                          <span className="chat-history-title">{chat.title}</span>
                          <span className="chat-history-meta">{chat.messageCount ?? chat.messages?.length ?? 0} messages</span>
                        </button>
                        <button
                          type="button"
                          className="chat-history-delete"
                          aria-label={`Delete chat ${chat.title}`}
                          onClick={() => onDelete(chat.id)}
                        >
                          ×
                        </button>
                      </li>
                    </React.Fragment>
                  );
                })}
              </ul>
            )}

            <div className="chat-history-footer">
              {chats.length > 0 ? (
                <button type="button" className="chat-history-link" onClick={onClear}>
                  Clear all
                </button>
              ) : (
                <span />
              )}
              {consent === 'yes' && (
                <button type="button" className="chat-history-link" onClick={() => onConsent('no')}>
                  Turn off saving
                </button>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
