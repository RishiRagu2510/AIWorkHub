import React, { useState } from 'react';

/**
 * What Athenna has been asked to remember about this person. Drawn only when there are notes, so most people never see it.
 * Notes are added by telling Athenna in the chat ("remember that my main project is FLP360"); here they can be read and
 * removed. Removing everything asks first.
 */
export default function AthennaMemory({ notes, onForget, onForgetAll }) {
  const [open, setOpen] = useState(false);
  const [confirmingAll, setConfirmingAll] = useState(false);
  const items = Array.isArray(notes) ? notes : [];
  if (items.length === 0) return null;

  return (
    <section className="athenna-memory" aria-label="What Athenna remembers">
      <button type="button" className="athenna-memory-toggle" aria-expanded={open} onClick={() => { setOpen(!open); setConfirmingAll(false); }}>
        Athenna remembers {items.length} {items.length === 1 ? 'thing' : 'things'} about you · {open ? 'Hide' : 'Manage'}
      </button>
      {open && (
        <div className="athenna-memory-body">
          <ul className="athenna-memory-list">
            {items.map((note) => (
              <li key={note.id} className="athenna-memory-item">
                <span>{note.text}</span>
                <button type="button" className="athenna-memory-forget" aria-label={`Forget: ${note.text}`} onClick={() => onForget?.(note.id)}>
                  ×
                </button>
              </li>
            ))}
          </ul>
          {confirmingAll ? (
            <p className="athenna-memory-confirm">
              Forget all {items.length}?{' '}
              <button type="button" onClick={() => { setConfirmingAll(false); setOpen(false); onForgetAll?.(); }}>Yes, forget everything</button>{' '}
              <button type="button" onClick={() => setConfirmingAll(false)}>Keep them</button>
            </p>
          ) : (
            <button type="button" className="athenna-memory-clear" onClick={() => setConfirmingAll(true)}>Forget everything</button>
          )}
          <p className="athenna-memory-hint">To add one, tell Athenna: "remember that …".</p>
        </div>
      )}
    </section>
  );
}
