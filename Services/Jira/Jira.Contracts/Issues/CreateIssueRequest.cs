namespace Jira.Contracts.Issues;

public sealed class CreateIssueRequest
{
    public string[]? Labels { get; init; }
    public long[]? FixVersionIds { get; init; }
    public long[]? AffectsVersionIds { get; init; }
    public int? OriginalEstimateMinutes { get; init; }
    public int? RemainingEstimateMinutes { get; init; }

    public long ProjectId { get; init; }

    public string IssueType { get; init; } = "TASK";

    public string Summary { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string StatusCode { get; init; } = "TODO";

    public string Priority { get; init; } = "MEDIUM";

    public long? AssigneeUserId { get; init; }
    public long[]? AssigneeUserIds { get; init; }

    public long? ParentIssueId { get; init; }

    public long? ComponentId { get; init; }

    public int? StoryPoints { get; init; }

    public DateOnly? DueDate { get; init; }
}