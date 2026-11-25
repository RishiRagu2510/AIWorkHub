using Jira.Application.Abstractions.Persistence;
using Jira.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jira.Infrastructure.Persistence.Repositories;

public sealed class IssueCommentRepository : IIssueCommentRepository
{
    private readonly JiraDbContext _dbContext;
    private readonly JiraResourceScope _scope;

    public IssueCommentRepository(JiraDbContext dbContext, JiraResourceScope scope)
    {
        _dbContext = dbContext;
        _scope = scope;
    }

    public async Task<IReadOnlyList<IssueComment>> GetByIssueAsync(
        long tenantId,
        long issueId,
        CancellationToken cancellationToken = default)
    {
        return await _scope.Comments(tenantId)
            .Where(
                comment =>
                    comment.TenantId == tenantId &&
                    comment.IssueId == issueId)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IssueComment?> GetByIdAsync(
        long tenantId,
        long commentId,
        CancellationToken cancellationToken = default)
    {
        return await _scope.Comments(tenantId)
            .FirstOrDefaultAsync(
                comment =>
                    comment.TenantId == tenantId &&
                    comment.Id == commentId,
                cancellationToken);
    }

    public async Task AddAsync(
        IssueComment comment,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.IssueComments.AddAsync(
            comment,
            cancellationToken);
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}