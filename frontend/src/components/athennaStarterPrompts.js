// The cards on Athenna's empty chat screen. What a person sees depends on what they are allowed to do and on their role:
// a manager is offered the sprint health check and pending approvals, HR the employee export, a developer their own day.
// A card is never offered for something the person cannot do, so the first thing they click always works.

const CARDS = {
  today: {
    system: 'GridHub', badgeClass: 'tag-gridhub', icon: '🎯', title: 'What to Work on Today',
    desc: 'Overdue, due soon and in-progress issues, in the order to tackle them',
    prompt: 'What should I work on today?',
    needs: ['ISSUE_VIEW'],
  },
  sprint: {
    system: 'GridHub', badgeClass: 'tag-gridhub', icon: '📈', title: 'Sprint Health',
    desc: 'Days left, points done, overdue work and who is overloaded',
    prompt: 'How is the current sprint going?',
    needs: ['ISSUE_VIEW'],
  },
  issuesReport: {
    system: 'GridHub', badgeClass: 'tag-gridhub', icon: '📊', title: 'Issues Report in Excel',
    desc: 'Download your open issues with status, priority and due dates',
    prompt: 'Export my open issues to Excel',
    needs: ['ISSUE_VIEW'],
  },
  timesheet: {
    system: 'Chronos', badgeClass: 'tag-chronos', icon: '⏱️', title: 'Timesheet & Worklogs',
    desc: "Check your logged hours and today's worklog breakdown",
    prompt: 'Show my logged hours and timesheet summary for today in Chronos',
    needs: ['TIMESHEET_VIEW'],
  },
  timeReport: {
    system: 'Chronos', badgeClass: 'tag-chronos', icon: '🧾', title: 'Time Report in Excel',
    desc: 'Download this month\'s logged hours, day by day',
    prompt: 'Export my time report for this month to Excel',
    needs: ['WORKLOG_VIEW'],
  },
  timesheetApprovals: {
    system: 'Chronos', badgeClass: 'tag-chronos', icon: '✅', title: 'Timesheets to Review',
    desc: 'Weekly timesheets your team has submitted and are waiting for you',
    prompt: 'Show the timesheets waiting for my approval',
    needs: ['TIMESHEET_APPROVE'],
  },
  leave: {
    system: 'Persona', badgeClass: 'tag-persona', icon: '🌴', title: 'Leave & Absences',
    desc: 'Inspect leave balance, company holidays, or team availability',
    prompt: 'Show my leave balance and upcoming team absences in Persona',
    needs: ['EMPLOYEE_VIEW'],
  },
  leaveApprovals: {
    system: 'Persona', badgeClass: 'tag-persona', icon: '📥', title: 'Leave Requests to Decide',
    desc: 'Pending leave and work-from-home requests waiting for your decision',
    prompt: 'Show the pending leave requests I need to approve',
    needs: ['EMPLOYEE_VIEW'],
  },
  profile: {
    system: 'Persona', badgeClass: 'tag-persona', icon: '🪪', title: 'Keep My Profile Current',
    desc: 'Add a certification, a language or a skill, or update your contact details',
    prompt: 'I want to add a certification to my profile',
    needs: ['EMPLOYEE_UPDATE_SELF'],
  },
  allEmployees: {
    system: 'Persona', badgeClass: 'tag-persona', icon: '👥', title: 'All Employees in Excel',
    desc: 'Every employee with skills, experience, education and current pay',
    prompt: 'Give me the details of all employees',
    needs: ['EMPLOYEE_VIEW_SENSITIVE'],
  },
  newIssue: {
    system: 'Workspace', badgeClass: 'tag-workspace', icon: '🚀', title: 'Create Issue or Task',
    desc: 'Report a new task or defect assigned to team members',
    prompt: 'I want to create a new issue',
    needs: ['ISSUE_CREATE'],
  },
};

// What each kind of person is offered first. The list is longer than needed on purpose: a card the person may not use
// is skipped and the next one takes its place.
const ORDER = {
  hr: ['allEmployees', 'leaveApprovals', 'leave', 'profile', 'timesheet', 'today'],
  admin: ['sprint', 'allEmployees', 'issuesReport', 'leaveApprovals', 'timesheetApprovals', 'today'],
  manager: ['sprint', 'timesheetApprovals', 'leaveApprovals', 'today', 'issuesReport', 'timesheet'],
  developer: ['today', 'timesheet', 'leave', 'newIssue', 'timeReport', 'profile'],
};

/** Which group a role code belongs to. Unknown codes are treated like a developer: their own work first. */
export function roleGroup(role) {
  const code = String(role || '').toUpperCase();
  if (code === 'ADMIN' || code === 'SUPER_ADMIN') return 'admin';
  if (code === 'HR' || code === 'HR_ADMIN' || code.startsWith('HUMAN_RESOURCE')) return 'hr';
  if (code.includes('MANAGER') || code.includes('LEAD')) return 'manager';
  return 'developer';
}

/**
 * The cards to show. `can(permission)` says what the person may do; an admin can do everything it answers yes to.
 * Always returns up to `limit` cards (default 4, which fills the two-column grid).
 */
export function starterPromptsFor({ role, can = () => true, limit = 4 } = {}) {
  const allowed = (card) => card.needs.every((permission) => can(permission));
  const order = [...ORDER[roleGroup(role)], ...Object.keys(CARDS)];
  const seen = new Set();
  const cards = [];
  for (const id of order) {
    if (seen.has(id)) continue;
    seen.add(id);
    if (allowed(CARDS[id])) cards.push({ id, ...CARDS[id] });
    if (cards.length === limit) break;
  }
  return cards;
}
