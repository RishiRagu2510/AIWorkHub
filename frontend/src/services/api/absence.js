import { api } from './api';
export const absenceApi = api.enhanceEndpoints({ addTagTypes: ['Absence'] }).injectEndpoints({
  endpoints: (b) => ({
    absenceRequests: b.query({
      query: (params) => ({ path: '/api/employees/absence', params }),
      providesTags: ['Absence'],
    }),
    absencePolicy: b.query({
      query: (year) => ({ path: '/api/employees/absence/policy', params: { year } }),
      providesTags: ['Absence'],
    }),
    saveAbsence: b.mutation({
      query: ({ body, id }) => ({ path: `/api/employees/absence${id ? `/${id}/decision` : ''}`, method: 'POST', body }),
      invalidatesTags: (_, error) => error ? [] : ['Absence', 'Audit'],
    }),
    saveAbsencePolicy: b.mutation({
      query: ({ year, body }) => ({ path: '/api/employees/absence/policy', method: 'PUT', params: { year }, body }),
      invalidatesTags: (_, error) => error ? [] : ['Absence'],
    }),
  }),
});
export const { useAbsenceRequestsQuery, useAbsencePolicyQuery, useSaveAbsenceMutation, useSaveAbsencePolicyMutation } = absenceApi;
