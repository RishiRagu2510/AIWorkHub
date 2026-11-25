namespace Jira.Infrastructure.Persistence.Collaboration;
public sealed class ActivityEvent
{
    public Guid EventId { get; set; }
    public long TenantId { get; set; }
    public string EntityType { get; set; } = "";
    public long EntityId { get; set; }
    public long? ProjectId { get; set; }
    public long ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
public sealed class IssueWatcher
{
    public long TenantId { get; set; }
    public long IssueId { get; set; }
    public long UserId { get; set; }
}
public sealed class WorkflowRules
{
    public long TenantId { get; set; }
    public long ProjectId { get; set; }
    public string Definition { get; set; } = "{}";
    public uint Version { get; set; }
}
public sealed class RequestReplay
{
    public string? ResourceType { get; set; }
    public long? ResourceId { get; set; }
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public string Key { get; set; } = "";
    public string Hash { get; set; } = "";
    public int Status { get; set; }
    public string Response { get; set; } = "";
    public string? ETag { get; set; }
    public string? Location { get; set; }
    public long IssueId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class Board
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long ProjectId { get; set; }
    public string Name { get; set; } = "";
    public string BoardType { get; set; } = "KANBAN";
    public string FilterDefinition { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class BoardColumn
{
    public long Id { get; set; }
    public long BoardId { get; set; }
    public string Name { get; set; } = "";
    public string StatusCode { get; set; } = "";
    public int Position { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
