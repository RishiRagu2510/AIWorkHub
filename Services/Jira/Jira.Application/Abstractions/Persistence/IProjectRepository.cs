using Jira.Domain.Entities;

namespace Jira.Application.Abstractions.Persistence;

public interface IProjectRepository
{
    Task<Project> UpdateAsync(long tenantId, long projectId, string name, string? description,
        Jira.Domain.Enums.ProjectType type, Jira.Domain.Enums.ProjectStatus? status, CancellationToken ct, long? accountId = null, string? displayName = null);
    Task<Project> MoveAsync(long tenantId, long projectId, long? parentProjectId, CancellationToken ct);
    Task DeleteAsync(long tenantId, long projectId, CancellationToken ct);

    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Project>> GetChildrenAsync(long tenantId, long parentProjectId, CancellationToken cancellationToken);

    Task<Project?> GetByIdAsync(
        long tenantId,
        long projectId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Project>> GetAllAsync(
        long tenantId,
        CancellationToken cancellationToken);
}