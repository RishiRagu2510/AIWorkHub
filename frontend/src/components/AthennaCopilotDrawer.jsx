import React, { useState, useRef, useEffect } from 'react';
import { useChatWithAthennaMutation } from '../services/api/agent';
import './AthennaCopilotDrawer.css';

export const AthennaCopilotDrawer = ({ isOpen, onClose, currentPortal = 'AIWorkHub' }) => {
  const [messages, setMessages] = useState([
    {
      role: 'assistant',
      content: `Hello! I am **Athenna**, your autonomous workspace copilot across GridHub, Persona, and Chronos. What would you like to plan, track, or verify today?`,
    },
  ]);
  const [input, setInput] = useState('');
  const [sessionId] = useState(() => 'sess_' + Math.random().toString(36).substring(2, 11));
  const [chatWithAthenna, { isLoading }] = useChatWithAthennaMutation();
  const messagesEndRef = useRef(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView?.({ behavior: 'smooth' });
  }, [messages, isLoading]);

  const handleSend = async (e) => {
    e?.preventDefault();
    const trimmed = input.trim();
    if (!trimmed || isLoading) return;

    const userMsg = { role: 'user', content: trimmed };
    setMessages((prev) => [...prev, userMsg]);
    setInput('');

    try {
      const response = await chatWithAthenna({
        message: trimmed,
        sessionId,
        contextPortal: currentPortal,
        history: messages.slice(-8), // Send recent context
      }).unwrap();

      if (response?.message) {
        setMessages((prev) => [
          ...prev,
          {
            role: 'assistant',
            content: response.message,
            actions: response.executedActions,
          },
        ]);
      }
    } catch {
      setMessages((prev) => [
        ...prev,
        {
          role: 'assistant',
          content: 'Sorry, I encountered an issue connecting to the Athenna agent engine. Please ensure the agent backend is running.',
          isError: true,
        },
      ]);
    }
  };

  const handleQuickPrompt = (promptText) => {
    setInput(promptText);
  };

  if (!isOpen) return null;

  return (
    <div className="athenna-drawer-backdrop" onClick={onClose}>
      <aside
        className="athenna-drawer"
        role="dialog"
        aria-label="Athenna Workspace Copilot"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="athenna-drawer-header">
          <div className="athenna-brand">
            <span className="athenna-avatar">✦</span>
            <div>
              <h2 className="athenna-title">Athenna</h2>
              <span className="athenna-badge">{currentPortal} Copilot</span>
            </div>
          </div>
          <button
            type="button"
            className="athenna-close-btn"
            onClick={onClose}
            aria-label="Close copilot"
          >
            ×
          </button>
        </header>

        <div className="athenna-quick-prompts">
          <button
            type="button"
            className="quick-chip"
            onClick={() => handleQuickPrompt('Raise a bug for login failure on mobile safari')}
          >
            ⚡ Raise a Bug in GridHub
          </button>
          <button
            type="button"
            className="quick-chip"
            onClick={() => handleQuickPrompt('Log 2 hours of engineering development today')}
          >
            ⏱ Automate Worklog
          </button>
          <button
            type="button"
            className="quick-chip"
            onClick={() => handleQuickPrompt('Check if anyone is on leave next week for sprint planning')}
          >
            🏖 Check Leave Conflicts
          </button>
          <button
            type="button"
            className="quick-chip"
            onClick={() => handleQuickPrompt('Search active high priority bugs in GridHub')}
          >
            🐛 Search Active Bugs
          </button>
        </div>

        <div className="athenna-messages-container">
          {messages.map((m, idx) => (
            <div key={idx} className={`athenna-msg-bubble ${m.role} ${m.isError ? 'error' : ''}`}>
              <div className="athenna-msg-sender">
                {m.role === 'assistant' ? '✦ Athenna' : 'You'}
              </div>
              <div className="athenna-msg-text">{m.content}</div>

              {m.actions && m.actions.length > 0 && (
                <div className="athenna-tool-actions">
                  <span className="tool-actions-title">Executed Tools:</span>
                  {m.actions.map((act, aIdx) => (
                    <div key={aIdx} className={`tool-action-tag ${act.success ? 'success' : 'failed'}`}>
                      ⚙ {act.toolName} ({act.description})
                    </div>
                  ))}
                </div>
              )}
            </div>
          ))}

          {isLoading && (
            <div className="athenna-msg-bubble assistant loading">
              <div className="athenna-msg-sender">✦ Athenna</div>
              <div className="athenna-typing-indicator">
                <span></span>
                <span></span>
                <span></span>
              </div>
            </div>
          )}
          <div ref={messagesEndRef} />
        </div>

        <form className="athenna-input-form" onSubmit={handleSend}>
          <input
            type="text"
            className="athenna-input"
            placeholder={`Ask Athenna anything in ${currentPortal}...`}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            disabled={isLoading}
            autoFocus
          />
          <button
            type="submit"
            className="athenna-send-btn"
            disabled={isLoading || !input.trim()}
          >
            Send
          </button>
        </form>
      </aside>
    </div>
  );
};
