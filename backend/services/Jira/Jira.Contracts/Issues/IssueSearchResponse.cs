namespace Jira.Contracts.Issues;

public sealed class IssueSearchResponse
{
    public IReadOnlyList<IssueDto> Items { get; init; }
        = Array.Empty<IssueDto>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }
}
