import { api } from './api';
// The company's plan as the server sees it right now (not as the token said when the person signed in), plus the requests
// people have made for things the plan does not include.
export const planApi = api.enhanceEndpoints({ addTagTypes: ['Plan'] }).injectEndpoints({
  endpoints: (b) => ({
    plan: b.query({
      query: () => ({ path: '/api/tenants/current/plan' }),
      providesTags: ['Plan'],
    }),
    requestAccess: b.mutation({
      query: (body) => ({ path: '/api/tenants/current/plan/requests', method: 'POST', body }),
      invalidatesTags: (_, error) => (error ? [] : ['Plan']),
    }),
    // Takes a request back. The server only lets the person who asked, or an admin, do this.
    cancelAccessRequest: b.mutation({
      query: (id) => ({ path: `/api/tenants/current/plan/requests/${encodeURIComponent(id)}`, method: 'DELETE' }),
      invalidatesTags: ['Plan'],
    }),
  }),
});
export const { usePlanQuery, useRequestAccessMutation, useCancelAccessRequestMutation } = planApi;
