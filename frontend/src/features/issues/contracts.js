// Authoritative: Jira.Domain/Enums, IssueService.ValidPriorities and 002_issue_schema.sql.
export const issueTypes = ['EPIC', 'STORY', 'TASK', 'BUG', 'SUBTASK'];
export const priorities = ['LOWEST', 'LOW', 'MEDIUM', 'HIGH', 'HIGHEST'];
export const statuses = [
  'TODO',
  'IN_PROGRESS',
  'CODE_REVIEW',
  'TESTING',
  'BLOCKED',
  'DONE',
  'CANCELLED',
];
export const sortFields = [
  'issueNumber',
  'issueKey',
  'summary',
  'priority',
  'status',
  'createdAt',
  'updatedAt',
];
export function issuePayload(values, editing = false) {
  const result = {
    summary: values.summary.trim(),
    description: values.description,
    priority: values.priority,
  };
  if (!editing)
    Object.assign(result, {
      projectId: Number(values.projectId),
      issueType: values.issueType,
      statusCode: 'TODO',
    });
  for (const key of ['parentIssueId', 'componentId', 'storyPoints'])
    if (values[key] !== '' && values[key] != null) result[key] = Number(values[key]);
  if (values.labels !== undefined)
    result.labels = String(values.labels)
      .split(',')
      .map((x) => x.trim())
      .filter(Boolean);
  for (const key of ['fixVersionIds', 'affectsVersionIds'])
    if (values[key] !== undefined)
      result[key] = String(values[key]).split(',').filter(Boolean).map(Number);
  for (const key of ['originalEstimateMinutes', 'remainingEstimateMinutes'])
    if (values[key] !== undefined && values[key] !== '') result[key] = Number(values[key]);
  if (values.dueDate) result.dueDate = values.dueDate;
  if (editing)
    for (const [flag, field] of Object.entries({
      clearParentIssue: 'parentIssueId',
      clearComponent: 'componentId',
      clearStoryPoints: 'storyPoints',
      clearDueDate: 'dueDate',
    })) {
      if (values[flag] === 'on' || values[flag] === true) {
        result[flag] = true;
        delete result[field];
      }
    }
  return result;
}

export const workflowSteps = ['TODO', 'IN_PROGRESS', 'CODE_REVIEW', 'TESTING', 'DONE'];
export function nextWorkflowStatus(status) {
  const index = workflowSteps.indexOf(status);
  return index >= 0 ? workflowSteps[index + 1] : undefined;
}
