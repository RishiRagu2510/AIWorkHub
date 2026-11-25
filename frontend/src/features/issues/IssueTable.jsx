import { useState, lazy, Suspense } from 'react';
import Link from '../../components/PortalLink';
import { Badge, DataTable } from '../../components/common';
import UserIdentity from '../../components/UserIdentity';
import { useAuth } from '../auth/context';
import { preferenceKey, readPreference, writePreference } from '../../utils/preferences';
import { dateTime } from '../../utils/format';
const BulkIssueActions = lazy(() => import('./BulkIssueActions'));
const IssuePreview = lazy(() => import('./IssuePreview'));
const defaultOrder = [
  'issueKey',
  'summary',
  'statusCode',
  'priority',
  'assigneeUserId',
  'updatedAt',
];
export function IssueTable({ issues = [], manage = false }) {
  const { session, can } = useAuth();
  const key = preferenceKey(session.user, 'issue-columns');
  const [columns, setColumns] = useState(() => {
    const saved = readPreference(key);
    const order = Array.isArray(saved.order)
      ? [...new Set(saved.order.filter((item) => defaultOrder.includes(item)))]
      : [];
    return {
      order: [...order, ...defaultOrder.filter((item) => !order.includes(item))],
      hidden: Array.isArray(saved.hidden)
        ? saved.hidden.filter((item) => defaultOrder.includes(item) && item !== 'issueKey')
        : [],
    };
  });
  const [snapshots, setSnapshots] = useState({});
  const [selected, setSelected] = useState([]);
  const [busy, setBusy] = useState(false);
  const [preview, setPreview] = useState(null);
  const selectable =
    manage && (can('ISSUE_UPDATE') || can('ISSUE_TRANSITION') || can('ISSUE_ASSIGN'));
  const selectedIssues = issues
    .filter((issue) => selected.includes(issue.id))
    .map((issue) => snapshots[issue.id] || issue);
  const allSelected = issues.length > 0 && selectedIssues.length === issues.length;
  function customize(next) {
    setColumns(next);
    writePreference(key, next);
  }
  const definitions = {
    issueKey: {
      title: 'Key',
      render: (i) => (
        <Link className="issue-key" to={`/issues/${i.id}`}>
          {i.issueKey}
        </Link>
      ),
    },
    summary: {
      title: 'Summary',
      render: (i) => (
        <Link className="summary-link" to={`/issues/${i.id}`}>
          {i.summary}
        </Link>
      ),
    },
    statusCode: { title: 'Status', render: (i) => <Badge value={i.statusCode} /> },
    priority: { title: 'Priority', render: (i) => <Badge value={i.priority} /> },
    assigneeUserId: { title: 'Assignee', render: (i) => <UserIdentity id={i.assigneeUserId} /> },
    updatedAt: { title: 'Updated', render: (i) => dateTime(i.updatedAt) },
  };
  return (
    <>
      <details className="column-settings">
        <summary>Customize columns</summary>
        <ul>
          {columns.order.map((field, index) => (
            <li key={field}>
              <label>
                <input
                  name="visibleColumns"
                  type="checkbox"
                  checked={!columns.hidden.includes(field)}
                  disabled={field === 'issueKey'}
                  onChange={() =>
                    customize({
                      ...columns,
                      hidden: columns.hidden.includes(field)
                        ? columns.hidden.filter((item) => item !== field)
                        : [...columns.hidden, field],
                    })
                  }
                />
                {definitions[field].title}
              </label>
              <button
                aria-label={`Move ${definitions[field].title} column left`}
                disabled={index === 0}
                onClick={() => {
                  const order = [...columns.order];
                  [order[index - 1], order[index]] = [order[index], order[index - 1]];
                  customize({ ...columns, order });
                }}
              >
                ←
              </button>
              <button
                aria-label={`Move ${definitions[field].title} column right`}
                disabled={index === columns.order.length - 1}
                onClick={() => {
                  const order = [...columns.order];
                  [order[index + 1], order[index]] = [order[index], order[index + 1]];
                  customize({ ...columns, order });
                }}
              >
                →
              </button>
            </li>
          ))}
        </ul>
        <button onClick={() => customize({ order: defaultOrder, hidden: [] })}>
          Reset columns
        </button>
      </details>
      {selectable && selectedIssues.length > 0 && (
        <Suspense fallback={<p role="status">Loading bulk actions…</p>}>
          <BulkIssueActions
            issues={selectedIssues}
            onBusy={setBusy}
            onClear={() => setSelected([])}
            onReconciled={(issue) => {
              setSnapshots((current) => ({ ...current, [issue.id]: issue }));
              setSelected((ids) => [...ids]);
            }}
            onCompleted={(ids) =>
              setSelected((current) => current.filter((id) => !ids.includes(id)))
            }
          />
        </Suspense>
      )}
      <DataTable
        empty="No issues found"
        emptyVariant="issues"
        rows={issues}
        columns={[
          ...(selectable
            ? [
                {
                  key: 'select',
                  title: (
                    <input
                      name="selectAllIssues"
                      type="checkbox"
                      aria-label="Select all issues on this page"
                      checked={allSelected}
                      disabled={busy || !issues.length}
                      onChange={() => {
                        setSnapshots(Object.fromEntries(issues.map((issue) => [issue.id, issue])));
                        setSelected(allSelected ? [] : issues.map((issue) => issue.id));
                      }}
                    />
                  ),
                  render: (issue) => (
                    <input
                      name="selectedIssues"
                      type="checkbox"
                      aria-label={`Select ${issue.issueKey}`}
                      checked={selected.includes(issue.id)}
                      disabled={busy}
                      onChange={() => {
                        setSnapshots((current) => ({ ...current, [issue.id]: issue }));
                        setSelected((current) =>
                          current.includes(issue.id)
                            ? current.filter((id) => id !== issue.id)
                            : [...current, issue.id],
                        );
                      }}
                    />
                  ),
                },
              ]
            : []),
          ...columns.order
            .filter((field) => !columns.hidden.includes(field))
            .map((field) => ({ key: field, ...definitions[field] })),
          {
            key: 'preview',
            title: 'Preview',
            render: (issue) => (
              <button aria-label={`Preview ${issue.issueKey}`} onClick={() => setPreview(issue.id)}>
                Quick view
              </button>
            ),
          },
        ]}
      />
      {preview && (
        <Suspense fallback={<p role="status">Loading preview…</p>}>
          <IssuePreview id={preview} close={() => setPreview(null)} />
        </Suspense>
      )}
    </>
  );
}
