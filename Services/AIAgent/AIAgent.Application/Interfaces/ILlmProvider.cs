using AIAgent.Domain.Models;

namespace AIAgent.Application.Interfaces;

public interface ILlmProvider
{
    Task<LlmChatResponse> ChatAsync(
        List<AgentMessage> messages,
        IEnumerable<IAgentTool>? availableTools = null,
        string? model = null,
        CancellationToken cancellationToken = default
    );

    Task<LlmChatResponse> ChatStreamAsync(
        List<AgentMessage> messages,
        IEnumerable<IAgentTool>? availableTools,
        string? model,
        Func<string, Task> onText,
        CancellationToken cancellationToken = default
    ) => ChatAsync(messages, availableTools, model, cancellationToken);

    Task<string> GenerateTextAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default
    );
}

public class ToolCall
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
    public string? ThoughtSignature { get; set; }
}

public class LlmChatResponse
{
    public string? Content { get; set; }
    public List<ToolCall>? ToolCalls { get; set; }
    public bool HasToolCalls => ToolCalls is { Count: > 0 };
    public const string UnexpectedErrorMessage = "An unexpected error occurred. Please try again later.";
    public string? FallbackReason { get; set; }
    public string? ModelNotice { get; set; }
}


public class LlmUnavailableException(string message) : Exception(message);
