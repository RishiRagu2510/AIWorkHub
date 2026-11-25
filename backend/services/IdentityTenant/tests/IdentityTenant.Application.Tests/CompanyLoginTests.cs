using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Application.Services;
using IdentityTenant.Domain.Common;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;

namespace IdentityTenant.Application.Tests;

public sealed class CompanyLoginTests
{
    [Theory]
    [InlineData("unverified", "EMAIL_NOT_VERIFIED")]
    [InlineData("unknown-domain", "ORGANIZATION_ACCESS_DENIED")]
    [InlineData("suffix-domain", "ORGANIZATION_ACCESS_DENIED")]
    [InlineData("malformed-email", "ORGANIZATION_ACCESS_DENIED")]
    [InlineData("suspended-tenant", "ORGANIZATION_ACCESS_DENIED")]
    [InlineData("disabled-tenant", "ORGANIZATION_ACCESS_DENIED")]
    [InlineData("disabled-provider", "ORGANIZATION_ACCESS_DENIED")]
    [InlineData("suspended-user", "ORGANIZATION_ACCESS_DENIED")]
    [InlineData("disabled-user", "ORGANIZATION_ACCESS_DENIED")]
    [InlineData("cross-tenant-identity", "ORGANIZATION_ACCESS_DENIED")]
    [InlineData("changed-email", "ORGANIZATION_ACCESS_DENIED")]
    [InlineData("missing-subject", "INVALID_GOOGLE_IDENTITY")]
    public async Task RejectedLoginDoesNotWrite(string scenario, string code)
    {
        var store = new Store();
        var email = "rishi@griddynamics.com";
        switch (scenario)
        {
            case "unknown-domain": email = "rishi@gmail.com"; break;
            case "suffix-domain": email = "rishi@griddynamics.com.evil.test"; break;
            case "malformed-email": email = "rishi@@griddynamics.com"; break;
            case "suspended-tenant": store.Tenant.Suspend(); break;
            case "disabled-tenant": store.Tenant.Disable(); break;
            case "disabled-provider": store.Provider = null; break;
            case "no-role": store.Roles = []; break;
            case "missing-default-role": store.User = null; store.DefaultRole = null; store.Invitation = new() {TenantId=1,Email=email,ExpiresAt=DateTimeOffset.UtcNow.AddDays(1)}; break;
            case "suspended-user": store.User!.Suspend(); break;
            case "disabled-user": store.User!.Disable(); break;
            case "cross-tenant-identity":
                store.Link = new UserExternalIdentity(7, 2, "subject");
                store.User = WithId(new User(99, email, "Rishi"), 7); break;
            case "changed-email":
                store.Link = new UserExternalIdentity(7, 2, "subject");
                email = "someoneelse@griddynamics.com"; break;
        }
        var exception = await Assert.ThrowsAsync<LoginRejectedException>(() => store.Service.ProvisionAsync(
            scenario == "missing-subject" ? "" : "subject", email, "Rishi", scenario != "unverified", default, "client"));
        Assert.Equal(code, exception.Code);
        Assert.Equal(0, store.Writes);
        Assert.Null(store.User?.LastLoginAt);
        if (scenario == "unverified") Assert.Equal(0, store.DomainLookups);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingMemberCanLoginWithoutCreatingUserOrAssigningRoles(bool linked)
    {
        var store = new Store();
        if (linked) store.Link = new UserExternalIdentity(7, 2, "subject");
        var result = await store.Service.ProvisionAsync("subject", " RISHI@GridDynamics.COM ", "Rishi", true, default, "client");
        Assert.Equal(1, result.TenantId);
        Assert.Equal(7, result.UserId);
        Assert.True(store.User!.EmailVerified);
        Assert.NotNull(store.User.LastLoginAt);
        Assert.Equal(linked ? 1 : 2, store.Writes);
        Assert.NotNull(store.Link);
    }

    [Fact]
    public async Task NewAccountPersistsWithoutDefaultRoleAndCanRetry()
    {
        var store = new Store { User = null, DefaultRole = null, Roles = [], Invitation = new() {TenantId=1,Email="rishi@griddynamics.com",ExpiresAt=DateTimeOffset.UtcNow.AddDays(1)} };
        var first = await store.Service.ProvisionAsync("subject", "rishi@griddynamics.com", "Rishi", true, default, "client");
        var second = await store.Service.ProvisionAsync("subject", "rishi@griddynamics.com", "Rishi", true, default, "client");
        Assert.Equal(first.UserId, second.UserId);
        Assert.Empty(store.Roles);
        Assert.True(store.User!.EmailVerified);
        Assert.NotNull(store.Link);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-client")]
    public async Task MissingOrWrongClientFailsClosed(string? clientId)
    {
        var store=new Store();
        await Assert.ThrowsAsync<LoginRejectedException>(()=>store.Service.ProvisionAsync("subject","rishi@griddynamics.com","Rishi",true,default,clientId));
        Assert.Equal(0,store.Writes);Assert.Equal(0,store.DomainLookups);
    }
    [Fact]
    public async Task DomainAloneCannotCreateMembership()
    {
        var store=new Store{User=null};
        await Assert.ThrowsAsync<LoginRejectedException>(()=>store.Service.ProvisionAsync("subject","rishi@griddynamics.com","Rishi",true,default,"client"));
        Assert.Equal(0,store.Writes);Assert.Equal(0,store.DomainLookups);
    }
    private static T WithId<T>(T entity, long id) where T : Entity
    {
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, id);
        return entity;
    }

    private sealed class Store : ITenantRepository, IIdentityProviderRepository,
        IUserExternalIdentityRepository, IUserRepository, IUnitOfWork, IUserRoleRepository, IRoleRepository, IMembershipRepository
    {
        public Tenant Tenant = WithId(new Tenant("Grid Dynamics", "grid", "griddynamics.com"), 1);
        public IdentityProvider? Provider = WithId(new IdentityProvider(1, "Google", IdentityProviderType.Google, "client"), 2);
        public User? User = WithId(new User(1, "rishi@griddynamics.com", "Rishi"), 7);
        public UserExternalIdentity? Link;
        public Role? DefaultRole = WithId(new Role(1, "DEVELOPER", "Developer"), 3);
        public Task<Role?> GetByTenantAndCodeAsync(long tenantId, string code, CancellationToken ct) => Task.FromResult(DefaultRole);
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct) => action(ct);
        public IReadOnlyCollection<string> Roles = ["DEVELOPER"];
        public Task<IReadOnlyCollection<string>> GetRoleCodesAsync(long tenantId, long userId, CancellationToken ct) => Task.FromResult(Roles);
        public Task<bool> ExistsAsync(long tenantId, long userId, long roleId, CancellationToken ct) => throw new NotImplementedException();
        public Task AddAsync(UserRole role, CancellationToken ct) => throw new Exception("Login must not grant roles.");
        public TenantInvitation? Invitation;
        public Task LockAsync(CancellationToken ct)=>Task.CompletedTask;
        public Task<List<UserExternalIdentity>> FindGoogleLinksAsync(string subject,CancellationToken ct)=>Task.FromResult(Link is null?new List<UserExternalIdentity>():[Link]);
        public Task<List<User>> FindUsersAsync(string email,CancellationToken ct)=>Task.FromResult(User?.Email==email?new List<User>{User}:[]);
        public Task<bool> HasGoogleIdentityAsync(long userId,CancellationToken ct)=>Task.FromResult(Link is not null);
        public Task<List<TenantInvitation>> FindInvitationsAsync(string email,CancellationToken ct,long? tenantId=null)=>Task.FromResult(Invitation is null?new List<TenantInvitation>():[Invitation]);
        public int Writes;
        public int DomainLookups;
        public UserProvisioningService Service => new(this, this, this, this, this, this);
        public Task<Tenant?> FindByEmailDomainAsync(string domain, CancellationToken ct)
        {
            DomainLookups++;
            return Task.FromResult(domain == Tenant.EmailDomain ? Tenant : null);
        }
        Task<Tenant?> ITenantRepository.GetByIdAsync(long id, CancellationToken ct) => Task.FromResult(id==Tenant.Id?Tenant:null);
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct) => throw new NotImplementedException();
        public Task<List<IdentityProvider>> GetEnabledGoogleByClientIdAsync(string clientId,CancellationToken ct)=>Task.FromResult(Provider is not null&&Provider.Enabled&&Provider.ClientId==clientId?new List<IdentityProvider>{Provider}:[]);
        public Task<IdentityProvider?> GetEnabledByTenantAndTypeAsync(long tenantId, IdentityProviderType type, CancellationToken ct)
            => Task.FromResult(Provider);
        public Task<UserExternalIdentity?> GetByProviderAndSubjectAsync(long id, string subject, CancellationToken ct)
            => Task.FromResult(Link);
        public Task AddAsync(UserExternalIdentity link, CancellationToken ct) { Writes++; Link = link; return Task.CompletedTask; }
        Task<User?> IUserRepository.GetByIdAsync(long id, CancellationToken ct) => Task.FromResult(User);
        public Task<User?> GetByTenantAndEmailAsync(long tenantId, string email, CancellationToken ct)
            => Task.FromResult(User?.Email == email && User.TenantId == tenantId ? User : null);
        public Task AddAsync(User user, CancellationToken ct) { User = WithId(user, 7); return Task.CompletedTask; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { Writes++; return Task.FromResult(1); }
    }
}
