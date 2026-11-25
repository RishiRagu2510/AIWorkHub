import { useContext } from 'react';
import { ReactReduxContext } from 'react-redux';
import { api } from './api';
import { chatWithStreaming } from './athennaStream';

export const agentApi = api?.injectEndpoints
  ? api.injectEndpoints({
      endpoints: (build) => ({
        getAgentStatus: build.query({
          query: () => ({ path: '/api/v1/agent/status' }),
        }),
        getAgentModels: build.query({
          query: () => ({ path: '/api/v1/agent/models' }),
        }),
        // The notes the person asked Athenna to remember. They are added through the chat; here they can be seen and removed.
        getAgentMemory: build.query({
          query: () => ({ path: '/api/v1/agent/memory' }),
          providesTags: ['AgentMemory'],
        }),
        deleteAgentMemory: build.mutation({
          query: (id) => ({ path: `/api/v1/agent/memory/${encodeURIComponent(id)}`, method: 'DELETE' }),
          invalidatesTags: ['AgentMemory'],
        }),
        clearAgentMemory: build.mutation({
          query: () => ({ path: '/api/v1/agent/memory', method: 'DELETE' }),
          invalidatesTags: ['AgentMemory'],
        }),
        // Company documents Admin and HR upload for Athenna to answer from. The server refuses anyone else.
        getAgentKnowledge: build.query({
          query: () => ({ path: '/api/v1/agent/knowledge' }),
          providesTags: ['AgentKnowledge'],
        }),
        getAgentKnowledgeDoc: build.query({
          query: (id) => ({ path: `/api/v1/agent/knowledge/${encodeURIComponent(id)}` }),
          providesTags: (result, error, id) => [{ type: 'AgentKnowledge', id }],
        }),
        uploadAgentKnowledge: build.mutation({
          query: ({ file, title, roles }) => {
            const body = new FormData();
            body.append('file', file, file.name);
            if (title) body.append('title', title);
            (roles || []).forEach((role) => body.append('roles', role));
            return { path: '/api/v1/agent/knowledge', method: 'POST', body };
          },
          invalidatesTags: (_, error) => (error ? [] : ['AgentKnowledge']),
        }),
        deleteAgentKnowledge: build.mutation({
          query: (id) => ({ path: `/api/v1/agent/knowledge/${encodeURIComponent(id)}`, method: 'DELETE' }),
          invalidatesTags: (_, error) => (error ? [] : ['AgentKnowledge']),
        }),
        // What needs the signed-in person's attention (overdue work, an unsubmitted timesheet, requests waiting for them).
        getAgentNudges: build.query({
          query: () => ({ path: '/api/v1/agent/nudges' }),
        }),
        // The reply is streamed, so progress reaches the page as it happens (see listenToChat). The result is the same
        // complete reply the plain endpoint gives, and the same tags are refreshed once it is in.
        chatWithAthenna: build.mutation({
          async queryFn(arg, { signal }) {
            try {
              return { data: await chatWithStreaming(arg, { signal }) };
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
          // 'AgentMemory': the chat may just have been asked to remember or forget something.
          invalidatesTags: ['Issues', 'Timesheets', 'AgentMemory'],
        }),
        runAthennaCopilot: build.mutation({
          query: (body) => ({
            path: '/api/v1/agent/copilot',
            method: 'POST',
            body,
          }),
        }),
        getAgentSummary: build.query({
          query: () => ({ path: '/api/agent/summary' }),
          providesTags: ['AgentTasks'],
        }),
        getAgentTasks: build.query({
          query: (params) => ({ path: '/api/agent/tasks', params }),
          providesTags: ['AgentTasks'],
        }),
        getAgentTask: build.query({
          query: (id) => ({ path: `/api/agent/tasks/${id}` }),
          providesTags: (result, error, id) => [{ type: 'AgentTasks', id }],
        }),
        executeAgentPrompt: build.mutation({
          query: (body) => ({
            path: '/api/agent/prompt',
            method: 'POST',
            body,
          }),
          invalidatesTags: ['AgentTasks', 'Issues'],
        }),
        approveAgentTask: build.mutation({
          query: ({ id, approved, notes }) => ({
            path: `/api/agent/tasks/${id}/approve`,
            method: 'POST',
            body: { approved, notes },
          }),
          invalidatesTags: ['AgentTasks', 'Issues'],
        }),
        cancelAgentTask: build.mutation({
          query: (id) => ({
            path: `/api/agent/tasks/${id}/cancel`,
            method: 'POST',
            body: { approved: false },
          }),
          invalidatesTags: ['AgentTasks'],
        }),
      }),
    })
  : {};

const rawUseChatWithAthennaMutation = agentApi?.useChatWithAthennaMutation;
const rawUseRunAthennaCopilotMutation = agentApi?.useRunAthennaCopilotMutation;
const useGetAgentModelsQuery = agentApi?.useGetAgentModelsQuery || (() => ({}));
const useGetAgentMemoryQuery = agentApi?.useGetAgentMemoryQuery || (() => ({}));
const useDeleteAgentMemoryMutation = agentApi?.useDeleteAgentMemoryMutation || (() => [async () => ({}), {}]);
const useClearAgentMemoryMutation = agentApi?.useClearAgentMemoryMutation || (() => [async () => ({}), {}]);
const useGetAgentKnowledgeQuery = agentApi?.useGetAgentKnowledgeQuery || (() => ({}));
const useGetAgentKnowledgeDocQuery = agentApi?.useGetAgentKnowledgeDocQuery || (() => ({}));
const useUploadAgentKnowledgeMutation = agentApi?.useUploadAgentKnowledgeMutation || (() => [async () => ({}), {}]);
const useDeleteAgentKnowledgeMutation = agentApi?.useDeleteAgentKnowledgeMutation || (() => [async () => ({}), {}]);
const useGetAgentNudgesQuery = agentApi?.useGetAgentNudgesQuery || (() => ({}));
const useGetAgentStatusQuery = agentApi?.useGetAgentStatusQuery || (() => ({}));
const useGetAgentSummaryQuery = agentApi?.useGetAgentSummaryQuery || (() => ({}));
const useGetAgentTasksQuery = agentApi?.useGetAgentTasksQuery || (() => ({}));
const useGetAgentTaskQuery = agentApi?.useGetAgentTaskQuery || (() => ({}));
const useExecuteAgentPromptMutation = agentApi?.useExecuteAgentPromptMutation || (() => [async () => ({}), {}]);
const useApproveAgentTaskMutation = agentApi?.useApproveAgentTaskMutation || (() => [async () => ({}), {}]);
const useCancelAgentTaskMutation = agentApi?.useCancelAgentTaskMutation || (() => [async () => ({}), {}]);

export const useChatWithAthennaMutation = (...args) => {
  const ctx = useContext(ReactReduxContext);
  if (!ctx || !ctx.store || !rawUseChatWithAthennaMutation) {
    return [async () => ({ data: {} }), { isLoading: false }];
  }
  return rawUseChatWithAthennaMutation(...args);
};

export const useRunAthennaCopilotMutation = (...args) => {
  const ctx = useContext(ReactReduxContext);
  if (!ctx || !ctx.store || !rawUseRunAthennaCopilotMutation) {
    return [async () => ({ data: {} }), { isLoading: false }];
  }
  return rawUseRunAthennaCopilotMutation(...args);
};

export {
  useGetAgentModelsQuery,
  useGetAgentMemoryQuery,
  useDeleteAgentMemoryMutation,
  useClearAgentMemoryMutation,
  useGetAgentKnowledgeQuery,
  useGetAgentKnowledgeDocQuery,
  useUploadAgentKnowledgeMutation,
  useDeleteAgentKnowledgeMutation,
  useGetAgentNudgesQuery,
  useGetAgentStatusQuery,
  useGetAgentSummaryQuery,
  useGetAgentTasksQuery,
  useGetAgentTaskQuery,
  useExecuteAgentPromptMutation,
  useApproveAgentTaskMutation,
  useCancelAgentTaskMutation,
};
