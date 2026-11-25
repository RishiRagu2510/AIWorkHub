import { usePresence } from '../collaboration/useLive';
import CommentActions from './CommentActions';
import { useAuth } from '../auth/context';
import { liveQueryOptions } from '../../services/api/live';
import UserIdentity from '../../components/UserIdentity';
import { useUnsavedChanges } from '../../components/unsavedContext';
import { useState } from 'react';
import { useCommentsQuery, useAddCommentMutation } from '../../services/api/api';
import { PermissionGuard } from '../auth/AuthProvider';
import { QueryState, EmptyState, ErrorState, Field } from '../../components/common';
import { useNotice } from '../../components/noticeContext';
import { dateTime } from '../../utils/format';
export function IssueComments({ id, issue }) {
  const { session, can } = useAuth();
  const presence = usePresence();
  const [mentionedUserIds, setMentions] = useState([]);
  const [teammate, setTeammate] = useState('');
  const candidates = [
    ...new Set(
      [issue?.reporterUserId, issue?.assigneeUserId].filter(
        (id) => id && String(id) !== String(session.user.id),
      ),
    ),
  ];
  const { markDirty, clearDirty } = useUnsavedChanges();
  const query = useCommentsQuery(id, liveQueryOptions);
  const [add, mutation] = useAddCommentMutation();
  const [body, setBody] = useState('');
  const notice = useNotice();
  async function submit(e) {
    e.preventDefault();
    if (!body.trim()) return;
    try {
      await add({ id, body: body.trim(), mentionedUserIds }).unwrap();
      clearDirty();
      setBody('');
      presence.typing(false);
      setMentions([]);
      notice('Comment added.');
    } catch {
      /* Error displayed below. */
    }
  }
  return (
    <section className="panel">
      <div className="section-header">
        <h2>Comments</h2>
        <span className="muted">{query.data?.length ?? ''}</span>
      </div>
      <QueryState query={query}>
        {query.data?.length ? (
          <ul className="comments">
            {query.data.map((c) => (
              <li key={c.id}>
                <div className="comment-meta">
                  <strong>
                    <UserIdentity id={c.authorUserId} />
                  </strong>
                  <time>{dateTime(c.createdAt)}</time>
                </div>
                <p className="preserve-lines">{c.body}</p>
                {can('ISSUE_COMMENT_CREATE') &&
                  (session.user.role === 'ADMIN' ||
                    String(c.authorUserId) === String(session.user.id)) && (
                    <CommentActions id={id} comment={c} reload={query.refetch} />
                  )}
              </li>
            ))}
          </ul>
        ) : (
          <EmptyState title="Start the conversation">
            Keep context and decisions together.
          </EmptyState>
        )}
      </QueryState>
      <PermissionGuard permission="ISSUE_COMMENT_CREATE">
        <form className="comment-form" onSubmit={submit}>
          <Field label="Add a comment">
            <textarea
              name="commentBody"
              value={body}
              onChange={(e) => {
                setBody(e.target.value);
                presence.typing(Boolean(e.target.value));
                if (e.target.value) markDirty();
                else clearDirty();
              }}
              onBlur={() => presence.typing(false)}
              maxLength={20000}
              rows="3"
              required
              disabled={mutation.isLoading}
              placeholder="Share an update or ask a question…"
            />
          </Field>
          {candidates.length > 0 && (
            <fieldset className="mention-options">
              <legend>Mention issue participants</legend>
              <p className="hint">
                Notify teammates who have access to this issue. Access is checked when you send.
              </p>
              {candidates.map((userId) => (
                <label key={userId}>
                  <input
                    name="mentionedUserIds"
                    type="checkbox"
                    disabled={mutation.isLoading}
                    checked={mentionedUserIds.includes(userId)}
                    onChange={(e) =>
                      setMentions(
                        e.target.checked
                          ? [...mentionedUserIds, userId]
                          : mentionedUserIds.filter((id) => id !== userId),
                      )
                    }
                  />
                  <UserIdentity id={userId} />
                </label>
              ))}
            </fieldset>
          )}
          {/* <Field label="Mention another teammate by user ID">
            <input
              name="mentionUserId"
              type="number"
              min="1"
              value={teammate}
              onChange={(e) => setTeammate(e.target.value)}
            />
          </Field> */}
          <button
            type="button"
            disabled={
              mutation.isLoading ||
              mentionedUserIds.length >= 10 ||
              !Number.isSafeInteger(Number(teammate)) ||
              Number(teammate) <= 0
            }
            onClick={() => {
              setMentions([...new Set([...mentionedUserIds, Number(teammate)])]);
              setTeammate('');
            }}
          >
            Add mention
          </button>
          {mentionedUserIds
            .filter((id) => !candidates.includes(id))
            .map((userId) => (
              <div key={userId}>
                <UserIdentity id={userId} />
                <button
                  type="button"
                  onClick={() => setMentions(mentionedUserIds.filter((id) => id !== userId))}
                >
                  Remove mention {userId}
                </button>
              </div>
            ))}
          <ErrorState error={mutation.error} />
          <button className="primary" disabled={mutation.isLoading || !body.trim()}>
            {mutation.isLoading ? 'Adding…' : 'Add comment'}
          </button>
        </form>
      </PermissionGuard>
    </section>
  );
}
