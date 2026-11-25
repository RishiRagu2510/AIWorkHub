import React, { useRef, useState } from 'react';
import { useAuth } from '../../../src/features/auth/context';
import {
  useGetAgentKnowledgeQuery,
  useGetAgentKnowledgeDocQuery,
  useUploadAgentKnowledgeMutation,
  useDeleteAgentKnowledgeMutation,
} from '../../../src/services/api/agent';
import {
  AUDIENCE_ROLES,
  KNOWLEDGE_ACCEPT,
  audienceLabel,
  checkKnowledgeFile,
} from '../../../src/components/knowledgeUploads';
import { canManageKnowledge } from '../../../src/components/knowledgeUploads';
import { Icon } from '../../../src/components/Icon';
import '../../../src/styles/knowledge-page.scss';

const formatDate = (value) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? ''
    : date.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
};

export default function KnowledgePage() {
  const { session } = useAuth();
  const userRole = session?.user?.role || 'DEVELOPER';
  const isAdminOrHr = canManageKnowledge(userRole);

  const { data, error, isLoading, refetch } = useGetAgentKnowledgeQuery(undefined, {
    skip: !isAdminOrHr,
  });
  const [uploadKnowledge] = useUploadAgentKnowledgeMutation();
  const [deleteKnowledge] = useDeleteAgentKnowledgeMutation();

  const [file, setFile] = useState(null);
  const [title, setTitle] = useState('');
  const [everyone, setEveryone] = useState(true);
  const [roles, setRoles] = useState([]);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState(null);
  const [removing, setRemoving] = useState(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const [previewDocId, setPreviewDocId] = useState(null);
  const picker = useRef(null);

  const {
    data: previewData,
    isLoading: isPreviewLoading,
    error: previewError,
  } = useGetAgentKnowledgeDocQuery(previewDocId, {
    skip: !previewDocId,
  });

  const items = Array.isArray(data?.documents) ? data.documents : [];

  const reset = () => {
    setFile(null);
    setTitle('');
    setEveryone(true);
    setRoles([]);
    if (picker.current) picker.current.value = '';
  };

  const handleSelectedFile = (chosen) => {
    const problem = chosen ? checkKnowledgeFile(chosen) : null;
    setNotice(problem ? { kind: 'error', text: problem } : null);
    setFile(problem ? null : chosen);
    if (!title && chosen && !problem) {
      const cleanName = chosen.name.replace(/\.[^/.]+$/, '').replace(/[-_]/g, ' ');
      setTitle(cleanName.charAt(0).toUpperCase() + cleanName.slice(1));
    }
  };

  const choose = (event) => {
    const chosen = event.target.files?.[0] || null;
    handleSelectedFile(chosen);
    if (picker.current) picker.current.value = '';
  };

  const onDragOver = (e) => {
    e.preventDefault();
    setIsDragOver(true);
  };

  const onDragLeave = () => {
    setIsDragOver(false);
  };

  const onDrop = (e) => {
    e.preventDefault();
    setIsDragOver(false);
    const chosen = e.dataTransfer.files?.[0] || null;
    handleSelectedFile(chosen);
  };

  const toggleRole = (code) =>
    setRoles((held) => (held.includes(code) ? held.filter((r) => r !== code) : [...held, code]));

  const needsRoles = !everyone && roles.length === 0;
  const canUpload = Boolean(file) && !busy && !needsRoles;

  const submit = async (event) => {
    event.preventDefault();
    const problem = checkKnowledgeFile(file);
    if (problem) {
      setNotice({ kind: 'error', text: problem });
      return;
    }
    setBusy(true);
    setNotice(null);
    try {
      const outcome = await uploadKnowledge({
        file,
        title: title.trim() || undefined,
        roles: everyone ? [] : roles,
      }).unwrap();
      const docTitle = title.trim() || file.name;
      setNotice({
        kind: 'ok',
        text: outcome?.status === 'replaced'
          ? `"${docTitle}" was updated.`
          : `"${docTitle}" was added. Athenna can answer from it now.`,
      });
      reset();
      refetch();
    } catch (failure) {
      setNotice({
        kind: 'error',
        text: failure?.data?.error || failure?.message || 'The document could not be saved. Please try again.',
      });
    } finally {
      setBusy(false);
    }
  };

  const remove = async (doc) => {
    setRemoving(null);
    if (previewDocId === doc.id) {
      setPreviewDocId(null);
    }
    try {
      await deleteKnowledge(doc.id).unwrap();
      setNotice({ kind: 'ok', text: `"${doc.title}" was removed.` });
      refetch();
    } catch (failure) {
      setNotice({
        kind: 'error',
        text: failure?.data?.error || failure?.message || 'The document could not be removed.',
      });
    }
  };

  return (
    <div className="knowledge-page-container">
      {/* Top Banner & Status */}
      <div className="knowledge-page-header">
        <div className="knowledge-header-info">
          <h2>Company Knowledge Base</h2>
          <p>
            Upload handbooks, policy documents, and engineering specs. Athenna indexes and queries them with semantic vector search. Click any document below to view its full extracted text.
          </p>
        </div>
        <div className="knowledge-header-meta">
          {/* <div className="rag-status-badge">
            <span className="rag-live-dot"></span>
            <span className="rag-label">pgvector RAG Active</span>
          </div> */}
          <div className="knowledge-doc-counter">
            <span className="count-number">{items.length}</span>
            <span className="count-label">indexed docs</span>
          </div>
        </div>
      </div>

      {/* Main Content Layout */}
      <div className="knowledge-page-body">
        {/* Left Side: Upload Panel */}
        {isAdminOrHr ? (
          <div className="knowledge-upload-panel">
            <div className="panel-header">
              <div className="panel-header-icon-box">
                <Icon name="plus" />
              </div>
              <div className="panel-header-text">
                <h3>Upload Document</h3>
                <span>Ingest new documentation into Athenna</span>
              </div>
            </div>

            <form className="knowledge-upload-form" onSubmit={submit}>
              {/* Dropzone */}
              <div
                className={`custom-dropzone ${isDragOver ? 'is-drag-over' : ''} ${file ? 'has-file' : ''}`}
                onDragOver={onDragOver}
                onDragLeave={onDragLeave}
                onDrop={onDrop}
                onClick={() => picker.current?.click()}
                role="button"
                tabIndex={0}
              >
                <input
                  ref={picker}
                  type="file"
                  accept={KNOWLEDGE_ACCEPT}
                  onChange={choose}
                  disabled={busy}
                  style={{ display: 'none' }}
                />
                <div className="dropzone-body">
                  <div className="dropzone-icon">
                    {file ? '📄' : '📁'}
                  </div>
                  <div className="dropzone-text-group">
                    <strong className="dropzone-main-text">
                      {file ? file.name : 'Click to select or drag & drop'}
                    </strong>
                    <span className="dropzone-sub-text">
                      {file ? `${(file.size / 1024 / 1024).toFixed(2)} MB • Ready to upload` : 'PDF, DOCX, TXT, MD up to 10 MB'}
                    </span>
                  </div>
                </div>
              </div>

              {/* Document Title */}
              <div className="form-field">
                <label htmlFor="doc-title-input">Document Title</label>
                <input
                  id="doc-title-input"
                  type="text"
                  value={title}
                  maxLength={200}
                  placeholder="e.g. Employee Handbook 2026"
                  onChange={(e) => setTitle(e.target.value)}
                  disabled={busy}
                />
              </div>

              {/* Target Audience Segmented Cards */}
              <div className="form-field">
                <label>Target Audience (Access Permissions)</label>
                <div className="audience-pill-selector">
                  <button
                    type="button"
                    className={`audience-pill-btn ${everyone ? 'selected' : ''}`}
                    onClick={() => setEveryone(true)}
                  >
                    <Icon name="user" />
                    <div className="pill-text-content">
                      <span className="pill-title">Everyone</span>
                      <span className="pill-subtitle">All workspace members</span>
                    </div>
                  </button>

                  <button
                    type="button"
                    className={`audience-pill-btn ${!everyone ? 'selected' : ''}`}
                    onClick={() => setEveryone(false)}
                  >
                    <Icon name="shield" />
                    <div className="pill-text-content">
                      <span className="pill-title">Specific Roles</span>
                      <span className="pill-subtitle">Restricted access</span>
                    </div>
                  </button>
                </div>

                {!everyone && (
                  <div className="roles-checklist-card">
                    <span className="roles-hint">Select the roles permitted to query this document:</span>
                    <div className="roles-grid">
                      {AUDIENCE_ROLES.map((r) => (
                        <label key={r.code} className={`role-checkbox-item ${roles.includes(r.code) ? 'checked' : ''}`}>
                          <input
                            type="checkbox"
                            checked={roles.includes(r.code)}
                            onChange={() => toggleRole(r.code)}
                          />
                          <span>{r.label}</span>
                        </label>
                      ))}
                    </div>
                  </div>
                )}
                {needsRoles && (
                  <p className="field-error-hint">Please choose at least one role, or switch to Everyone.</p>
                )}
              </div>

              <button type="submit" className="upload-btn" disabled={!canUpload}>
                {busy ? (
                  <span className="btn-spinner-content">
                    <span className="mini-spinner"></span>
                    Vectorizing Passages…
                  </span>
                ) : (
                  'Upload to Knowledge Base'
                )}
              </button>

              {notice && (
                <div className={`form-notice ${notice.kind}`}>
                  <span className="notice-icon">{notice.kind === 'ok' ? '✓' : '⚠️'}</span>
                  <span>{notice.text}</span>
                </div>
              )}
            </form>
          </div>
        ) : (
          <div className="knowledge-upload-panel readonly">
            <div className="panel-header">
              <h3>Knowledge Viewer</h3>
              <p>You can query indexed documents via Athenna Chat.</p>
            </div>
          </div>
        )}

        {/* Right Side: Stored Documents Table */}
        <div className="knowledge-list-panel">
          <div className="panel-header">
            <div className="panel-header-icon-box blue">
              <Icon name="folder" />
            </div>
            <div className="panel-header-text">
              <h3>Indexed Documents ({items.length})</h3>
              <span>Click on any document row to view its extracted text</span>
            </div>
          </div>

          {isLoading ? (
            <div className="knowledge-loading-state">
              <div className="mini-spinner large"></div>
              <span>Loading documents…</span>
            </div>
          ) : error ? (
            <div className="form-notice error">
              <span>⚠️</span>
              <span>Failed to load knowledge documents: {error?.message || 'Access error'}</span>
            </div>
          ) : items.length === 0 ? (
            <div className="knowledge-empty-state">
              <div className="empty-illustration">📚</div>
              <h4>No documents indexed yet</h4>
              <p>
                Upload your company handbook, remote work policy, or technical guides on the left. Athenna will chunk and vectorize them to answer queries with citations.
              </p>
            </div>
          ) : (
            <div className="knowledge-table-card">
              <table className="knowledge-data-table">
                <thead>
                  <tr>
                    <th>Document</th>
                    <th>Audience</th>
                    <th>Version</th>
                    <th>Last Updated</th>
                    {isAdminOrHr && <th className="text-right">Action</th>}
                  </tr>
                </thead>
                <tbody>
                  {items.map((doc) => (
                    <tr
                      key={doc.id}
                      className={`doc-table-row ${previewDocId === doc.id ? 'active-preview-row' : ''}`}
                      onClick={() => setPreviewDocId(doc.id)}
                      title="Click to view document content"
                    >
                      <td className="doc-main-cell">
                        <span className="file-badge-icon">📄</span>
                        <div className="doc-title-block">
                          <strong className="doc-name">{doc.title}</strong>
                          {/* <span className="doc-meta-id">
                            ID: #{doc.id} • {doc.characters ? `${doc.characters.toLocaleString()} chars` : ''} • {doc.passages ? `${doc.passages} chunks` : ''}
                          </span> */}
                        </div>
                      </td>
                      <td>
                        <span className="audience-pill-tag">
                          {audienceLabel(doc)}
                        </span>
                      </td>
                      <td>
                        <span className="version-tag">v{doc.version}</span>
                      </td>
                      <td className="date-cell-text">{formatDate(doc.updatedAt)}</td>
                      {isAdminOrHr && (
                        <td className="text-right" onClick={(e) => e.stopPropagation()}>
                          {removing === doc.id ? (
                            <div className="delete-confirm-box">
                              {/* <span>Remove?</span> */}
                              <button
                                type="button"
                                className="confirm-delete-btn yes"
                                onClick={() => remove(doc)}
                              >
                                Delete
                              </button>
                              <button
                                type="button"
                                className="confirm-delete-btn no"
                                onClick={() => setRemoving(null)}
                              >
                                Cancel
                              </button>
                            </div>
                          ) : (
                            <button
                              type="button"
                              className="table-action-delete"
                              title="Delete document"
                              onClick={() => setRemoving(doc.id)}
                            >
                              <Icon name="trash" />
                            </button>
                          )}
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* Document Content Preview Modal / Drawer */}
      {previewDocId && (
        <div className="doc-preview-backdrop" onClick={() => setPreviewDocId(null)}>
          <div className="doc-preview-modal" onClick={(e) => e.stopPropagation()}>
            <div className="doc-preview-header">
              <div className="preview-header-left">
                <span className="preview-icon">📄</span>
                <div>
                  <h3>{previewData?.title || 'Document Content'}</h3>
                  <span className="preview-meta">
                    v{previewData?.version || 1} • {previewData?.characters?.toLocaleString() || 0} characters • Audience: {previewData?.everyone ? 'Everyone' : (previewData?.roles || []).join(', ')}
                  </span>
                </div>
              </div>
              <button
                type="button"
                className="close-preview-btn"
                onClick={() => setPreviewDocId(null)}
                title="Close preview"
              >
                ✕
              </button>
            </div>

            <div className="doc-preview-body">
              {isPreviewLoading ? (
                <div className="preview-loading">
                  <div className="mini-spinner large"></div>
                  <span>Loading full document text…</span>
                </div>
              ) : previewError ? (
                <div className="form-notice error">
                  <span>Failed to load document content: {previewError?.message || 'Error'}</span>
                </div>
              ) : (
                <div className="preview-text-container">
                  <pre>{previewData?.content || 'No text extracted from this document.'}</pre>
                </div>
              )}
            </div>

            <div className="doc-preview-footer">
              <span className="footer-hint">This text is chunked and searched by Athenna with cosine similarity (pgvector).</span>
              <button
                type="button"
                className="preview-close-button"
                onClick={() => setPreviewDocId(null)}
              >
                Done Reading
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
