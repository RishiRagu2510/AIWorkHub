namespace AIAgent.Contracts.Dtos;

public record ChatMessageDto(
    string Role, 
    string Content,
    string? ToolName = null,
    string? ToolCallId = null
);

public record AgentChatRequest(
    string Message,
    string? SessionId = null,
    string? ContextPortal = null, 
    Dictionary<string, object>? ExtraContext = null,
    List<ChatMessageDto>? History = null,
    
    
    List<ChatAttachmentRefDto>? Attachments = null
);

public record ChatAttachmentRefDto(string Id);

public record AgentChatResponse(
    string SessionId,
    string Message,
    List<ExecutedToolActionDto>? ExecutedActions = null,
    bool Success = true,
    string? Error = null,
    
    string? Notice = null
);

public record ExecutedToolActionDto(
    string ToolName,
    string Description,
    object? Arguments,
    object? Result,
    bool Success
);

public record SingleCallCopilotRequest(
    string Feature, 
    string InputText,
    Dictionary<string, object>? Parameters = null
);

public record SingleCallCopilotResponse(
    string OutputText,
    object? StructuredData = null,
    bool Success = true,
    string? Error = null
);
