namespace AIAgent.Domain.Models;

public enum MessageRole
{
    System,
    User,
    Assistant,
    Tool
}

public class AgentToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
    
    public string? ThoughtSignature { get; set; }
}


public class AgentMedia
{
    public string Name { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];

    public bool IsImage => MimeType.StartsWith("image/", StringComparison.Ordinal);
}

public class AgentMessage
{
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    
    public List<AgentMedia>? Media { get; set; }
    public string? ToolName { get; set; }
    public string? ToolCallId { get; set; }
    
    public List<AgentToolCall>? ToolCalls { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static AgentMessage User(string content) => new() { Role = MessageRole.User, Content = content };
    public static AgentMessage Assistant(string content) => new() { Role = MessageRole.Assistant, Content = content };
    public static AgentMessage System(string content) => new() { Role = MessageRole.System, Content = content };
    public static AgentMessage Tool(string toolName, string toolCallId, string result) => 
        new() { Role = MessageRole.Tool, ToolName = toolName, ToolCallId = toolCallId, Content = result };
}

public class AgentSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? PortalContext { get; set; }
    public List<AgentMessage> Messages { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
}
