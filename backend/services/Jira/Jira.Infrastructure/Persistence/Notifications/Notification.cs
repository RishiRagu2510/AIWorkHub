namespace Jira.Infrastructure.Persistence.Notifications;
public sealed class Notification
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public long TenantId { get; set; }
    public long RecipientUserId { get; set; }
    public long ActorUserId { get; set; }
    public long? TimesheetPeriodId { get; set; }
    public string? Reason { get; set; }
    public long IssueId { get; set; }
    public string Kind { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
