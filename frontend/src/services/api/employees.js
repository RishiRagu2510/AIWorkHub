import { api } from './api';
export const employeeApi = api.enhanceEndpoints({ addTagTypes: ['Employees'] }).injectEndpoints({
  endpoints: (b) => ({
    deleteEmployeeSetup: b.mutation({
      query: ({kind,id}) => ({path: `/api/employees/setup/${kind}${id ? `/${id}` : ''}`, method: 'DELETE'}),
      invalidatesTags: (_,error) => error ? [] : ['Employees'],
    }),
    employeeSetupUsers: b.query({
      query: params => ({path: '/api/employees/setup/users', params}),
      providesTags: ['Employees'],
    }),
    assignEmployeeUserRoles: b.mutation({
      query: ({userId, roleIds, expectedRoleIds}) => ({path: `/api/employees/setup/users/${userId}/roles`, method: 'PUT', body: {roleIds, expectedRoleIds}}),
      invalidatesTags: (_, error) => error ? [] : ['Employees'],
    }),
    bulkAssignEmployeeUserRoles: b.mutation({
      query: ({ userIds, roleIds, mode }) => ({
        path: '/api/employees/setup/users/bulk-roles',
        method: 'POST',
        body: { userIds, roleIds, mode },
      }),
      invalidatesTags: (_, error) => (error ? [] : ['Employees']),
    }),
    employeeSetup: b.query({
      query: () => ({path: '/api/employees/setup'}),
      providesTags: ['Employees'],
    }),
    saveEmployeeSetup: b.mutation({
      query: ({kind, id, body}) => ({path: `/api/employees/setup/${kind}${id ? `/${id}` : ''}`, method: id ? 'PUT' : 'POST', body}),
      invalidatesTags: (_, error) => error ? [] : ['Employees'],
    }),
    employees: b.query({
      query: (params) => ({ path: '/api/employees', params }),
      providesTags: ['Employees'],
    }),
    employee: b.query({
      query: (id) => ({ path: `/api/employees/${id}` }),
      providesTags: ['Employees'],
    }),
    employeeSection: b.query({
      query: ({ id, section }) => ({ path: `/api/employees/${id}/${section}` }),
      providesTags: ['Employees'],
    }),
    employeeCareerCatalog: b.query({
      query: () => ({ path: '/api/employees/career-catalog' }),
      providesTags: ['Employees'],
    }),
    employeeReferences: b.query({
      query: () => ({ path: '/api/employees/references' }),
      providesTags: ['Employees'],
    }),
    employeeFullChart: b.query({
      async queryFn(_, api, extra, baseQuery) {
        const items = [];
        for (const roleLevel of ['ADMIN', 'MANAGER', 'DEVELOPER', 'OTHER']) {
          let page = 1;
          while (true) {
            const result = await baseQuery({path:'/api/employees/org-chart', params:{roleLevel,page}});
            if (result.error) return {error:result.error};
            items.push(...result.data.items.map(item => ({...item, roleLevel})));
            if (page * result.data.pageSize >= result.data.totalCount || !result.data.items.length) break;
            page++;
          }
        }
        return {data:items};
      },
      providesTags: ['Employees'],
    }),
    employeeChart: b.query({
      query: (params) => ({ path: '/api/employees/org-chart', params }),
      providesTags: ['Employees'],
    }),
    employeeManagers: b.query({
      query: (employeeId) => ({path: '/api/employees/manager-options', params: {employeeId}}),
      providesTags: ['Employees'],
    }),
    employeeUsers: b.query({
      query: (params) => ({
        path: '/api/employees/login-users',
        params: { ...params, pageSize: 25 },
      }),
      providesTags: ['Employees'],
    }),
    employeeProjects: b.query({
      query: (userId) => ({ path: `/api/projects/by-user/${userId}` }),
      providesTags: ['Projects'],
    }),
    employeeTime: b.query({
      query: ({ team = false, ...params }) => ({
        path: `/api/timesheets/${team ? 'report' : 'me'}`,
        params,
      }),
      providesTags: ['Timesheets'],
    }),
    allocations: b.query({
      query: (userId) => ({ path: '/api/projects/allocations', params: {userId} }),
      providesTags: ['Employees'],
    }),
    saveAllocation: b.mutation({
      query: ({id, ...body}) => ({path: `/api/projects/allocations${id ? `/${id}` : ''}`, method: id ? 'DELETE' : 'POST', ...(id ? {} : {body})}),
      invalidatesTags: (_,error) => error ? [] : ['Employees','Audit'],
    }),
    saveEmployee: b.mutation({
      query: ({ path = '', method = 'POST', body, version }) => ({
        path: `/api/employees${path}`,
        method,
        body,
        version,
      }),
      invalidatesTags: (_, error) => (error ? [] : ['Employees', 'Audit']),
    }),
  }),
});
export const {
  useDeleteEmployeeSetupMutation,
  useEmployeeSetupUsersQuery, useAssignEmployeeUserRolesMutation,
  useBulkAssignEmployeeUserRolesMutation,
  useEmployeeSetupQuery, useSaveEmployeeSetupMutation,
  useEmployeeFullChartQuery,
  useEmployeeManagersQuery,
  useAllocationsQuery, useSaveAllocationMutation,
  useEmployeesQuery,
  useEmployeeQuery, useLazyEmployeeQuery,
  useEmployeeSectionQuery,
  useEmployeeCareerCatalogQuery,
  useEmployeeReferencesQuery,
  useEmployeeChartQuery,
  useEmployeeUsersQuery,
  useEmployeeProjectsQuery,
  useEmployeeTimeQuery,
  useSaveEmployeeMutation,
} = employeeApi;
