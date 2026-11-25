namespace AIAgent.Application.Interfaces;

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    string Category { get; } 
    string RequiredPermission { get; } 
    string? RequiredPortal => null;
    object ParameterSchema { get; }
    Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default);
}

public record ToolResult(
    bool Success,
    object? Data,
    string? Error = null
);
