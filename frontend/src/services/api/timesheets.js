import { api } from './api';
export const timesheetApi = api.injectEndpoints({
  endpoints: (build) => ({
    timesheetHierarchy: build.query({
      query: (params) => ({ path: '/api/timesheets/hierarchy', params }),
      providesTags: ['Timesheets'],
    }),
    workLogs: build.query({
      query: (params) => ({ path: '/api/worklogs', params }),
      providesTags: ['Timesheets'],
    }),
    timesheet: build.query({
      query: ({ team = false, ...params }) => ({
        path: `/api/timesheets/${team ? 'team' : 'me'}`,
        params,
      }),
      providesTags: ['Timesheets'],
    }),
    timesheetApprovers: build.query({
      query: (id) => ({ path: `/api/timesheets/${id}/approvers` }),
      providesTags: ['Timesheets'],
    }),
    createWorkLog: build.mutation({
      query: (body) => ({ path: '/api/worklogs', method: 'POST', body }),
      invalidatesTags: (_, error) => (error ? [] : ['Timesheets', 'Audit']),
    }),
    updateWorkLog: build.mutation({
      query: ({ id, version, body }) => ({
        path: `/api/worklogs/${id}`,
        method: 'PUT',
        version,
        body,
      }),
      invalidatesTags: (_, error) => (error ? [] : ['Timesheets', 'Audit']),
    }),
    deleteWorkLog: build.mutation({
      query: ({ id, version }) => ({ path: `/api/worklogs/${id}`, method: 'DELETE', version }),
      invalidatesTags: (_, error) => (error ? [] : ['Timesheets', 'Audit']),
    }),
    timesheetAction: build.mutation({
      query: ({ action, version, ...body }) => ({
        path: `/api/timesheets/${action}`,
        method: 'POST',
        version,
        body,
      }),
      invalidatesTags: (_, error) => (error ? [] : ['Timesheets', 'Notifications', 'Audit']),
    }),
  }),
});
export const {
  useTimesheetHierarchyQuery,
  useWorkLogsQuery,
  useTimesheetQuery,
  useTimesheetApproversQuery,
  useCreateWorkLogMutation,
  useUpdateWorkLogMutation,
  useDeleteWorkLogMutation,
  useTimesheetActionMutation,
} = timesheetApi;
