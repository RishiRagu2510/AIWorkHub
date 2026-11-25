import { api } from './api';
export const onboardingApi = api.injectEndpoints({
  endpoints: (b) => ({
    onboardingOptions: b.query({ query: () => ({ path: '/api/employees/onboarding/options' }) }),
    onboardingRequest: b.query({
      query: (id) => ({ path: `/api/issues/${id}/employee-request` }),
      providesTags: (_, __, id) => [{ type: 'Issue', id }],
    }),
    reviewOnboarding: b.mutation({
      query: ({ id, ...body }) => ({
        path: `/api/issues/${id}/employee-request/review`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_, error, { id }) => (error ? [] : [{ type: 'Issue', id }, 'Audit']),
    }),
    completeOnboarding: b.mutation({
      query: ({ id, body }) => ({
        path: `/api/issues/${id}/employee-request/complete`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_, error, { id }) =>
        error ? [] : [{ type: 'Issue', id }, 'Employees', 'Audit'],
    }),
  }),
});
export const {
  useOnboardingOptionsQuery,
  useOnboardingRequestQuery,
  useReviewOnboardingMutation,
  useCompleteOnboardingMutation,
} = onboardingApi;
