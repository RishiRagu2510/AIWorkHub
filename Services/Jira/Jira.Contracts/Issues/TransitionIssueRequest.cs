namespace Jira.Contracts.Issues;

public sealed class TransitionIssueRequest
{
    public bool ConfirmSkip { get; init; }
    public string StatusCode { get; init; } = string.Empty;
}
