using IdentityTenant.Domain.Entities;
namespace IdentityTenant.Application.Abstractions.Persistence;
public interface IMembershipRepository
{
 Task LockAsync(CancellationToken ct);
 Task<List<UserExternalIdentity>> FindGoogleLinksAsync(string subject,CancellationToken ct);
 Task<List<User>> FindUsersAsync(string email,CancellationToken ct);
 Task<bool> HasGoogleIdentityAsync(long userId,CancellationToken ct);
 Task<List<TenantInvitation>> FindInvitationsAsync(string email,CancellationToken ct,long? tenantId=null);
}
