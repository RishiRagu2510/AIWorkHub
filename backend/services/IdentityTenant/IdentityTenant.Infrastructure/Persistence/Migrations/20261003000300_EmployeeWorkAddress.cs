using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;
[DbContext(typeof(IdentityTenantDbContext))]
[Migration("20261003000300_EmployeeWorkAddress")]
public sealed class EmployeeWorkAddress : Migration
{
 protected override void Up(MigrationBuilder builder)=>builder.Sql(CareerMigrationSql.Read("015_employee_work_address.sql"));
 protected override void Down(MigrationBuilder builder)=>throw new NotSupportedException("Preserve employee work addresses; use a forward migration.");
}
