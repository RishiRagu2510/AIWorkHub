import React, { useEffect, useState } from 'react';
import { Icon } from './Icon';
import { downloadAttachment, fetchAttachmentBlob, formatSize, isImageFile } from './athennaAttachments';

/** A picture, fetched with the user's token and shown as a thumbnail. Clicking it saves the file. */
function Thumbnail({ attachment }) {
  const [url, setUrl] = useState(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    let made = null;
    fetchAttachmentBlob(attachment.id)
      .then((blob) => {
        if (cancelled) return;
        made = URL.createObjectURL(blob);
        setUrl(made);
      })
      .catch(() => !cancelled && setFailed(true));
    return () => {
      cancelled = true;
      if (made) URL.revokeObjectURL(made);
    };
  }, [attachment.id]);

  if (failed) return <FileChip attachment={attachment} gone />;
  return (
    <button
      type="button"
      className="chat-attachment-thumb"
      title={`${attachment.name} (${formatSize(attachment.size)}). Click to save.`}
      aria-label={`Save ${attachment.name}`}
      onClick={() => downloadAttachment(attachment).catch(() => {})}
    >
      {url ? <img src={url} alt={attachment.name} /> : <span className="chat-attachment-thumb-loading" aria-hidden="true" />}
    </button>
  );
}

function FileChip({ attachment, gone = false }) {
  const [error, setError] = useState(false);
  return (
    <button
      type="button"
      className={`chat-attachment-file ${gone || error ? 'gone' : ''}`}
      disabled={gone}
      title={gone || error ? 'This file is no longer available.' : `Save ${attachment.name}`}
      onClick={() => downloadAttachment(attachment).catch(() => setError(true))}
    >
      <Icon name="file" />
      <span className="chat-attachment-name">{attachment.name}</span>
      <span className="chat-attachment-size">{gone || error ? 'No longer available' : formatSize(attachment.size)}</span>
    </button>
  );
}

/** The files that were sent with a message, inside its bubble. */
export function MessageAttachments({ attachments }) {
  if (!Array.isArray(attachments) || attachments.length === 0) return null;
  return (
    <div className="chat-attachments" aria-label="Attached files">
      {attachments.map((a) =>
        isImageFile(a) ? <Thumbnail key={a.id} attachment={a} /> : <FileChip key={a.id} attachment={a} />,
      )}
    </div>
  );
}

/** The files waiting to be sent, above the prompt. Each can be removed. */
export function ComposerAttachments({ items, onRemove }) {
  if (!items?.length) return null;
  return (
    <ul className="composer-attachments" aria-label="Files to send">
      {items.map((f) => (
        <li key={f.key} className={`composer-attachment ${f.status}`}>
          <Icon name="file" />
          <span className="composer-attachment-name" title={f.name}>{f.name}</span>
          <span className="composer-attachment-state">
            {f.status === 'uploading' ? 'Uploading…' : f.status === 'error' ? f.error || 'Failed' : formatSize(f.size)}
          </span>
          <button type="button" className="composer-attachment-remove" onClick={() => onRemove(f)} aria-label={`Remove ${f.name}`}>
            <Icon name="close" />
          </button>
        </li>
      ))}
    </ul>
  );
}
