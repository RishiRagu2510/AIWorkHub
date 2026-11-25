using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;
[DbContext(typeof(IdentityTenantDbContext))]
[Migration("20261003000100_EmployeeCareerHistory")]
public sealed class EmployeeCareerHistory : Migration
{
 protected override void Up(MigrationBuilder builder) => builder.Sql(CareerMigrationSql.Read("013_employee_career_history.sql"));
 protected override void Down(MigrationBuilder builder) => throw new NotSupportedException("Career history must be preserved. Use a reviewed forward migration.");
}
