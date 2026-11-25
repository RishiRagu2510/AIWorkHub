import { api } from './api';
const paged = (data) => ({ ...data, totalPages: Math.ceil(data.totalCount / data.pageSize) });
export const collaborationApi = api
  .enhanceEndpoints({ addTagTypes: ['Workflow', 'Boards', 'Watchers', 'Activity'] })
  .injectEndpoints({
    endpoints: (b) => ({
      workflows: b.query({
        async queryFn(ids, _, __, baseQuery) {
          const entries = await Promise.all(
            ids.map(async (id) => [
              String(id),
              await baseQuery({ path: `/api/projects/${id}/workflow` }),
            ]),
          );
          const failure = entries.find(([, result]) => result.error);
          return failure
            ? { error: failure[1].error }
            : { data: Object.fromEntries(entries.map(([id, result]) => [id, result.data])) };
        },
        providesTags: ['Workflow'],
      }),
      workflow: b.query({
        query: (id) => ({ path: `/api/projects/${id}/workflow` }),
        providesTags: ['Workflow'],
      }),
      saveWorkflow: b.mutation({
        query: ({ id, ...body }) => ({ path: `/api/projects/${id}/workflow`, method: 'PUT', body }),
        invalidatesTags: (_, e) => (e ? [] : ['Workflow']),
      }),
      boards: b.query({
        query: (projectId) => ({ path: '/api/boards', params: { projectId } }),
        providesTags: ['Boards'],
      }),
      board: b.query({
        query: ({ id, page = 1 }) => ({ path: `/api/boards/${id}`, params: { page } }),
        transformResponse: paged,
        providesTags: ['Boards', 'Issues'],
      }),
      createBoard: b.mutation({
        query: (body) => ({ path: '/api/boards', method: 'POST', body }),
        invalidatesTags: (_, e) => (e ? [] : ['Boards']),
      }),
      watchers: b.query({
        query: (id) => ({ path: `/api/issues/${id}/watchers` }),
        providesTags: (_, __, id) => [{ type: 'Watchers', id }],
      }),
      watch: b.mutation({
        query: ({ id, enabled }) => ({
          path: `/api/issues/${id}/watchers/me`,
          method: enabled ? 'PUT' : 'DELETE',
        }),
        invalidatesTags: (_, e, { id }) => (e ? [] : [{ type: 'Watchers', id }]),
      }),
      issueActivity: b.query({
        query: ({ id, page = 1 }) => ({ path: `/api/issues/${id}/activity`, params: { page } }),
        transformResponse: paged,
        providesTags: ['Activity'],
      }),
      activity: b.query({
        query: (page) => ({ path: '/api/activity', params: { page } }),
        transformResponse: paged,
        providesTags: ['Activity'],
      }),
    }),
  });
export const {
  useWorkflowQuery,
  useWorkflowsQuery,
  useSaveWorkflowMutation,
  useBoardsQuery,
  useBoardQuery,
  useCreateBoardMutation,
  useWatchersQuery,
  useWatchMutation,
  useIssueActivityQuery,
  useActivityQuery,
} = collaborationApi;
export function allowedStatuses(workflow, current, can = () => true) {
  if (!workflow) return [];
  return workflow.isDefault
    ? workflow.definition.statuses.filter((s) => s !== current)
    : workflow.definition.transitions
        .filter((t) => t.from === current && (!t.permission || can(t.permission)))
        .map((t) => t.to);
}
export function normalNext(workflow, current) {
  return workflow?.isDefault
    ? workflow.definition.transitions.find((t) => t.from === current)?.to
    : undefined;
}
