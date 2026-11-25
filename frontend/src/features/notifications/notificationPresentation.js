// How a notification reads on screen. The bell and the Notifications page both use this, so the two always agree on
// the wording, the colour and where a click goes.

const KINDS = {
  ISSUE_ASSIGNED: { label: 'Assigned to you', tone: 'info', symbol: '→' },
  ISSUE_COMMENT_ADDED: { label: 'New comment', tone: 'neutral', symbol: '💬' },
  ISSUE_MENTIONED: { label: 'Mentioned you', tone: 'info', symbol: '@' },
  ISSUE_TRANSITIONED: { label: 'Status changed', tone: 'neutral', symbol: '⇄' },
  ISSUE_UPDATED: { label: 'Issue updated', tone: 'neutral', symbol: '✎' },
  ISSUE_OVERDUE: { label: 'Overdue', tone: 'danger', symbol: '!' },
  ISSUE_DUE_SOON: { label: 'Due soon', tone: 'warn', symbol: '⏱' },
  ISSUE_STALE: { label: 'Needs an update', tone: 'warn', symbol: '⏱' },
  TIMESHEET_SUBMITTED: { label: 'Timesheet submitted for review', tone: 'info', symbol: '⏱' },
  TIMESHEET_APPROVED: { label: 'Timesheet approved', tone: 'success', symbol: '✓' },
  TIMESHEET_REJECTED: { label: 'Timesheet rejected', tone: 'danger', symbol: '✕' },
  EMPLOYEE_ONBOARDING_REQUESTED: { label: 'New employee onboarding request submitted', tone: 'info', symbol: '+' },
  EMPLOYEE_ONBOARDING_COMPLETED: { label: 'Employee onboarding completed', tone: 'success', symbol: '✓' },
  EMPLOYEE_REQUEST_CREATED: { label: 'Employee onboarding requested', tone: 'info', symbol: '+' },
  EMPLOYEE_REQUEST_APPROVED: { label: 'Employee request approved', tone: 'success', symbol: '✓' },
  EMPLOYEE_REQUEST_REJECTED: { label: 'Employee request rejected', tone: 'danger', symbol: '✕' },
  EMPLOYEE_REQUEST_COMPLETED: { label: 'Employee onboarding completed', tone: 'success', symbol: '✓' },
};

const STATUS_NAMES = { TODO: 'To Do', IN_PROGRESS: 'In Progress', IN_REVIEW: 'In Review', DONE: 'Done', BLOCKED: 'Blocked' };

export function statusName(code) {
  if (!code) return '';
  return STATUS_NAMES[code] || String(code).toLowerCase().replace(/_/g, ' ').replace(/^\w/, (c) => c.toUpperCase());
}

/** 'FLP360 ONGOING FEATURES AND IMPROVEMENTS-1' (an old key that holds a whole project name) reads as 'FLP360-1'. */
export function shortIssueKey(key) {
  if (!key) return '';
  const match = /^(\S+)\s.*-(\d+)$/.exec(key.trim());
  return match ? `${match[1]}-${match[2]}` : key;
}

export function portalOf(item) {
  if (item.kind?.startsWith('TIMESHEET') || item.timesheetPeriodId) return 'chronos';
  if (item.kind?.startsWith('EMPLOYEE')) return 'persona';
  return 'gridhub';
}

export const PORTAL_NAMES = { gridhub: 'GridHub', chronos: 'Chronos', persona: 'Persona' };

export function describeNotification(item) {
  const known = KINDS[item.kind];
  const fallback = { label: 'Issue activity', tone: 'neutral', symbol: '•' };
  const base = known || fallback;
  // A status change says what it changed to, so the reader does not have to open the issue to find out.
  const label =
    item.kind === 'ISSUE_TRANSITIONED' && item.issueStatus ? `Status changed to ${statusName(item.issueStatus)}` : base.label;
  return { ...base, label, portal: portalOf(item) };
}

export function notificationTitle(item) {
  if (item.timesheetPeriodId) {
    return item.timesheetPeriodStart ? `Timesheet · week of ${weekLabel(item.timesheetPeriodStart)}` : 'Timesheet';
  }
  const key = shortIssueKey(item.issueKey);
  return `${key}${key && item.issueSummary ? ' · ' : ''}${item.issueSummary || (key ? '' : 'View details')}`;
}

function weekLabel(value) {
  const date = new Date(`${String(value).slice(0, 10)}T00:00:00`);
  return Number.isNaN(date.getTime()) ? String(value) : new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' }).format(date);
}

export function notificationTarget(item) {
  if (item.kind === 'EMPLOYEE_ONBOARDING_REQUESTED' || item.kind === 'EMPLOYEE_ONBOARDING_COMPLETED') {
    return `/employees/onboarding/${item.issueId}`;
  }
  if (item.timesheetPeriodId) {
    return `/timesheets/${item.kind === 'TIMESHEET_SUBMITTED' ? 'team' : 'my'}?${new URLSearchParams({
      ...(item.timesheetPeriodStart ? { from: item.timesheetPeriodStart } : {}),
      ...(item.kind === 'TIMESHEET_SUBMITTED' && item.timesheetUserId ? { userId: item.timesheetUserId } : {}),
    })}`;
  }
  return `/issues/${item.issueId}`;
}

/** 'Just now', '5 min ago', '3 h ago', 'Yesterday', then the date. */
export function timeAgo(value, now = Date.now()) {
  const time = new Date(value).getTime();
  if (!value || Number.isNaN(time)) return '';
  const minutes = Math.floor((now - time) / 60000);
  if (minutes < 1) return 'Just now';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  if (hours < 48) return 'Yesterday';
  return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' }).format(time);
}
