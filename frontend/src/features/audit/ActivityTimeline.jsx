import UserIdentity from '../../components/UserIdentity';
import { EmptyState } from '../../components/common';
import { dateTime, label } from '../../utils/format';
function parse(value) {
  try {
    const parsed = typeof value === 'string' ? JSON.parse(value) : value;
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : {};
  } catch {
    return {};
  }
}
const fields = { statusCode: 'Status', assigneeUserId: 'Assignee', priority: 'Priority' };
function display(field, value) {
  if (field === 'assigneeUserId') return <UserIdentity id={value} />;
  return value == null ? 'Not set' : label(value);
}
export default function ActivityTimeline({ items = [] }) {
  if (!items.length)
    return (
      <EmptyState title="No issue activity yet">
        Status, assignment, and priority changes will appear here.
      </EmptyState>
    );
  return (
    <ol className="issue-timeline" aria-label="Issue activity timeline">
      {items.map((event) => {
        const before = parse(event.oldValues);
        const after = parse(event.newValues);
        const changed = Object.keys(fields).filter(
          (field) => Object.hasOwn(after, field) && before[field] !== after[field],
        );

        return (
          <li key={event.id}>
            <div>
              <strong>
                {event.actorUserId ? <UserIdentity id={event.actorUserId} /> : 'System'}
              </strong>{' '}
              · {label(event.action)}
            </div>
            <time dateTime={event.occurredAt}>{dateTime(event.occurredAt)}</time>
            {changed.map((field) => (
              <p key={field}>
                {fields[field]}: <span>{display(field, before[field])}</span> →{' '}
                <strong>{display(field, after[field])}</strong>
              </p>
            ))}
          </li>
        );
      })}
    </ol>
  );
}
