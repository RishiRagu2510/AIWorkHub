using Jira.Domain.Common;

namespace Jira.Domain.Entities;

public sealed class IssueComment : Entity
{
    private IssueComment()
    {
    }

    public IssueComment(
        long tenantId,
        long issueId,
        long authorUserId,
        string body)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tenantId));
        }

        if (issueId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(issueId));
        }

        if (authorUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authorUserId));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException(
                "Comment body is required.",
                nameof(body));
        }

        TenantId = tenantId;
        IssueId = issueId;
        AuthorUserId = authorUserId;
        Body = body.Trim();
    }

    public uint Version { get; private set; }

    public long TenantId { get; private set; }

    public long IssueId { get; private set; }

    public long AuthorUserId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public void UpdateBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException(
                "Comment body is required.",
                nameof(body));
        }

        Body = body.Trim();
        Touch();
    }
}