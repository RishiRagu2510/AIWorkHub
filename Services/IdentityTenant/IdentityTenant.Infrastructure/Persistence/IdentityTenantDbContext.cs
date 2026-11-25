using IdentityTenant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using IdentityTenant.Application.Abstractions.Persistence;

namespace IdentityTenant.Infrastructure.Persistence;

public sealed class IdentityTenantDbContext : DbContext , IUnitOfWork
{
    public IdentityTenantDbContext(
        DbContextOptions<IdentityTenantDbContext> options)
        : base(options)
    {
    }

    public DbSet<IdentityTenant.Domain.Careers.CareerProfile> CareerProfiles => Set<IdentityTenant.Domain.Careers.CareerProfile>();
    public DbSet<IdentityTenant.Domain.Careers.Grade> Grades => Set<IdentityTenant.Domain.Careers.Grade>();
    public DbSet<IdentityTenant.Domain.Careers.CareerLevel> CareerLevels => Set<IdentityTenant.Domain.Careers.CareerLevel>();
    public DbSet<IdentityTenant.Domain.Careers.Skill> Skills => Set<IdentityTenant.Domain.Careers.Skill>();
    public DbSet<IdentityTenant.Domain.Careers.CareerLevelSkill> CareerLevelSkills => Set<IdentityTenant.Domain.Careers.CareerLevelSkill>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();

    public DbSet<LocalCredential> LocalCredentials => Set<LocalCredential>();
    public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();

    public DbSet<User> Users => Set<User>();

    public DbSet<IdentityProvider> IdentityProviders =>
        Set<IdentityProvider>();

    public DbSet<UserExternalIdentity> UserExternalIdentities =>
        Set<UserExternalIdentity>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions =>
        Set<RolePermission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(IdentityTenantDbContext).Assembly);
    }
}