import React, { useRef, useState } from 'react';
import { AUDIENCE_ROLES, KNOWLEDGE_ACCEPT, audienceLabel, checkKnowledgeFile, uploadMessage } from './knowledgeUploads';

const formatDate = (value) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
};

/**
 * The company documents Athenna answers from, for Admin and HR. Upload a policy or guide, choose who may read it, and Athenna
 * can use it when anyone asks. Uploading a document with the same title replaces it. The parent supplies the documents and
 * does the saving; `onUpload({ file, title, roles })` resolves to `{ status }` or rejects with `{ message }`.
 */
export default function AthennaKnowledge({ documents, error, onUpload, onDelete }) {
  const [open, setOpen] = useState(false);
  const [file, setFile] = useState(null);
  const [title, setTitle] = useState('');
  const [everyone, setEveryone] = useState(true);
  const [roles, setRoles] = useState([]);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState(null); // { kind: 'ok' | 'error', text }
  const [removing, setRemoving] = useState(null);
  const picker = useRef(null);
  const items = Array.isArray(documents) ? documents : [];

  const reset = () => {
    setFile(null);
    setTitle('');
    setEveryone(true);
    setRoles([]);
    if (picker.current) picker.current.value = '';
  };

  const choose = (event) => {
    const chosen = event.target.files?.[0] || null;
    const problem = chosen ? checkKnowledgeFile(chosen) : null;
    setNotice(problem ? { kind: 'error', text: problem } : null);
    setFile(problem ? null : chosen);
    if (problem && picker.current) picker.current.value = '';
  };

  const toggleRole = (code) => setRoles((held) => (held.includes(code) ? held.filter((r) => r !== code) : [...held, code]));

  const needsRoles = !everyone && roles.length === 0;
  const canUpload = Boolean(file) && !busy && !needsRoles;

  const submit = async (event) => {
    event.preventDefault();
    const problem = checkKnowledgeFile(file);
    if (problem) {
      setNotice({ kind: 'error', text: problem });
      return;
    }
    const shownTitle = title.trim() || file.name.replace(/\.[^.]+$/, '');
    setBusy(true);
    setNotice(null);
    try {
      const result = await onUpload?.({ file, title: title.trim(), roles: everyone ? [] : roles });
      setNotice({ kind: 'ok', text: uploadMessage(result?.status, shownTitle) });
      reset();
    } catch (failure) {
      setNotice({ kind: 'error', text: failure?.message || 'The document could not be uploaded. Please try again.' });
    } finally {
      setBusy(false);
    }
  };

  const remove = async (doc) => {
    setRemoving(null);
    try {
      await onDelete?.(doc.id);
      setNotice({ kind: 'ok', text: `"${doc.title}" was removed.` });
    } catch (failure) {
      setNotice({ kind: 'error', text: failure?.message || 'The document could not be removed. Please try again.' });
    }
  };

  return (
    <section className="athenna-knowledge" aria-label="Company documents">
      <button type="button" className="athenna-knowledge-toggle" aria-expanded={open} onClick={() => { setOpen(!open); setRemoving(null); }}>
        Company documents for Athenna · {items.length === 0 ? 'Add' : `${items.length} ${items.length === 1 ? 'document' : 'documents'}`} · {open ? 'Hide' : 'Manage'}
      </button>
      {open && (
        <div className="athenna-knowledge-body">
          {error ? <p className="athenna-knowledge-note athenna-knowledge-error" role="alert">{error}</p> : null}

          {items.length === 0 && !error ? (
            <p className="athenna-knowledge-empty">No documents yet. Upload a policy or guide and Athenna can answer questions from it.</p>
          ) : (
            <ul className="athenna-knowledge-list">
              {items.map((doc) => (
                <li key={doc.id} className="athenna-knowledge-item">
                  <div className="athenna-knowledge-main">
                    <strong>{doc.title}</strong>
                    <span className="athenna-knowledge-meta">
                      {audienceLabel(doc)} · v{doc.version} · {formatDate(doc.updatedAt)}
                    </span>
                  </div>
                  {removing === doc.id ? (
                    <span className="athenna-knowledge-confirm">
                      Remove it?{' '}
                      <button type="button" onClick={() => remove(doc)}>Yes, remove</button>{' '}
                      <button type="button" onClick={() => setRemoving(null)}>Keep</button>
                    </span>
                  ) : (
                    <button type="button" className="athenna-knowledge-remove" aria-label={`Remove ${doc.title}`} onClick={() => setRemoving(doc.id)}>
                      ×
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}

          <form className="athenna-knowledge-form" onSubmit={submit}>
            <label className="athenna-knowledge-field">
              <span>File (PDF, Word, text or Markdown, up to 10 MB)</span>
              <input ref={picker} type="file" accept={KNOWLEDGE_ACCEPT} onChange={choose} disabled={busy} aria-label="Document file" />
            </label>
            <label className="athenna-knowledge-field">
              <span>Title (uploading the same title again replaces the document)</span>
              <input type="text" value={title} maxLength={200} placeholder="Defaults to the file name" onChange={(e) => setTitle(e.target.value)} disabled={busy} aria-label="Document title" />
            </label>
            <fieldset className="athenna-knowledge-audience" disabled={busy}>
              <legend>Who can ask about it</legend>
              <label><input type="radio" name="knowledge-audience" checked={everyone} onChange={() => setEveryone(true)} /> Everyone</label>
              <label><input type="radio" name="knowledge-audience" checked={!everyone} onChange={() => setEveryone(false)} /> Only these roles (Admin and HR always can)</label>
              {!everyone && (
                <div className="athenna-knowledge-roles">
                  {AUDIENCE_ROLES.map((role) => (
                    <label key={role.code}>
                      <input type="checkbox" checked={roles.includes(role.code)} onChange={() => toggleRole(role.code)} /> {role.label}
                    </label>
                  ))}
                </div>
              )}
            </fieldset>
            {needsRoles && <p className="athenna-knowledge-note">Choose at least one role, or switch to Everyone.</p>}
            <button type="submit" className="athenna-knowledge-upload" disabled={!canUpload}>
              {busy ? 'Uploading…' : 'Upload'}
            </button>
          </form>

          {notice && (
            <p className={`athenna-knowledge-note ${notice.kind === 'error' ? 'athenna-knowledge-error' : 'athenna-knowledge-ok'}`} role={notice.kind === 'error' ? 'alert' : 'status'}>
              {notice.text}
            </p>
          )}
        </div>
      )}
    </section>
  );
}
