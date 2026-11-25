using Jira.Domain.Common;
namespace Jira.Domain.Entities;
public sealed class TimesheetPeriod : Entity
{
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string Status { get; set; } = "DRAFT";
    public DateTimeOffset? SubmittedAt { get; set; }
    public long? ApproverUserId { get; set; }
    public long? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public uint Version { get; private set; }
    public void Changed() => Touch();
}
