using Jira.Contracts.Issues;

namespace Jira.Application.Abstractions.Services;

public interface IIssueService
{
    Task<IssueDto> CreateAsync(
        long tenantId,
        long reporterUserId,
        CreateIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<IssueDto?> GetByIdAsync(
        long tenantId,
        long issueId,
        CancellationToken cancellationToken = default);

    Task<IssueDto?> GetByKeyAsync(
        long tenantId,
        string issueKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueDto>> GetByProjectAsync(
        long tenantId,
        long projectId,
        CancellationToken cancellationToken = default);

    Task<IssueSearchResponse> SearchAsync(
        long tenantId,
        IssueSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<IssueDto?> UpdateAsync(
        long tenantId,
        long actorUserId,
        long issueId,
        UpdateIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<IssueDto?> AssignAsync(
        long tenantId,
        long actorUserId,
        long issueId,
        AssignIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<IssueDto?> TransitionAsync(
        long tenantId,
        long actorUserId,
        long issueId,
        TransitionIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<IssueDto?> DeleteAsync(
        long tenantId,
        long actorUserId,
        long issueId,
        CancellationToken cancellationToken = default);
    
    
}