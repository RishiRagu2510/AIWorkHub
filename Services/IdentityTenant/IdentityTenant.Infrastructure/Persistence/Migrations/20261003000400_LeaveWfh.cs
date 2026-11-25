using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;
[DbContext(typeof(IdentityTenantDbContext))]
[Migration("20261003000400_LeaveWfh")]
public sealed class LeaveWfh : Migration
{
 protected override void Up(MigrationBuilder builder)=>builder.Sql(CareerMigrationSql.Read("016_leave_wfh.sql"));
 protected override void Down(MigrationBuilder builder)=>throw new NotSupportedException("Preserve leave history; use a forward migration.");
}
