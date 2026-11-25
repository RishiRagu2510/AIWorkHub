using Jira.Domain.Entities;

namespace Jira.Application.Abstractions.Persistence;

public interface IIssueCommentRepository
{
    Task<IReadOnlyList<IssueComment>> GetByIssueAsync(
        long tenantId,
        long issueId,
        CancellationToken cancellationToken = default);

    Task<IssueComment?> GetByIdAsync(
        long tenantId,
        long commentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        IssueComment comment,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}