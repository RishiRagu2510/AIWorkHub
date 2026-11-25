using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IdentityTenant.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityTenantDbContext))]
[Migration("20261008000600_CleanupOrganizationSetupData")]
public sealed class CleanupOrganizationSetupData : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.Sql(CareerMigrationSql.Read("024_cleanup_organization_setup_data.sql"));
    protected override void Down(MigrationBuilder builder) => throw new NotSupportedException("Cleanup migration is a forward-only reset.");
}
