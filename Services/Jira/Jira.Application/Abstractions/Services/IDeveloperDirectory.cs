namespace Jira.Application.Abstractions.Services;

public sealed record AssignmentUser(long Id, long TenantId, bool IsAdmin);
public sealed record CollaborationUser(long Id,long TenantId,string[] Roles);
public interface IDeveloperDirectory
{
    Task<CollaborationUser?> GetCollaborationUserAsync(long tenantId,long userId,CancellationToken ct);
    Task<AssignmentUser?> GetAssignmentUserAsync(long tenantId, long userId, CancellationToken ct);
    Task<bool> IsActiveDeveloperAsync(long tenantId, long userId, CancellationToken ct);
}
