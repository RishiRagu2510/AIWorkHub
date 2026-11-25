using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;
[DbContext(typeof(IdentityTenantDbContext))]
[Migration("20261003000200_SoftwareEngineerTitles")]
public sealed class SoftwareEngineerTitles : Migration
{
 protected override void Up(MigrationBuilder builder) => builder.Sql(CareerMigrationSql.Read("014_software_engineer_titles.sql"));
 protected override void Down(MigrationBuilder builder) => throw new NotSupportedException("Preserve employee title links; use a forward migration.");
}
