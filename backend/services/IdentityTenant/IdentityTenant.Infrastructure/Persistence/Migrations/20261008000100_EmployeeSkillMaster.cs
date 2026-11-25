using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;
[DbContext(typeof(IdentityTenantDbContext))]
[Migration("20261008000100_EmployeeSkillMaster")]
public sealed class EmployeeSkillMaster : Migration
{
 protected override void Up(MigrationBuilder builder) => builder.Sql(CareerMigrationSql.Read("019_employee_skill_master.sql"));
 protected override void Down(MigrationBuilder builder) => throw new NotSupportedException("Preserve employee skill links. Use a reviewed forward migration.");
}
