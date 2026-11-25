import React, { useEffect, useRef, useState } from 'react';
import { Icon } from './Icon';

function QueueItem({ item, position, onEditing, onSave, onRemove }) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(item.text);
  const field = useRef(null);

  useEffect(() => {
    if (editing) field.current?.focus();
  }, [editing]);

  const start = () => {
    setDraft(item.text);
    setEditing(true);
    onEditing(item.id);
  };
  const finish = () => {
    setEditing(false);
    onEditing(null);
  };
  const save = () => {
    onSave(item.id, draft.trim());
    finish();
  };

  return (
    <li className="chat-queue-item">
      <span className="chat-queue-position" aria-hidden="true">{position}</span>
      {editing ? (
        <input
          ref={field}
          className="chat-queue-edit-input"
          value={draft}
          aria-label={`Edit queued message ${position}`}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              save();
            } else if (e.key === 'Escape') {
              e.stopPropagation();
              finish();
            }
          }}
        />
      ) : (
        <span className="chat-queue-text" title={item.text}>
          {item.text || 'File'}
          {item.attachments?.length > 0 && (
            <span className="chat-queue-files">
              <Icon name="paperclip" /> {item.attachments.map((a) => a.name).join(', ')}
            </span>
          )}
        </span>
      )}
      <span className="chat-queue-actions">
        {editing ? (
          <>
            <button type="button" className="chat-queue-btn primary" onClick={save}>Save</button>
            <button type="button" className="chat-queue-btn" onClick={finish}>Cancel</button>
          </>
        ) : (
          <>
            <button type="button" className="chat-queue-btn" onClick={start} aria-label={`Edit queued message ${position}`}>Edit</button>
            <button type="button" className="chat-queue-btn danger" onClick={() => onRemove(item.id)} aria-label={`Remove queued message ${position}`}>
              <Icon name="close" />
            </button>
          </>
        )}
      </span>
    </li>
  );
}

/** Messages typed while Athenna is still answering. They go out one by one, and each can be changed or dropped first. */
export default function ChatQueue({ items, paused, onEditing, onSave, onRemove, onResume }) {
  if (!items?.length) return null;
  return (
    <section className="chat-queue" aria-label="Queued messages">
      <header className="chat-queue-head">
        <span>{paused ? `Paused · ${items.length} queued` : `${items.length} queued · sent after the current answer`}</span>
        {paused && (
          <button type="button" className="chat-queue-btn primary" onClick={onResume}>Resume</button>
        )}
      </header>
      <ul>
        {items.map((item, index) => (
          <QueueItem key={item.id} item={item} position={index + 1} onEditing={onEditing} onSave={onSave} onRemove={onRemove} />
        ))}
      </ul>
    </section>
  );
}
