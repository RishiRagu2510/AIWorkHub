using System.Security.Claims;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Api.Configuration;
using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Application.Services;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TenantContext;
namespace Backend.Authorization.Tests;
public sealed class PasswordLoginTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private static PasswordLoginService Service(IServiceProvider sp, TestMail mail, TimeProvider? clock = null) => new(
        sp.GetRequiredService<IdentityTenantDbContext>(), sp.GetRequiredService<IMembershipRepository>(),
        sp.GetRequiredService<ITenantRepository>(), sp.GetRequiredService<CompanyPrincipalService>(),
        new PasswordHasher<LocalCredential>(Options.Create(new PasswordHasherOptions {IterationCount=210000})), mail, clock ?? TimeProvider.System);
    private const string Password = "my-long-test-passphrase";
    [Fact]
    public async Task VerificationAllowsSignInBeforeAdminAssignsRole()
    {
        using var scope = fixture.App.Services.CreateScope();
        var sp=scope.ServiceProvider;var db=sp.GetRequiredService<IdentityTenantDbContext>();
        var mail=new TestMail();var service=Service(sp,mail);
        var email="new-"+Guid.NewGuid().ToString("N")+"@company.com";
        await service.RegisterAsync(email,"New teammate",default);
        Assert.Single(mail.Tokens);
        Assert.False(await db.Users.AnyAsync(u=>u.Email==email));
        await Assert.ThrowsAsync<LoginRejectedException>(()=>service.LoginAsync(email,Password,default));
        await service.VerifyAsync(mail.Tokens[0],Password,default);
        var user=await db.Users.SingleAsync(u=>u.Email==email);
        Assert.True(user.EmailVerified);Assert.Equal(1,user.TenantId);
        var credential=await db.LocalCredentials.SingleAsync(c=>c.UserId==user.Id);
        Assert.NotEqual(Password,credential.PasswordHash);
        Assert.False(await db.EmailVerifications.AnyAsync(v=>v.TokenHash==mail.Tokens[0]));
        Assert.False(await db.UserRoles.AnyAsync(r=>r.UserId==user.Id));
        var pending = await service.LoginAsync(email,Password,default);
        Assert.True(pending.Identity!.IsAuthenticated);
        Assert.Empty(pending.FindAll(TenantClaimTypes.Role));
        Assert.Empty(pending.FindAll(TenantClaimTypes.Permission));
        var role = await db.Roles.SingleAsync(r=>r.TenantId==1 && r.Code=="DEVELOPER");
        db.UserRoles.Add(new UserRole(1,user.Id,role.Id));await db.SaveChangesAsync();
        var principal=await service.LoginAsync(email.ToUpperInvariant(),Password,default);
        Assert.Equal("DEVELOPER",principal.FindFirstValue(TenantClaimTypes.Role));
        Assert.Equal("PASSWORD",principal.FindFirstValue("aiworkhub:login_method"));
        var google=await sp.GetRequiredService<CompanyPrincipalService>().SignInGoogleAsync(SecurityFixture.Google(email),default);
        Assert.Equal(user.Id.ToString(),google.FindFirstValue(TenantClaimTypes.UserId));
        await Assert.ThrowsAsync<LoginRejectedException>(()=>service.VerifyAsync(mail.Tokens[0],"replacement-password",default));
        await service.RegisterAsync(email,"Overwrite",default);
        Assert.Single(mail.Tokens);
        await service.LoginAsync(email,Password,default);
        var sessions=new LoginSessionService(db,Options.Create(new LoginSessionOptions()),TimeProvider.System);
        await sessions.CreateAsync(principal,default);
        Assert.Contains(await db.UserSessions.Where(s=>s.UserId==user.Id).ToListAsync(),s=>s.LoginMethod=="PASSWORD");
    }
    [Fact]
    public async Task UnknownDomainsExpiredTokensAndReplayCannotCreateCredentials()
    {
        using var scope=fixture.App.Services.CreateScope();var sp=scope.ServiceProvider;
        var mail=new TestMail();var clock=new TestClock();var service=Service(sp,mail,clock);
        await service.RegisterAsync("outsider@unregistered.invalid","Outsider",default);
        Assert.Empty(mail.Tokens);
        var email="expiry-"+Guid.NewGuid().ToString("N")+"@company.com";
        await service.RegisterAsync(email,"Expiry",default);
        clock.Now=clock.Now.AddMinutes(31);
        await Assert.ThrowsAsync<LoginRejectedException>(()=>service.VerifyAsync(mail.Tokens[0],Password,default));
        Assert.False(await sp.GetRequiredService<IdentityTenantDbContext>().Users.AnyAsync(u=>u.Email==email));
    }
    [Fact]
    public async Task FailedLoginsLockCredentialAndSuspendedUsersCannotLogin()
    {
        using var scope=fixture.App.Services.CreateScope();var sp=scope.ServiceProvider;
        var mail=new TestMail();var clock=new TestClock();var service=Service(sp,mail,clock);
        var email="lock-"+Guid.NewGuid().ToString("N")+"@company.com";
        await service.RegisterAsync(email,"Locked",default);await service.VerifyAsync(mail.Tokens[0],Password,default);
        for(var i=0;i<5;i++) await Assert.ThrowsAsync<LoginRejectedException>(()=>service.LoginAsync(email,"wrong",default));
        await Assert.ThrowsAsync<LoginRejectedException>(()=>service.LoginAsync(email,Password,default));
        clock.Now=clock.Now.AddMinutes(16);
        var pending = await service.LoginAsync(email,Password,default);
        Assert.True(pending.Identity!.IsAuthenticated);
        Assert.Empty(pending.FindAll(TenantClaimTypes.Role));
        Assert.Empty(pending.FindAll(TenantClaimTypes.Permission));
        var db=sp.GetRequiredService<IdentityTenantDbContext>();var user=await db.Users.SingleAsync(u=>u.Email==email);
        user.Suspend();await db.SaveChangesAsync();
        await Assert.ThrowsAsync<LoginRejectedException>(()=>service.LoginAsync(email,Password,default));
    }
    [Fact]
    public async Task MailFailureIsNotSuccessAndLeavesRegistrationRetryable()
    {
        using var scope=fixture.App.Services.CreateScope();var sp=scope.ServiceProvider;
        var mail=new TestMail {Fail=true};var service=Service(sp,mail);
        var email="mail-"+Guid.NewGuid().ToString("N")+"@company.com";
        await Assert.ThrowsAsync<System.Net.Mail.SmtpException>(()=>service.RegisterAsync(email,"Mail",default));
        Assert.False(await sp.GetRequiredService<IdentityTenantDbContext>().EmailVerifications.AnyAsync(v=>v.Email==email));
        mail.Fail=false;await service.RegisterAsync(email,"Mail",default);Assert.Single(mail.Tokens);
    }
    private sealed class TestMail:IVerificationEmailSender
    {
        public bool IsConfigured=>true;public bool Fail;public List<string> Tokens=new();
        public Task SendAsync(string email,string token,CancellationToken ct){if(Fail)throw new System.Net.Mail.SmtpException("test failure");Tokens.Add(token);return Task.CompletedTask;}
    }
    private sealed class TestClock:TimeProvider {public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
}
