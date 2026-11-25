import { loadScopedIssues } from '../../features/issues/workViews';
import { loadAssignees } from '../../features/issues/assignment';
import { createApi } from '@reduxjs/toolkit/query/react';
import { request } from './client';
import { statuses } from '../../features/issues/contracts';
export const api = createApi({
  reducerPath: 'api',
  baseQuery: async ({ path, ...options }, { signal }) => {
    try {
      return { data: await request(path, { ...options, signal }) };
    } catch (error) {
      return {
        error: {
          status: error.status || 'NETWORK',
          ...(error.code ? { code: error.code } : {}),
          message: error.message || 'Request cancelled.',
        },
      };
    }
  },
  tagTypes: [
    'AgentMemory',
    'AgentKnowledge',
    'Timesheets',
    'Notifications',
    'Issues',
    'Issue',
    'Comments',
    'Attachments',
    'Workflow',
    'Watchers',
    'Boards',
    'Activity',
    'Audit',
    'ProjectMembers',
    'EffectiveMembers',
    'Projects',
    'AgentTasks',
  ],
  endpoints: (build) => ({
    deleteAttachment: build.mutation({
      query: ({ id, attachmentId }) => ({
        path: `/api/issues/${id}/attachments/${attachmentId}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_, error, { id }) =>
        error ? [] : [{ type: 'Attachments', id }, 'Activity'],
    }),
    notifications: build.query({
      query: (params) => ({ path: '/api/notifications', params }),
      providesTags: ['Notifications'],
    }),
    unreadNotifications: build.query({
      query: () => ({ path: '/api/notifications/unread-count' }),
      providesTags: ['Notifications'],
    }),
    readNotification: build.mutation({
      query: (id) => ({ path: `/api/notifications/${id}/read`, method: 'PUT' }),
      invalidatesTags: (_, error) => (!error || error.status === 404 ? ['Notifications'] : []),
    }),
    readAllNotifications: build.mutation({
      query: () => ({ path: '/api/notifications/read-all', method: 'POST' }),
      invalidatesTags: (_, error) => (error ? [] : ['Notifications']),
    }),
    user: build.query({ query: (id) => ({ path: `/api/users/${id}` }) }),
    developers: build.query({
      query: (page = 1) => ({
        path: '/api/users',
        params: { role: 'DEVELOPER', page, pageSize: 100 },
      }),
    }),
    effectiveMembers: build.query({
      query: (id) => ({ path: `/api/projects/${id}/members/effective` }),
      providesTags: ['EffectiveMembers'],
    }),
    projectAssignees: build.query({
      queryFn: (args, _, __, baseQuery) => loadAssignees(args, baseQuery),
      providesTags: ['EffectiveMembers'],
    }),
    projectMembers: build.query({
      query: (id) => ({ path: `/api/projects/${id}/members` }),
      providesTags: (_, __, id) => [{ type: 'ProjectMembers', id }],
    }),
    addProjectMember: build.mutation({
      query: ({ projectId, userId }) => ({
        path: `/api/projects/${projectId}/members`,
        method: 'POST',
        body: { userId },
      }),
      invalidatesTags: (_, __, { projectId }) => [
        { type: 'ProjectMembers', id: projectId },
        'EffectiveMembers',
        'Audit',
      ],
    }),
    removeProjectMember: build.mutation({
      query: ({ projectId, userId }) => ({
        path: `/api/projects/${projectId}/members/${userId}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_, __, { projectId }) => [
        { type: 'ProjectMembers', id: projectId },
        'EffectiveMembers',
        'Audit',
      ],
    }),
    createAccount: build.mutation({
      query: (body) => ({ path: '/api/projects/accounts', method: 'POST', body }),
      invalidatesTags: (_, error) => (error ? [] : ['Projects']),
    }),
    updateAccount: build.mutation({
      query: ({ id, version, ...body }) => ({
        path: `/api/projects/accounts/${id}`,
        method: 'PUT',
        version,
        body,
      }),
      invalidatesTags: (_, error) => (error ? [] : ['Projects']),
    }),
    deleteAccount: build.mutation({
      query: ({ id, version }) => ({
        path: `/api/projects/accounts/${id}`,
        method: 'DELETE',
        version,
      }),
      invalidatesTags: (_, error) => (error ? [] : ['Projects']),
    }),
    accounts: build.query({
      query: () => ({ path: '/api/projects/accounts' }),
      providesTags: ['Projects'],
    }),
    projects: build.query({ query: () => ({ path: '/api/projects' }), providesTags: ['Projects'] }),
    project: build.query({
      query: (id) => ({ path: `/api/projects/${id}` }),
      providesTags: ['Projects'],
    }),
    updateProject: build.mutation({
      query: ({ id, version, body }) => ({
        path: `/api/projects/${id}`,
        method: 'PUT',
        version,
        body,
      }),
      invalidatesTags: (_, error) => (error ? [] : ['Projects', 'EffectiveMembers', 'Audit']),
    }),
    moveProject: build.mutation({
      query: ({ id, version, parentProjectId }) => ({
        path: `/api/projects/${id}/parent`,
        method: 'PUT',
        version,
        body: { parentProjectId },
      }),
      invalidatesTags: (_, error) => (error ? [] : ['Projects', 'EffectiveMembers', 'Audit']),
    }),
    deleteProject: build.mutation({
      query: ({ id, version }) => ({ path: `/api/projects/${id}`, method: 'DELETE', version }),
      invalidatesTags: (_, error) => (error ? [] : ['Projects', 'EffectiveMembers', 'Audit']),
    }),
    createProject: build.mutation({
      query: ({
        projectName,
        accountName,
        accountId,
        description,
        projectType,
        parentProjectId,
      }) => ({
        path: '/api/projects',
        method: 'POST',
        body: {
          projectName,
          accountName,
          ...(accountId ? { accountId } : {}),
          description,
          projectType,
          parentProjectId,
        },
      }),
      invalidatesTags: (_, error) => (error ? [] : ['Projects', 'EffectiveMembers', 'Audit']),
    }),
    issueStatusCounts: build.query({
      async queryFn(statusOptions = statuses, __, ___, baseQuery) {
        const results = await Promise.all(
          statusOptions.map((Status) =>
            baseQuery({ path: '/api/issues/search', params: { Status, Page: 1, PageSize: 1 } }),
          ),
        );
        const failure = results.find((result) => result.error);
        if (failure) return { error: failure.error };
        return {
          data: statusOptions.map((status, index) => ({
            status,
            count: results[index].data.totalCount,
          })),
        };
      },
      providesTags: ['Issues'],
    }),
    scopedIssues: build.query({
      queryFn: (params, { signal }, _, baseQuery) => loadScopedIssues(params, baseQuery, signal),
      providesTags: ['Issues'],
    }),
    searchIssues: build.query({
      query: (params) => ({ path: '/api/issues/search', params }),
      providesTags: ['Issues'],
    }),
    issue: build.query({
      query: (id) => ({ path: `/api/issues/${id}` }),
      providesTags: (_, __, id) => [{ type: 'Issue', id }],
    }),
    createIssue: build.mutation({
      query: ({employeeRequest,...body}) => employeeRequest ? {path:'/api/issues/employee-requests',method:'POST',body:{issue:body,proposal:employeeRequest}} : ({ path: '/api/issues', method: 'POST', body }),
      invalidatesTags: ['Issues', 'Audit'],
    }),
    updateIssue: build.mutation({
      query: ({ id, body, version }) => ({
        path: `/api/issues/${id}`,
        method: 'PUT',
        body,
        version,
      }),
      invalidatesTags: (_, error, { id }) =>
        error ? [] : ['Issues', 'Audit', { type: 'Issue', id }],
    }),
    assignIssue: build.mutation({
      query: ({ id, assigneeUserId, assigneeUserIds, clearAssignee, version }) => ({
        path: `/api/issues/${id}/assignee`,
        method: 'PUT',
        body: clearAssignee
          ? { clearAssignee: true }
          : assigneeUserIds
            ? { assigneeUserIds, assigneeUserId }
            : { assigneeUserId },
        version,
      }),
      invalidatesTags: (_, error, { id }) =>
        error ? [] : ['Issues', 'Audit', { type: 'Issue', id }],
    }),
    transitionIssue: build.mutation({
      query: ({ id, statusCode, version, confirmSkip }) => ({
        path: `/api/issues/${id}/transition`,
        method: 'PUT',
        body: { statusCode, ...(confirmSkip ? { confirmSkip: true } : {}) },
        version,
      }),
      invalidatesTags: (_, error, { id }) =>
        error ? [] : ['Issues', 'Audit', { type: 'Issue', id }],
    }),
    deleteIssue: build.mutation({
      query: ({ id, version }) => ({ path: `/api/issues/${id}`, method: 'DELETE', version }),
      invalidatesTags: (_, error) => (error ? [] : ['Issues', 'Issue', 'Comments', 'Audit']),
    }),
    comments: build.query({
      query: (id) => ({ path: `/api/issues/${id}/comments` }),
      providesTags: (_, __, id) => [{ type: 'Comments', id }],
    }),
    addComment: build.mutation({
      query: ({ id, body, mentionedUserIds = [] }) => ({
        path: `/api/issues/${id}/comments`,
        method: 'POST',
        body: { body, ...(mentionedUserIds.length ? { mentionedUserIds } : {}) },
      }),
      invalidatesTags: (_, __, { id }) => [{ type: 'Comments', id }, 'Audit'],
    }),
    editComment: build.mutation({
      query: ({ id, commentId, body, version }) => ({
        path: `/api/issues/${id}/comments/${commentId}`,
        method: 'PUT',
        body: { body },
        version,
      }),
      invalidatesTags: (_, error, { id }) => (error ? [] : [{ type: 'Comments', id }, 'Audit']),
    }),
    deleteComment: build.mutation({
      query: ({ id, commentId, version }) => ({
        path: `/api/issues/${id}/comments/${commentId}`,
        method: 'DELETE',
        version,
      }),
      invalidatesTags: (_, error, { id }) => (error ? [] : [{ type: 'Comments', id }, 'Audit']),
    }),
    attachments: build.query({
      query: (id) => ({ path: `/api/issues/${id}/attachments` }),
      providesTags: (_, __, id) => [{ type: 'Attachments', id }],
    }),
    uploadAttachment: build.mutation({
      query: ({ id, file }) => {
        const body = new FormData();
        body.append('file', file);
        return { path: `/api/issues/${id}/attachments`, method: 'POST', body };
      },
      invalidatesTags: (_, error, { id }) => (error ? [] : [{ type: 'Attachments', id }, 'Audit']),
    }),
    audit: build.query({
      query: (params) => ({ path: '/api/audit/logs', params }),
      providesTags: ['Audit'],
    }),
  }),
});
export const {
  useDeleteAttachmentMutation,
  useEditCommentMutation,
  useDeleteCommentMutation,
  useAttachmentsQuery,
  useUploadAttachmentMutation,
  useNotificationsQuery,
  useUnreadNotificationsQuery,
  useReadNotificationMutation,
  useReadAllNotificationsMutation,
  useScopedIssuesQuery,
  useProjectQuery,
  useUserQuery,
  useAssignIssueMutation,
  useDevelopersQuery,
  useEffectiveMembersQuery,
  useProjectAssigneesQuery,
  useProjectMembersQuery,
  useAddProjectMemberMutation,
  useRemoveProjectMemberMutation,
  useProjectsQuery,
  useAccountsQuery,
  useCreateAccountMutation,
  useUpdateAccountMutation,
  useDeleteAccountMutation,
  useCreateProjectMutation,
  useUpdateProjectMutation,
  useMoveProjectMutation,
  useDeleteProjectMutation,
  useSearchIssuesQuery,
  useIssueStatusCountsQuery,
  useIssueQuery,
  useCreateIssueMutation,
  useUpdateIssueMutation,
  useTransitionIssueMutation,
  useDeleteIssueMutation,
  useCommentsQuery,
  useAddCommentMutation,
  useAuditQuery,
} = api;
