namespace Jira.Contracts.Issues;

public sealed class UpdateIssueRequest
{
    public string[]? Labels { get; init; }
    public long[]? FixVersionIds { get; init; }
    public long[]? AffectsVersionIds { get; init; }
    public int? OriginalEstimateMinutes { get; init; }
    public int? RemainingEstimateMinutes { get; init; }

    public bool ConfirmSkip { get; init; }
    public bool ClearAssignee { get; init; }
    public bool ClearParentIssue { get; init; }
    public bool ClearComponent { get; init; }
    public bool ClearStoryPoints { get; init; }
    public bool ClearDueDate { get; init; }

    public string? Summary { get; init; }

    public string? Description { get; init; }

    public string? Priority { get; init; }

    public long? AssigneeUserId { get; init; }
    public long[]? AssigneeUserIds { get; init; }

    public long? ParentIssueId { get; init; }

    public long? ComponentId { get; init; }

    public int? StoryPoints { get; init; }

    public DateOnly? DueDate { get; init; }

    public string? StatusCode { get; init; }
}