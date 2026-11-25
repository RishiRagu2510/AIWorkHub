using AIAgent.Application.Interfaces;
using AIAgent.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AIAgent.Infrastructure.Llm;

public sealed class ModelRoutingLlmProvider(LlmProvider gemini, OpenAiChatClient openAi, GroqChatClient groq, ILogger<ModelRoutingLlmProvider> logger) : ILlmProvider
{
    public Task<LlmChatResponse> ChatAsync(
        List<AgentMessage> messages,
        IEnumerable<IAgentTool>? availableTools = null,
        string? model = null,
        CancellationToken cancellationToken = default) =>
        RouteAsync(messages, availableTools, model, null, cancellationToken);

    public Task<LlmChatResponse> ChatStreamAsync(
        List<AgentMessage> messages,
        IEnumerable<IAgentTool>? availableTools,
        string? model,
        Func<string, Task> onText,
        CancellationToken cancellationToken = default) =>
        RouteAsync(messages, availableTools, model, onText, cancellationToken);

    private async Task<LlmChatResponse> RouteAsync(
        List<AgentMessage> messages,
        IEnumerable<IAgentTool>? availableTools,
        string? model,
        Func<string, Task>? onText,
        CancellationToken cancellationToken)
    {
        var useOpenAi = OpenAiChatClient.Owns(model);
        var useGroq = !useOpenAi && GroqChatClient.Owns(model);
        if (!useOpenAi && !useGroq)
            return onText is null
                ? await gemini.ChatAsync(messages, availableTools, model, cancellationToken)
                : await gemini.ChatStreamAsync(messages, availableTools, model, onText, cancellationToken);

        var tools = availableTools?.ToList();
        var chosen = model!.Trim();
        string reason;
        try
        {
            if (onText is null)
                return useOpenAi
                    ? await openAi.ChatAsync(messages, tools, chosen, cancellationToken)
                    : await groq.ChatAsync(messages, tools, chosen, cancellationToken);

            return useOpenAi
                ? await openAi.ChatStreamAsync(messages, tools, chosen, onText, cancellationToken)
                : await groq.ChatStreamAsync(messages, tools, chosen, onText, cancellationToken);
        }
        catch (Exception ex) when (ex is OpenAiUnavailableException or GroqUnavailableException)
        {
            reason = ex.Message;
        }

        logger.LogWarning("Model {Model} unavailable: {Reason} Answering with Gemini.", chosen, reason);
        var answer = onText is null
            ? await gemini.ChatAsync(messages, tools, null, cancellationToken)
            : await gemini.ChatStreamAsync(messages, tools, null, onText, cancellationToken);
        answer.ModelNotice = $"{chosen} could not answer ({reason.TrimEnd('.')}), so this reply came from Gemini instead.";
        return answer;
    }

    public Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
        gemini.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);
}
