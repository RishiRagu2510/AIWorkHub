namespace IdentityTenant.Application.Abstractions.Persistence;

public sealed record DeveloperDto(long Id, long TenantId, string Email, string DisplayName);
public sealed record DeveloperPage(IReadOnlyList<DeveloperDto> Items, int TotalCount, int Page, int PageSize);
public sealed record AssignmentUserDto(long Id, long TenantId, bool IsAdmin, string? DisplayName = null);
public sealed record CollaborationUserDto(long Id,long TenantId,string[] Roles);
public interface IUserDirectory
{
    Task<CollaborationUserDto?> GetCollaborationUserAsync(long tenantId,long userId,CancellationToken ct);
    Task<AssignmentUserDto?> GetAssignmentUserAsync(long tenantId, long userId, CancellationToken ct);
    Task<DeveloperPage> ListDevelopersAsync(long tenantId, int page, int pageSize, CancellationToken ct);
    Task<DeveloperDto?> GetDeveloperAsync(long tenantId, long userId, CancellationToken ct);
}
