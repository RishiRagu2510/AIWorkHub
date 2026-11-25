import { useEffect, useState } from 'react';
import { request } from '../services/api/client';
import { ErrorState } from './common';
const titles = {
  name: 'Name',
  description: 'Description',
  projectType: 'Project type',
  status: 'Project status',
  parentProjectId: 'Parent project ID',
  summary: 'Summary',
  priority: 'Priority',
  statusCode: 'Issue status',
  assigneeUserId: 'Assignee ID',
  storyPoints: 'Story points',
  dueDate: 'Due date',
};
const fields = [
  'name',
  'description',
  'projectType',
  'status',
  'parentProjectId',
  'summary',
  'priority',
  'statusCode',
  'assigneeUserId',
  'storyPoints',
  'dueDate',
];
export default function VersionConflict({ error, path, onReviewed }) {
  const [result, setResult] = useState(null);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (error?.status !== 412) return;
    const controller = new AbortController();
    request(path, { signal: controller.signal })
      .then((data) => {
        if (!controller.signal.aborted) setResult({ data });
      })
      .catch((failure) => {
        if (!controller.signal.aborted) setResult({ error: failure });
      });
    return () => controller.abort();
  }, [error?.status, path, attempt]);
  if (error?.status !== 412) return <ErrorState error={error} />;
  const current = result?.data;
  return (
    <section className="version-conflict" aria-label="Resolve conflicting changes">
      <h3>Another user saved changes</h3>
      <p>
        Your draft is unchanged. Compare it with the current saved values below. Review and adjust
        your draft before choosing to continue.
      </p>
      {!result && <p role="status">Loading current saved values…</p>}
      <ErrorState error={result?.error} />
      {current && (
        <dl>
          {fields
            .filter((field) => Object.hasOwn(current, field))
            .map((field) => (
              <div key={field}>
                <dt>{titles[field]}</dt>
                <dd>{current[field] == null ? 'Not set' : String(current[field])}</dd>
              </div>
            ))}
        </dl>
      )}
      <div className="actions">
        <button
          type="button"
          disabled={!result}
          onClick={() => {
            setResult(null);
            setAttempt((value) => value + 1);
          }}
        >
          Reload current values
        </button>
        <button
          type="button"
          disabled={!current?.version}
          onClick={() => {
            setResult(null);
            onReviewed(current);
          }}
        >
          I reviewed these changes — keep my draft
        </button>
      </div>
      <p className="hint">
        This does not save or retry. Your next save uses the version shown in this comparison.
      </p>
    </section>
  );
}
