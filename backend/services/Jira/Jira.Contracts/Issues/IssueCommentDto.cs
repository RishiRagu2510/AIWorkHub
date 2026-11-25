namespace Jira.Contracts.Issues;

public sealed class IssueCommentDto
{
    public uint Version { get; init; }

    public long Id { get; init; }

    public long TenantId { get; init; }

    public long IssueId { get; init; }

    public long AuthorUserId { get; init; }

    public string Body { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}