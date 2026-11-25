import VersionConflict from '../../components/VersionConflict';
import { assignmentError } from './assignment';
import { useEffect, useRef, useState } from 'react';
import { useAuth } from '../auth/context';
import {
  useUpdateIssueMutation,
  useAssignIssueMutation,
  useTransitionIssueMutation,
} from '../../services/api/api';
import { ConfirmDialog, SelectField } from '../../components/common';
import IssueAssignee from './IssueAssignee';
import { priorities } from './contracts';
import { useWorkflowsQuery, allowedStatuses, normalNext } from '../../services/api/collaboration';
import { label } from '../../utils/format';
export default function BulkIssueActions({ issues, onCompleted, onBusy, onReconciled, onClear }) {
  const { can } = useAuth();
  const [mode, setMode] = useState('');
  const [value, setValue] = useState('');
  const projectIds = [...new Set(issues.map((issue) => String(issue.projectId)))].sort();
  const scope = projectIds.join(',');
  const workflows = useWorkflowsQuery(projectIds, {
    skip: !projectIds.length || !can('ISSUE_TRANSITION'),
  });
  const definitions = workflows.currentData || {};
  const statuses = issues.length
    ? allowedStatuses(definitions[issues[0].projectId], issues[0].statusCode, can).filter(
        (status) =>
          issues.every((issue) =>
            allowedStatuses(definitions[issue.projectId], issue.statusCode, can).includes(status),
          ),
      )
    : [];
  const [selection, setSelection] = useState(null);
  const assignee = selection?.scope === scope ? selection.person : null;
  const setAssignee = (person) => {
    const primary = Array.isArray(person) ? person[0] || null : person;
    setSelection({ scope, person: primary, list: Array.isArray(person) ? person : person ? [person] : [] });
  };
  const [pending, setPending] = useState(null);
  const [running, setRunning] = useState(false);
  const [results, setResults] = useState([]);
  const lock = useRef(false);
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  const [update] = useUpdateIssueMutation();
  const [assign] = useAssignIssueMutation();
  const [transition] = useTransitionIssueMutation();
  const actions = [
    ...(can('ISSUE_UPDATE') ? [{ value: 'priority', label: 'Change priority' }] : []),
    ...(can('ISSUE_TRANSITION') ? [{ value: 'status', label: 'Change status' }] : []),
    ...(can('ISSUE_ASSIGN') ? [{ value: 'assignee', label: 'Assign issues' }] : []),
  ];
  async function apply() {
    if (lock.current || !pending || !actions.some((action) => action.value === pending.mode))
      return;
    lock.current = true;
    setRunning(true);
    onBusy(true);
    const outcomes = [];
    for (const issue of pending.issues) {
      if (!mounted.current) break;
      try {
        const id = String(issue.id);
        await (
          pending.mode === 'priority'
            ? update({ id, version: issue.version, body: { priority: pending.value } })
            : pending.mode === 'status'
              ? transition({
                  id,
                  version: issue.version,
                  statusCode: pending.value,
                  confirmSkip: true,
                })
              : assign({ id, version: issue.version, assigneeUserId: pending.assignee.id })
        ).unwrap();
        outcomes.push({ id: issue.id, key: issue.issueKey, success: true });
      } catch (error) {
        outcomes.push({
          id: issue.id,
          key: issue.issueKey,
          success: false,
          error,
          message:
            (pending.mode === 'assignee' ? assignmentError(error).message : error.message) ||
            'Update failed. Try again.',
        });
        if (error.status === 401 || error.status === 403) break;
      }
    }
    lock.current = false;
    if (mounted.current) {
      setRunning(false);
      onBusy(false);
      setPending(null);
      setResults(outcomes);
      onCompleted(outcomes.filter((result) => result.success).map((result) => result.id));
    }
  }
  return (
    <div className="bulk-actions">
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '8px' }}>
        <p style={{ margin: 0 }}>
          <strong>{issues.length}</strong> {issues.length === 1 ? 'issue' : 'issues'} selected
        </p>
        {onClear && (
          <button type="button" onClick={onClear} style={{ minHeight: '30px', padding: '4px 10px', fontSize: '12px' }}>
            Clear selection
          </button>
        )}
      </div>
      <div className="bulk-controls">
        <SelectField
          label="Bulk action"
          empty="Choose an action"
          values={actions}
          value={mode}
          disabled={running}
          onChange={(event) => {
            setMode(event.target.value);
            setValue('');
            setResults([]);
          }}
        />
        {mode !== 'assignee' && mode && (
          <SelectField
            label={mode === 'status' ? 'New status' : 'New priority'}
            empty="Choose a value"
            values={mode === 'status' ? statuses : priorities}
            value={value}
            disabled={running}
            onChange={(event) => setValue(event.target.value)}
          />
        )}
        <button
          className="primary"
          disabled={
            running || !issues.length || !mode || (mode === 'assignee' ? !assignee : !value)
          }
          onClick={() => setPending({ issues: [...issues], mode, value, assignee })}
        >
          Review changes
        </button>
      </div>
      {mode === 'assignee' && (
        <IssueAssignee
          key={scope}
          projectIds={projectIds}
          value={assignee}
          onChange={setAssignee}
          disabled={running}
        />
      )}
      {!!results.length && (
        <div role="status">
          <p>
            {results.filter((result) => result.success).length} updated. Unsuccessful or unprocessed
            issues remain selected.
          </p>
          <ul>
            {results
              .filter((result) => !result.success)
              .map((result) => (
                <li key={result.id}>
                  {result.key}: {result.message}
                  {result.error?.status === 412 && (
                    <VersionConflict
                      error={result.error}
                      path={`/api/issues/${result.id}`}
                      onReviewed={(current) => {
                        onReconciled?.(current);
                        setResults((items) => items.filter((item) => item.id !== result.id));
                      }}
                    />
                  )}
                </li>
              ))}
          </ul>
        </div>
      )}
      {pending && (
        <ConfirmDialog
          title="Confirm bulk update"
          confirmLabel={`Update ${pending.issues.length} issues`}
          busyLabel="Updating issues…"
          confirmClass="primary"
          busy={running}
          close={() => setPending(null)}
          confirm={apply}
        >
          <p>
            {pending.mode === 'assignee'
              ? `Assign to ${pending.assignee.displayName || pending.assignee.email}`
              : `Set ${pending.mode} to ${label(pending.value)}`}{' '}
            for {pending.issues.length} issues?
          </p>
          {pending.mode === 'status' &&
            pending.issues.some(
              (issue) =>
                definitions[issue.projectId]?.isDefault &&
                pending.value !== normalNext(definitions[issue.projectId], issue.statusCode),
            ) && (
              <p>
                This includes moves outside the normal next workflow step. Confirm to skip those
                steps.
              </p>
            )}
          <ul>
            {pending.issues.map((issue) => (
              <li key={issue.id}>
                {issue.issueKey} — {issue.summary}
              </li>
            ))}
          </ul>
        </ConfirmDialog>
      )}
    </div>
  );
}
