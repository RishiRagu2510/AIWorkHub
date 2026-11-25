using Jira.Contracts.Issues;

namespace Jira.Application.Abstractions.Services;

public interface IIssueCommentService
{
    Task<IssueCommentDto> CreateAsync(
        long tenantId,
        long authorUserId,
        long issueId,
        CreateIssueCommentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueCommentDto>> GetByIssueAsync(
        long tenantId,
        long issueId,
        CancellationToken cancellationToken = default);
}