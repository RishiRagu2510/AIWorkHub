using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;
[DbContext(typeof(IdentityTenantDbContext))]
[Migration("20261008000400_HrPersonaPermissions")]
public sealed class HrPersonaPermissions : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.Sql(CareerMigrationSql.Read("022_hr_persona_permissions.sql"));
    protected override void Down(MigrationBuilder builder) => throw new NotSupportedException("Use a reviewed forward migration to change HR access.");
}
