namespace Jira.Contracts.Issues;

public sealed class CreateIssueCommentRequest
{
    public long[] MentionedUserIds { get; init; } = [];


    public string Body { get; init; } = string.Empty;
}