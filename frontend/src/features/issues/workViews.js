export async function loadScopedIssues(params, baseQuery, signal) {
  const items = new Map();
  for (let page = 1; ; page++) {
    if (signal?.aborted) return { error: { status: 'CANCELLED', message: 'Loading cancelled.' } };
    const result = await baseQuery({
      path: '/api/issues/search',
      params: { ...params, Page: page, PageSize: 100 },
    });
    if (result.error) return result;
    for (const item of result.data.items) items.set(String(item.id), item);
    if (page >= result.data.totalPages || !result.data.items.length) break;
  }
  return { data: [...items.values()] };
}
export function groupMyWork(issues, now = new Date()) {
  const dateKey = (date) =>
    `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
  const today = dateKey(now);
  const upcoming = new Date(now);
  upcoming.setDate(upcoming.getDate() + 7);
  const end = dateKey(upcoming);
  const groups = { Overdue: [], 'Due soon': [], 'In progress': [], 'Other open work': [] };
  for (const issue of issues) {
    if (['DONE', 'CANCELLED'].includes(issue.statusCode)) continue;
    const due = issue.dueDate?.slice(0, 10);
    const group =
      due && due < today
        ? 'Overdue'
        : due && due <= end
          ? 'Due soon'
          : ['IN_PROGRESS', 'CODE_REVIEW', 'TESTING'].includes(issue.statusCode)
            ? 'In progress'
            : 'Other open work';
    groups[group].push(issue);
  }
  return groups;
}
export function copyIssueFields(issue) {
  return {
    projectId: issue.projectId,
    summary: `Copy of ${issue.summary}`.slice(0, 500),
    description: issue.description || '',
    priority: issue.priority,
    issueType: issue.issueType,
    storyPoints: issue.storyPoints ?? '',
    componentId: issue.componentId ?? '',
  };
}
