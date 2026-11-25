import VersionConflict from '../../components/VersionConflict';
import { useUnsavedChanges } from '../../components/unsavedContext';
import { useRef, useState } from 'react';
import { useAssignIssueMutation } from '../../services/api/api';
import { useNotice } from '../../components/noticeContext';
import IssueAssignee from './IssueAssignee';
import { assignmentError } from './assignment';
export default function IssueAssignment({ issue }) {
  const { markDirty, clearDirty } = useUnsavedChanges();
  const [clearing, setClearing] = useState(false);
  const [value, setValue] = useState(null);
  const [version, setVersion] = useState(issue.version);
  const [assign, state] = useAssignIssueMutation();
  const lock = useRef(false);
  const notice = useNotice();
  async function submit(event) {
    event.preventDefault();
    const list = Array.isArray(value) ? value : value ? [value] : [];
    if ((!list.length && !clearing) || lock.current || state.error?.status === 412) return;
    lock.current = true;
    try {
      await assign({
        id: String(issue.id),
        ...(clearing
          ? { clearAssignee: true }
          : list.length > 1
            ? {
                assigneeUserId: list[0]?.id,
                assigneeUserIds: list.map((a) => a.id),
              }
            : {
                assigneeUserId: list[0]?.id,
              }),
        version,
      }).unwrap();
      clearDirty();
      setValue(null);
      setClearing(false);
      notice('Issue assignment updated.');
    } catch {
      /* Preserve the selection and show a safe error. */
    } finally {
      lock.current = false;
    }
  }
  return (
    <form className="assignment" onSubmit={submit}>
      <IssueAssignee
        projectIds={[String(issue.projectId)]}
        value={value}
        onChange={(person) => {
          if (!value) setVersion(issue.version);
          setValue(person);
          if (person) markDirty();
          else clearDirty();
        }}
        disabled={state.isLoading || clearing}
      />
      {issue.assigneeUserId != null && (
        <label className="unassign-option">
          <input
            name="clearAssignee"
            type="checkbox"
            checked={clearing}
            disabled={state.isLoading}
            onChange={(e) => {
              if (!value && !clearing) setVersion(issue.version);
              setClearing(e.target.checked);
              if (e.target.checked || value) markDirty();
              else clearDirty();
            }}
          />
          Remove the current assignee
        </label>
      )}
      <VersionConflict
        error={assignmentError(state.error)}
        path={`/api/issues/${issue.id}`}
        onReviewed={(current) => {
          setVersion(current.version);
          state.reset();
        }}
      />
      <div className="assignment-footer">
        <button
          className="primary"
          disabled={((!value || (Array.isArray(value) && !value.length)) && !clearing) || state.isLoading || state.error?.status === 412}
        >
          {state.isLoading ? 'Saving…' : clearing ? 'Unassign issue' : 'Assign issue'}
        </button>
      </div>
    </form>
  );
}
