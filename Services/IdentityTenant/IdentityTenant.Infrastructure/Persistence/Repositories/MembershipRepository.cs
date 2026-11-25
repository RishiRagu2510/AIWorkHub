using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace IdentityTenant.Infrastructure.Persistence.Repositories;
public sealed class MembershipRepository(IdentityTenantDbContext db):IMembershipRepository
{
 
 public async Task LockAsync(CancellationToken ct)=>await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1948401201)",ct);
 public Task<List<UserExternalIdentity>> FindGoogleLinksAsync(string subject,CancellationToken ct)=>
  db.UserExternalIdentities.Where(l=>l.ExternalSubject==subject&&db.IdentityProviders.Any(p=>p.Id==l.IdentityProviderId&&p.ProviderType==IdentityProviderType.Google)).Take(2).ToListAsync(ct);
 public Task<List<User>> FindUsersAsync(string email,CancellationToken ct)=>db.Users.Where(u=>u.Email.ToLower()==email).Take(2).ToListAsync(ct);
 public Task<bool> HasGoogleIdentityAsync(long userId,CancellationToken ct)=>db.UserExternalIdentities.AnyAsync(l=>l.UserId==userId&&db.IdentityProviders.Any(p=>p.Id==l.IdentityProviderId&&p.ProviderType==IdentityProviderType.Google),ct);
 public Task<List<TenantInvitation>> FindInvitationsAsync(string email,CancellationToken ct,long? tenantId=null)=>db.Set<TenantInvitation>().Where(i=>(tenantId==null||i.TenantId==tenantId)&&i.Email==email&&i.AcceptedAt==null&&i.RevokedAt==null&&i.ExpiresAt>DateTimeOffset.UtcNow).Take(2).ToListAsync(ct);
}
