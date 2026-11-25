import { useAuth } from '../auth/context';
import { useRef, useState } from 'react';
import {
  useAttachmentsQuery,
  useUploadAttachmentMutation,
  useDeleteAttachmentMutation,
} from '../../services/api/api';
import { request } from '../../services/api/client';
import { PermissionGuard } from '../auth/AuthProvider';
import { QueryState, ErrorState, Field, ConfirmDialog } from '../../components/common';
import { useNotice } from '../../components/noticeContext';
export default function IssueAttachments({ id }) {
  const query = useAttachmentsQuery(id);
  const { session, can } = useAuth();
  const [removing, setRemoving] = useState(null);
  const [remove, deletion] = useDeleteAttachmentMutation();
  const [upload, mutation] = useUploadAttachmentMutation();
  const [file, setFile] = useState(null);
  const [error, setError] = useState(null);
  const [downloading, setDownloading] = useState(null);
  const input = useRef(null);
  const lock = useRef(false);
  const notice = useNotice();
  async function submit(e) {
    e.preventDefault();
    if (!file || lock.current) return;
    if (file.size <= 0 || file.size > 10 * 1024 * 1024) {
      setError({ message: 'Choose a file between 1 byte and 10 MB.' });
      return;
    }
    lock.current = true;
    setError(null);
    try {
      await upload({ id, file }).unwrap();
      setFile(null);
      input.current.value = '';
      notice('Attachment uploaded.');
    } catch {
      /* Preserve selected file. */
    } finally {
      lock.current = false;
    }
  }
  async function download(item) {
    setDownloading(item.id);
    setError(null);
    try {
      const blob = await request(`/api/issues/${id}/attachments/${item.id}/download`, {
        responseType: 'blob',
      });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = item.name;
      document.body.append(link);
      link.click();
      link.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (e) {
      setError(e);
    } finally {
      setDownloading(null);
    }
  }
  return (
    <section className="panel">
      <div className="section-header">
        <h2>Attachments</h2>
        <button onClick={query.refetch} disabled={query.isFetching}>
          Refresh files
        </button>
      </div>
      {removing && (
        <ConfirmDialog
          title="Delete attachment?"
          confirmLabel="Delete attachment"
          busy={deletion.isLoading}
          error={deletion.error}
          close={() => {
            setRemoving(null);
            deletion.reset();
          }}
          confirm={async () => {
            try {
              await remove({ id, attachmentId: removing.id }).unwrap();
              setRemoving(null);
              notice('Attachment deleted.');
            } catch {
              /* Keep confirmation. */
            }
          }}
        >
          <p>{removing.name} will be removed from this issue.</p>
        </ConfirmDialog>
      )}
      <div className="collaboration-body">
        <QueryState query={query}>
          {query.data?.length ? (
            <ul className="attachment-list">
              {query.data.map((item) => (
                <li key={item.id}>
                  <span>
                    {item.name} <small>({Math.ceil(item.size / 1024)} KB)</small>
                  </span>
                  <button disabled={downloading !== null} onClick={() => download(item)}>
                    {downloading === item.id ? 'Downloading…' : 'Download'}
                  </button>
                  {can('ISSUE_COMMENT_CREATE') &&
                    (session.user.role === 'ADMIN' ||
                      String(session.user.id) === String(item.authorUserId)) && (
                      <button onClick={() => setRemoving(item)}>Delete file</button>
                    )}
                </li>
              ))}
            </ul>
          ) : (
            <p className="hint">No attachments yet. Add screenshots or supporting documents.</p>
          )}
        </QueryState>
        <PermissionGuard permission="ISSUE_COMMENT_CREATE">
          <form onSubmit={submit}>
            <Field label="Attach a file">
              <input
                name="attachment"
                ref={input}
                type="file"
                disabled={mutation.isLoading}
                onChange={(e) => {
                  setFile(e.target.files[0] || null);
                  setError(null);
                }}
              />
            </Field>
            <p className="hint">Maximum file size: 10 MB.</p>
            <button className="primary" disabled={!file || mutation.isLoading}>
              {mutation.isLoading ? 'Uploading…' : 'Upload file'}
            </button>
          </form>
        </PermissionGuard>
        <ErrorState error={error || mutation.error} />
      </div>
    </section>
  );
}
