namespace Jira.Contracts.Issues;

public sealed class IssueDto
{
    public string[]? Labels { get; init; }
    public long[]? FixVersionIds { get; init; }
    public long[]? AffectsVersionIds { get; init; }
    public int? OriginalEstimateMinutes { get; init; }
    public int? RemainingEstimateMinutes { get; init; }
    public long LoggedTimeMinutes { get; init; }

    public uint Version { get; init; }

    public long Id { get; init; }

    public long TenantId { get; init; }

    public long ProjectId { get; init; }

    public long IssueNumber { get; init; }

    public string IssueKey { get; init; } = string.Empty;

    public string IssueType { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string StatusCode { get; init; } = string.Empty;

    public string Priority { get; init; } = string.Empty;

    public long ReporterUserId { get; init; }

    public long? AssigneeUserId { get; init; }
    public long[] AssigneeUserIds { get; init; } = [];

    public long? ParentIssueId { get; init; }

    public long? ComponentId { get; init; }

    public int? StoryPoints { get; init; }

    public DateOnly? DueDate { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}