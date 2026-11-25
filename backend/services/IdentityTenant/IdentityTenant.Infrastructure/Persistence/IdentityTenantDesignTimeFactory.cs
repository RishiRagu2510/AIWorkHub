using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IdentityTenant.Infrastructure.Persistence;


public sealed class IdentityTenantDesignTimeFactory : IDesignTimeDbContextFactory<IdentityTenantDbContext>
{
    public IdentityTenantDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<IdentityTenantDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("IDENTITY_TENANT_MIGRATION_CONNECTION")
                ?? "Host=localhost;Database=identity_tenant_db;Username=aiworkhub")
            .Options);
}
