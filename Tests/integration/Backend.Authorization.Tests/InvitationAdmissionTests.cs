using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Application.Services;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TenantContext;
namespace Backend.Authorization.Tests;
public sealed class InvitationAdmissionTests(SecurityFixture fixture):IClassFixture<SecurityFixture>
{
 private async Task<HttpResponseMessage> Send(string method,string path,object? body=null,long user=100,long tenant=1,string role="ADMIN"){
  using var request=new HttpRequestMessage(new HttpMethod(method),path);request.Headers.Authorization=new("Bearer",fixture.Token(user,tenant,role));if(body is not null)request.Content=JsonContent.Create(body);return await fixture.Client.SendAsync(request);
 }
 private async Task<ClaimsPrincipal> Login(string email,string? subject=null){using var scope=fixture.App.Services.CreateScope();var google=SecurityFixture.Google(email);if(subject is not null){var identity=(ClaimsIdentity)google.Identity!;foreach(var claim in identity.Claims.Where(c=>c.Type==ClaimTypes.NameIdentifier||c.Type=="sub").ToArray())identity.RemoveClaim(claim);identity.AddClaim(new("sub",subject));}return await scope.ServiceProvider.GetRequiredService<CompanyPrincipalService>().SignInGoogleAsync(google,default);}
 [Fact] public async Task RegisteredDomainCannotAdmitAnUninvitedAccount(){
  await Assert.ThrowsAsync<LoginRejectedException>(()=>Login("autojoin@company.com"));
  using var scope=fixture.App.Services.CreateScope();
  Assert.False(await scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>().Users.AnyAsync(u=>u.Email=="autojoin@company.com"));
 }
 [Fact] public async Task GoogleClientCannotAdmitAnotherProvidersMember(){
  await Assert.ThrowsAsync<LoginRejectedException>(()=>Login("user200@other.com"));
 }
 [Theory]
 [InlineData("unknown@gmail.com")]
 [InlineData("unknown@company.com.evil.test")]
 [InlineData("unknown@sub.company.com")]
 public async Task UnregisteredDomainCannotAutoJoin(string email){
  var error=await Assert.ThrowsAsync<LoginRejectedException>(()=>Login(email));Assert.Equal("ORGANIZATION_ACCESS_DENIED",error.Code);
  using var scope=fixture.App.Services.CreateScope();Assert.False(await scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>().Users.AnyAsync(u=>u.Email==email));
 }
 [Fact] public async Task DisabledTenantCannotAutoEnroll(){
  using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
  var tenant=await db.Tenants.SingleAsync(t=>t.Id==1);tenant.Suspend();await db.SaveChangesAsync();
  try{await Assert.ThrowsAsync<LoginRejectedException>(()=>Login("blocked-autojoin@company.com"));Assert.False(await db.Users.AnyAsync(u=>u.Email=="blocked-autojoin@company.com"));}
  finally{tenant.Activate();await db.SaveChangesAsync();}
 }
 [Fact] public async Task InviteExternalEmailAcceptOnceAndPreserveLinkedSubject(){
  const string email="invited-contractor@gmail.com";
  using var invitation=await Send("POST","/api/invitations",new{email});Assert.Equal(HttpStatusCode.Created,invitation.StatusCode);
  var pending=await Login(email,"google-stable-subject");
  Assert.True(pending.Identity!.IsAuthenticated);Assert.Empty(pending.FindAll(TenantClaimTypes.Role));
  using(var setupScope=fixture.App.Services.CreateScope()){
   var setupDb=setupScope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
   var user=await setupDb.Users.SingleAsync(u=>u.Email==email);
   Assert.False(await setupDb.UserRoles.AnyAsync(r=>r.UserId==user.Id));
   var role=await setupDb.Roles.SingleAsync(r=>r.TenantId==1&&r.Code=="DEVELOPER");
   setupDb.UserRoles.Add(new UserRole(1,user.Id,role.Id));await setupDb.SaveChangesAsync();
  }
  using(var refreshScope=fixture.App.Services.CreateScope()){
   var refreshed=await refreshScope.ServiceProvider.GetRequiredService<CompanyPrincipalService>().RefreshAsync(pending,default);
   Assert.NotNull(refreshed);Assert.Equal("DEVELOPER",refreshed.FindFirstValue(TenantClaimTypes.Role));
  }
  var first=await Login(email,"google-stable-subject");var second=await Login(email,"google-stable-subject");
  Assert.Equal(first.FindFirstValue(TenantClaimTypes.UserId),second.FindFirstValue(TenantClaimTypes.UserId));
  await Assert.ThrowsAsync<LoginRejectedException>(()=>Login(email,"different-subject"));
  using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();Assert.NotNull((await db.Set<TenantInvitation>().SingleAsync(i=>i.Email==email)).AcceptedAt);Assert.Single(await db.Users.Where(u=>u.Email==email).ToListAsync());
  Assert.NotNull(await scope.ServiceProvider.GetRequiredService<CompanyPrincipalService>().RefreshAsync(first,default));
 }
 [Fact] public async Task RevokeAndExpiredInvitationsCannotAdmitUsers(){
  const string email="revoked-invite@gmail.com";using var created=await Send("POST","/api/invitations",new{email});var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  using var wrong=await Send("DELETE",$"/api/invitations/{id}",user:200,tenant:2);Assert.Equal(HttpStatusCode.NotFound,wrong.StatusCode);
  using var revoked=await Send("DELETE",$"/api/invitations/{id}");Assert.Equal(HttpStatusCode.NoContent,revoked.StatusCode);await Assert.ThrowsAsync<LoginRejectedException>(()=>Login(email));
  using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();db.Add(new TenantInvitation{TenantId=1,Email="expired@gmail.com",ExpiresAt=DateTimeOffset.UtcNow.AddSeconds(-1),InvitedByUserId=100});await db.SaveChangesAsync();
  await Assert.ThrowsAsync<LoginRejectedException>(()=>Login("expired@gmail.com"));
 }
 [Fact] public async Task InvitationApisRequireActualTenantAdminAndNeverTrustBodyTenant(){
  using var forbidden=await Send("POST","/api/invitations",new{email="developer-invite@gmail.com"},user:101,role:"DEVELOPER");Assert.Equal(HttpStatusCode.Forbidden,forbidden.StatusCode);
  using var forged=await Send("POST","/api/invitations",new{email="forged-invite@gmail.com"},user:101);Assert.Equal(HttpStatusCode.Forbidden,forged.StatusCode);
  using var invite=await Send("POST","/api/invitations",new{email="scoped-invite@gmail.com",tenantId=2,roleCode="ADMIN"});Assert.Equal(HttpStatusCode.Created,invite.StatusCode);
  var data=await invite.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(1,data.GetProperty("tenantId").GetInt64());Assert.Equal("DEVELOPER",data.GetProperty("roleCode").GetString());
  using var listing=await Send("GET","/api/invitations",user:200,tenant:2);var rows=(await listing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");Assert.All(rows.EnumerateArray(),row=>Assert.Equal(2,row.GetProperty("tenantId").GetInt64()));
 }
 [Fact] public async Task ExistingMemberAndCookieDoNotDependOnDomainMetadata(){
  using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
  await db.Database.ExecuteSqlRawAsync("UPDATE tenants SET email_domain=NULL WHERE id=1");
  try{var principal=await Login("user101@company.com");Assert.Equal("1",principal.FindFirstValue(TenantClaimTypes.TenantId));Assert.NotNull(await scope.ServiceProvider.GetRequiredService<CompanyPrincipalService>().RefreshAsync(principal,default));}
  finally{await db.Database.ExecuteSqlRawAsync("UPDATE tenants SET email_domain='company.com' WHERE id=1");}
 }
 [Fact] public async Task ConcurrentAcceptanceCreatesOneMemberAndOneIdentity(){
  const string email="parallel-invite@gmail.com";using var invite=await Send("POST","/api/invitations",new{email});Assert.Equal(HttpStatusCode.Created,invite.StatusCode);
  var principals=await Task.WhenAll(Login(email),Login(email));Assert.All(principals,p=>Assert.Empty(p.FindAll(TenantClaimTypes.Role)));
  using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();Assert.Equal(1,await db.Users.CountAsync(u=>u.Email==email));Assert.Equal(1,await db.UserExternalIdentities.CountAsync(i=>i.Email==email));
 }
}
