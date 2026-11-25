import { useEffect, useRef, useId } from 'react';
import { ErrorState } from '../../../src/components/common';

export default function DeleteConfirmation({
  title,
  name,
  busy,
  error,
  onCancel,
  onConfirm,
  description = 'This will permanently delete this record. This action cannot be undone.',
  confirmLabel = 'Delete',
  variant = 'danger',
}) {
  const dialog = useRef(null);
  const previousFocus = useRef(document.activeElement);
  const titleId = useId();
  const descriptionId = useId();

  useEffect(() => {
    const previous = previousFocus.current;
    const node = dialog.current;
    node?.showModal?.();
    return () => {
      node?.close?.();
      if (previous?.isConnected) previous.focus();
    };
  }, []);

  const heading = title || `Delete ${name}?`;
  const isWarning = variant === 'warning';

  return (
    <dialog
      ref={dialog}
      className={`setup-delete-dialog ${isWarning ? 'is-warning' : 'is-danger'}`}
      aria-labelledby={titleId}
      aria-describedby={descriptionId}
      onCancel={(e) => {
        e.preventDefault();
        if (!busy) onCancel();
      }}
    >
      <div className={`setup-dialog-icon-wrap ${isWarning ? 'is-warning' : 'is-danger'}`} aria-hidden="true">
        {isWarning ? (
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.25" strokeLinecap="round" strokeLinejoin="round">
            <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
            <line x1="12" y1="9" x2="12" y2="13" />
            <line x1="12" y1="17" x2="12.01" y2="17" />
          </svg>
        ) : (
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.25" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="3 6 5 6 21 6" />
            <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
            <line x1="10" y1="11" x2="10" y2="17" />
            <line x1="14" y1="11" x2="14" y2="17" />
          </svg>
        )}
      </div>

      <div className="setup-dialog-body">
        <h2 id={titleId}>{heading}</h2>
        <p id={descriptionId}>{description}</p>
        {error && <ErrorState error={error} />}
      </div>

      <div className="setup-dialog-actions">
        <button
          type="button"
          autoFocus
          className="setup-dialog-btn-cancel"
          disabled={busy}
          onClick={onCancel}
        >
          Cancel
        </button>
        <button
          type="button"
          className={`setup-dialog-btn-confirm ${isWarning ? 'btn-warning' : 'btn-danger'}`}
          disabled={busy}
          onClick={onConfirm}
        >
          {busy ? 'Processing…' : confirmLabel}
        </button>
      </div>
    </dialog>
  );
}
