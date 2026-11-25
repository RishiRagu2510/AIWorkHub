import { Presence } from '../../../src/features/collaboration/IssueCollaboration';
import { useCollaborationLive } from '../../../src/features/collaboration/useLive';
import { copyIssueFields } from '../../../src/features/issues/workViews';
import { readDraft, saveDraft, removeDraft } from '../../../src/features/issues/drafts';
import { preferenceKey } from '../../../src/utils/preferences';
import { useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router';
import { useCreateIssueMutation, useIssueQuery, useUpdateIssueMutation } from '../../../src/services/api/api';
import { PageHeader, QueryState } from '../../../src/components/common';
import VersionConflict from '../../../src/components/VersionConflict';
import { useNotice } from '../../../src/components/noticeContext';
import { IssueForm } from '../../../src/features/issues/IssueForm';
import { useAuth } from '../../../src/features/auth/context';
function EditingPresence({ id }) {
  const live = useCollaborationLive('ISSUE', id, { editing: true });
  return <Presence live={live} />;
}
function Editor({ initial, defaults, draftKey, recovered }) {
  // Keep the exact resource that populated the form even if another view refreshes the cache.
  const [snapshot] = useState(initial ? { ...initial, ...recovered?.values } : undefined);
  const [version, setVersion] = useState(recovered?.version ?? initial?.version);
  const navigate = useNavigate();
  const notice = useNotice();
  const { can } = useAuth();
  const [create, creation] = useCreateIssueMutation();
  const [update, editing] = useUpdateIssueMutation();
  async function save(body, onSaved) {
    if (editing.error?.status === 412) return;
    try {
      const result = await (
        snapshot ? update({ id: snapshot.id, version, body }) : create(body)
      ).unwrap();
      removeDraft(draftKey);
      onSaved?.();
      notice(snapshot ? 'Issue updated.' : 'Issue created.');
      navigate(can('ISSUE_VIEW') ? `/issues/${result.id}` : '/dashboard');
    } catch {
      /* Preserve the original form and its version until explicit reconciliation. */
    }
  }
  return (
    <>
      {snapshot && <EditingPresence id={snapshot.id} />}
      <IssueForm
        issue={snapshot}
        defaults={recovered?.values || defaults}
        restored={Boolean(recovered)}
        onDraft={(values) => saveDraft(draftKey, values, version)}
        save={save}
        saveBlocked={editing.error?.status === 412}
        busy={creation.isLoading || editing.isLoading}
        error={editing.error?.status === 412 ? null : creation.error || editing.error}
      />
      {snapshot && (
        <VersionConflict
          error={editing.error?.status === 412 ? editing.error : null}
          path={`/api/issues/${snapshot.id}`}
          onReviewed={(current) => {
            setVersion(current.version);
            editing.reset();
          }}
        />
      )}
    </>
  );
}
function DraftEditor({ initial, defaults, context }) {
  const { session } = useAuth();
  const draftKey = preferenceKey(session.user, `issue-draft:${context}`);
  const [draft] = useState(() => readDraft(draftKey));
  const [choice, setChoice] = useState(draft ? null : 'new');
  if (!choice)
    return (
      <section className="panel draft-recovery">
        <h2>Resume your unfinished issue?</h2>
        <p>
          A draft was saved on this browser. Restore it to continue, or discard it to start fresh.
          Drafts expire after seven days.
        </p>
        <div className="actions">
          <button className="primary" onClick={() => setChoice('restore')}>
            Restore draft
          </button>
          <button
            onClick={() => {
              removeDraft(draftKey);
              setChoice('new');
            }}
          >
            Discard draft
          </button>
        </div>
      </section>
    );
  return (
    <>
      <p className="hint">
        Changes to issue fields are saved on this browser for seven days. Assignee selections are
        not saved.
      </p>
      <Editor
        initial={initial}
        defaults={defaults}
        draftKey={draftKey}
        recovered={choice === 'restore' ? draft : null}
      />
    </>
  );
}
export default function IssueEditor() {
  const { issueId } = useParams();
  const [params] = useSearchParams();
  const { can } = useAuth();
  const parent = params.get('parent');
  const copy = params.get('copy');
  const sourceId = issueId || parent || copy;
  const issue = useIssueQuery(sourceId, { skip: !sourceId || !can('ISSUE_VIEW') });
  if (sourceId && !can('ISSUE_VIEW')) return <p>You do not have permission to view this issue.</p>;
  const context = issueId
    ? `edit:${issueId}`
    : parent
      ? `parent:${parent}`
      : copy
        ? `copy:${copy}`
        : 'create';
  const editor = () => (
    <DraftEditor
      key={context}
      context={context}
      initial={issueId ? issue.data : undefined}
      defaults={
        parent
          ? { projectId: issue.data.projectId, parentIssueId: issue.data.id, issueType: ({ EPIC: 'STORY', STORY: 'TASK', TASK: 'SUBTASK', BUG: 'SUBTASK' })[issue.data.issueType] || 'SUBTASK' }
          : copy
            ? copyIssueFields(issue.data)
            : {}
      }
    />
  );
  return (
    <>
      <PageHeader
        title={
          issueId
            ? 'Edit issue'
            : parent
              ? 'Create subtask'
              : copy
                ? 'Duplicate issue'
                : 'Create issue'
        }
        description={
          issueId ? issue.data?.issueKey : 'Review the details before creating your issue.'
        }
      />
      {sourceId ? <QueryState query={issue}>{issue.data && editor()}</QueryState> : editor()}
    </>
  );
}
