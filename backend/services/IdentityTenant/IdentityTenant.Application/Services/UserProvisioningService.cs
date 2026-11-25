using System.Net.Mail;
using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Application.Abstractions.Services;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;
namespace IdentityTenant.Application.Services;

public sealed class UserProvisioningService(
 ITenantRepository tenantRepository,IIdentityProviderRepository identityProviderRepository,
 IUserExternalIdentityRepository externalIdentityRepository,IUserRepository userRepository,
 IUnitOfWork unitOfWork,
 IMembershipRepository memberships):IUserProvisioningService
{
 public async Task<UserProvisioningResult> ProvisionAsync(string providerExternalSubject,string email,string displayName,bool emailVerified,CancellationToken cancellationToken,string? clientId=null){
  if(!emailVerified)throw new LoginRejectedException("EMAIL_NOT_VERIFIED","Your Google email address could not be verified.");
  if(string.IsNullOrWhiteSpace(providerExternalSubject))throw new LoginRejectedException("INVALID_GOOGLE_IDENTITY","Google identity could not be verified.");
  var normalized=email?.Trim().ToLowerInvariant();
  if(!MailAddress.TryCreate(normalized,out var address)||address.Address!=normalized||normalized.Count(c=>c=='@')!=1)throw Denied();
  UserProvisioningResult? result=null;
  await unitOfWork.ExecuteInTransactionAsync(async ct=>{
   await memberships.LockAsync(ct);
   if(string.IsNullOrWhiteSpace(clientId))throw Denied();
   var providers=await identityProviderRepository.GetEnabledGoogleByClientIdAsync(clientId,ct);
   if(providers.Count>1)throw Ambiguous();
   var provider=providers.SingleOrDefault()??throw Denied();
   var tenant=await tenantRepository.GetByIdAsync(provider.TenantId,ct);
   if(tenant is null||tenant.Status!=TenantStatus.Active)throw Denied();
   var mappedLink=await externalIdentityRepository.GetByProviderAndSubjectAsync(provider.Id,providerExternalSubject,ct);
   var links=mappedLink is null?new List<UserExternalIdentity>():[mappedLink];
   if(links.Count>1)throw Ambiguous();
   var link=links.SingleOrDefault();User? user;
   if(link is not null){user=await userRepository.GetByIdAsync(link.UserId,ct);if(user is null)throw Denied();}
   else{
    var member=await userRepository.GetByTenantAndEmailAsync(provider.TenantId,normalized,ct);
    var candidates=member is null?new List<User>():[member];
    if(candidates.Count>1)throw Ambiguous();user=candidates.SingleOrDefault();
    
    if(user is not null&&await memberships.HasGoogleIdentityAsync(user.Id,ct))throw Denied();
   }
   TenantInvitation? invitation=null;
   if(user is null){
    var invitations=await memberships.FindInvitationsAsync(normalized,ct,provider.TenantId);
    invitation=invitations.SingleOrDefault(i=>i.TenantId==provider.TenantId);
    if(invitation is null)throw Denied();
   }
   var tenantId=provider.TenantId;
   if(user is not null&&user.TenantId!=tenantId||link is not null&&link.IdentityProviderId!=provider.Id)throw Denied();
   if(user is null){
    if(invitation is not null&&invitation.RoleCode!="DEVELOPER")throw Denied();
    user=new User(tenantId,normalized,string.IsNullOrWhiteSpace(displayName)?normalized:displayName);
    await userRepository.AddAsync(user,ct);await unitOfWork.SaveChangesAsync(ct);
    if(invitation is not null)invitation.AcceptedAt=DateTimeOffset.UtcNow;
   }else{
    if(user.Status!=UserStatus.Active||!string.Equals(user.Email,normalized,StringComparison.OrdinalIgnoreCase))throw Denied();
   }
   if(link is null)await externalIdentityRepository.AddAsync(new UserExternalIdentity(user.Id,provider.Id,providerExternalSubject,normalized),ct);
   if(!user.EmailVerified)user.VerifyEmail();
   if(!string.IsNullOrWhiteSpace(displayName)&&user.DisplayName!=displayName)user.UpdateDisplayName(displayName);
   user.RecordLogin();await unitOfWork.SaveChangesAsync(ct);
   result=new(user.Id,user.TenantId,user.Email,user.DisplayName);
  },cancellationToken);
  return result!;
 }
 private static LoginRejectedException Denied()=>new("ORGANIZATION_ACCESS_DENIED","Your Google account does not have access to an active registered organization. Ask an administrator for an invitation to the organization configured for this Google login.");
 private static LoginRejectedException Ambiguous()=>new("ORGANIZATION_SELECTION_REQUIRED","This account belongs to multiple organizations. Contact an administrator to resolve its membership.");
}
