import { useState } from 'react';
import { useEditCommentMutation, useDeleteCommentMutation } from '../../services/api/api';
import { ConfirmDialog, ErrorState, Field } from '../../components/common';
import { useNotice } from '../../components/noticeContext';
import { useUnsavedChanges } from '../../components/unsavedContext';
export default function CommentActions({ id, comment, reload }) {
  const [draft, setDraft] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [current, setCurrent] = useState(null);
  const [edit, editing] = useEditCommentMutation();
  const [remove, removing] = useDeleteCommentMutation();
  const { markDirty, clearDirty } = useUnsavedChanges();
  const notice = useNotice();
  const error = editing.error || removing.error;
  async function review() {
    const result = await reload();
    if (!result.error)
      setCurrent(result.data.find((c) => c.id === comment.id) || { deleted: true });
  }
  async function save(event) {
    event.preventDefault();
    try {
      await edit({ id, commentId: comment.id, ...draft }).unwrap();
      clearDirty();
      setDraft(null);
      setCurrent(null);
      notice('Comment updated.');
    } catch {
      /* Keep draft and its original version. */
    }
  }
  return (
    <div className="comment-actions">
      {draft ? (
        <form onSubmit={save}>
          <Field label="Edit comment">
            <textarea
              name="commentBody"
              required
              maxLength={20000}
              rows={4}
              value={draft.body}
              disabled={editing.isLoading}
              onChange={(e) => {
                setDraft({ ...draft, body: e.target.value });
                markDirty();
              }}
            />
          </Field>
          <div className="actions">
            <button
              type="button"
              disabled={editing.isLoading}
              onClick={() => {
                setDraft(null);
                setCurrent(null);
                editing.reset();
                clearDirty();
              }}
            >
              Cancel edit
            </button>
            <button
              className="primary"
              disabled={editing.isLoading || !draft.body.trim() || error?.status === 412}
            >
              Save comment
            </button>
          </div>
        </form>
      ) : (
        <div className="actions">
          <button
            onClick={() => {
              editing.reset();
              removing.reset();
              setDraft({ body: comment.body, version: comment.version });
            }}
          >
            Edit comment
          </button>
          <button
            onClick={() => {
              removing.reset();
              editing.reset();
              setDeleting(comment.version);
            }}
          >
            Delete comment
          </button>
        </div>
      )}
      {error?.status === 412 && (
        <div role="alert" className="conflict-panel">
          <p>This comment changed. Your draft is preserved.</p>
          <button onClick={review}>Load latest comment for review</button>
          {current && (
            <>
              <p>{current.deleted ? 'This comment was deleted.' : current.body}</p>
              {!current.deleted && draft && (
                <button
                  onClick={() => {
                    setDraft({ ...draft, version: current.version });
                    editing.reset();
                    removing.reset();
                  }}
                >
                  Keep my draft after reviewing
                </button>
              )}
            </>
          )}
        </div>
      )}
      {!deleting && <ErrorState error={error} />}
      {deleting && (
        <ConfirmDialog
          title="Delete comment?"
          confirmLabel="Delete comment"
          busy={removing.isLoading}
          error={removing.error}
          confirmDisabled={removing.error?.status === 412}
          close={() => {
            setDeleting(null);
            removing.reset();
          }}
          confirm={async () => {
            try {
              await remove({ id, commentId: comment.id, version: deleting }).unwrap();
              setDeleting(null);
              notice('Comment deleted.');
            } catch {
              /* Explicit retry only. */
            }
          }}
        >
          <p>This removes the comment from this issue. Its audit history is retained.</p>
        </ConfirmDialog>
      )}
    </div>
  );
}
