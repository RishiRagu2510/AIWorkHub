using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;
[DbContext(typeof(IdentityTenantDbContext))]
[Migration("20261015000100_EmployeePersonalDetails")]
public sealed class EmployeePersonalDetails : Migration
{
 protected override void Up(MigrationBuilder builder)=>builder.Sql(CareerMigrationSql.Read("028_employee_personal_details.sql"));
 protected override void Down(MigrationBuilder builder)=>throw new NotSupportedException("Preserve employee personal details; use a forward migration.");
}
