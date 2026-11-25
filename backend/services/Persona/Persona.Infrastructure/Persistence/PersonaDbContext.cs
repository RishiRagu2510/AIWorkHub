using IdentityTenant.Domain.Entities;
using IdentityTenant.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Persona.Domain.Careers;
using Persona.Domain.Employees;

namespace Persona.Infrastructure.Persistence;

public sealed class PersonaDbContext : DbContext
{
    public PersonaDbContext(DbContextOptions<PersonaDbContext> options)
        : base(options)
    {
    }

    public DbSet<CareerProfile> CareerProfiles => Set<CareerProfile>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<CareerLevel> CareerLevels => Set<CareerLevel>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<CareerLevelSkill> CareerLevelSkills => Set<CareerLevelSkill>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeReference> EmployeeReferences => Set<EmployeeReference>();
    public DbSet<EmployeeSkill> EmployeeSkills => Set<EmployeeSkill>();
    public DbSet<EmployeeEducation> EmployeeEducations => Set<EmployeeEducation>();
    public DbSet<EmployeeCertification> EmployeeCertifications => Set<EmployeeCertification>();
    public DbSet<EmployeeExperience> EmployeeExperiences => Set<EmployeeExperience>();
    public DbSet<EmployeeLanguage> EmployeeLanguages => Set<EmployeeLanguage>();
    public DbSet<EmployeeCompensation> EmployeeCompensations => Set<EmployeeCompensation>();

    // Cross-domain entities for user/role queries within the same database
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

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
            typeof(PersonaDbContext).Assembly);

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleConfiguration());
        modelBuilder.ApplyConfiguration(new TeamConfiguration());
        modelBuilder.ApplyConfiguration(new TeamMemberConfiguration());
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new TenantSettingsConfiguration());
    }
}
