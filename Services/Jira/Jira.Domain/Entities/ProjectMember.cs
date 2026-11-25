namespace Jira.Domain.Entities;


public sealed class ProjectMember
{
    public long Id { get; private set; }
    public long TenantId { get; private set; }
    public long ProjectId { get; private set; }
    public long UserId { get; private set; }
    public string Role { get; private set; } = "MEMBER";
    public DateTimeOffset CreatedAt { get; private set; }
}
