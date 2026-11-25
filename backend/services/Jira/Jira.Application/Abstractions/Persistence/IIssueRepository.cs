using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jira.Domain.Entities;

namespace Jira.Application.Abstractions.Persistence;

public interface IIssueRepository
{
    Task ValidateVersionsAsync(long tenant,long project,long[] ids,CancellationToken ct);
    Task ValidateComponentAsync(long tenantId, long projectId, long? componentId, CancellationToken ct);
    Task ValidateParentAsync(long tenantId, long projectId, long issueId, long? parentId, CancellationToken ct, Jira.Domain.Enums.IssueType? childType = null);

    Task<Issue?> GetByIdAsync(
        long tenantId,
        long issueId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Issue>> GetByProjectAsync(
        long tenantId,
        long projectId,
        CancellationToken cancellationToken = default);

    Task<Issue?> GetByIssueKeyAsync(
        long tenantId,
        string issueKey,
        CancellationToken cancellationToken = default);

    Task<IssueSearchResult> SearchAsync(
        long tenantId,
        IssueSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Issue issue,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}