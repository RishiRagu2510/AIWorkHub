using Jira.Domain.Enums;

namespace Jira.Application.Abstractions.Persistence;

public sealed class IssueSearchCriteria
{
    public string? Label { get; init; }
    public long? ComponentId { get; init; }
    public long? FixVersionId { get; init; }
    public long? AffectsVersionId { get; init; }

    public bool RootOnly { get; init; }
    public long? ProjectId { get; init; }
    public long? ParentIssueId { get; init; }
    public DateOnly? DueFrom { get; init; }
    public DateOnly? DueTo { get; init; }
    public string? WorkGroup { get; init; }
    public DateOnly? Today { get; init; }

    public string? IssueKey { get; init; }

    public IssueType? IssueType { get; init; }

    public string? StatusCode { get; init; }

    public string? Priority { get; init; }

    public long? AssigneeUserId { get; init; }

    public long? ReporterUserId { get; init; }

    public string? Text { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string SortBy { get; init; } = "issueNumber";

    public string SortDirection { get; init; } = "asc";
}