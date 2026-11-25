using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;
[DbContext(typeof(IdentityTenantDbContext))]
[Migration("20261008000300_NormalizeLegacyEmployeeSkills")]
public sealed class NormalizeLegacyEmployeeSkills : Migration
{
 protected override void Up(MigrationBuilder builder) => builder.Sql(CareerMigrationSql.Read("021_normalize_legacy_employee_skills.sql"));
 protected override void Down(MigrationBuilder builder) => throw new NotSupportedException("Preserve normalized employee skills. Use a reviewed forward migration.");
}
