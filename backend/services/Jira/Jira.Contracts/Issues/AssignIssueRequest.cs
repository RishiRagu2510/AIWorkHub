namespace Jira.Contracts.Issues;

public sealed class AssignIssueRequest
{
    public long? AssigneeUserId { get; init; }
    public long[]? AssigneeUserIds { get; init; }
    public bool ClearAssignee { get; init; }
}
