import { api } from './api';
const paged = (data) => ({ ...data, totalPages: Math.ceil(data.totalCount / data.pageSize) });
export const planningApi = api.enhanceEndpoints({ addTagTypes: ['Planning'] }).injectEndpoints({
  endpoints: (b) => ({
    projectCatalog: b.query({
      query: ({ projectId, kind }) => ({ path: `/api/projects/${projectId}/${kind}` }),
      providesTags: ['Planning'],
    }),
    updateCatalog: b.mutation({
      query: ({ projectId, kind, id, version, body }) => ({
        path: `/api/projects/${projectId}/${kind}/${id}`,
        method: 'PUT',
        version,
        body,
      }),
      invalidatesTags: (_, e) => (e ? [] : ['Planning', 'Audit']),
    }),
    createCatalog: b.mutation({
      query: ({ projectId, kind, body }) => ({
        path: `/api/projects/${projectId}/${kind}`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_, e) => (e ? [] : ['Planning', 'Audit']),
    }),
    sprints: b.query({
      query: (boardId) => ({ path: `/api/boards/${boardId}/sprints` }),
      providesTags: ['Planning'],
    }),
    createSprint: b.mutation({
      query: ({ boardId, body }) => ({
        path: `/api/boards/${boardId}/sprints`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_, e) => (e ? [] : ['Planning', 'Audit']),
    }),
    sprintAction: b.mutation({
      query: ({ id, action, version }) => ({
        path: `/api/sprints/${id}/${action}`,
        method: 'POST',
        version,
      }),
      invalidatesTags: (_, e) => (e ? [] : ['Planning', 'Issues', 'Activity', 'Audit']),
    }),
    backlog: b.query({
      query: ({ boardId, ...params }) => ({ path: `/api/boards/${boardId}/backlog`, params }),
      transformResponse: paged,
      providesTags: ['Planning', 'Issues'],
    }),
    placeIssue: b.mutation({
      query: ({ boardId, issueId, version, ...body }) => ({
        path: `/api/boards/${boardId}/issues/${issueId}/placement`,
        method: 'PUT',
        version,
        body,
      }),
      invalidatesTags: (_, e) => (e ? [] : ['Planning', 'Issues', 'Issue', 'Activity', 'Audit']),
    }),
  }),
});
export const {
  useProjectCatalogQuery,
  useCreateCatalogMutation,
  useUpdateCatalogMutation,
  useSprintsQuery,
  useCreateSprintMutation,
  useSprintActionMutation,
  useBacklogQuery,
  usePlaceIssueMutation,
} = planningApi;
