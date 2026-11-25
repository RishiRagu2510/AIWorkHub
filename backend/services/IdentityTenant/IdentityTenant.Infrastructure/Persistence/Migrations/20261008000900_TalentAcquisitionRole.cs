using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;
[DbContext(typeof(IdentityTenantDbContext))]
[Migration("20261008000900_TalentAcquisitionRole")]
public sealed class TalentAcquisitionRole:Migration {
 protected override void Up(MigrationBuilder builder)=>builder.Sql(CareerMigrationSql.Read("027_talent_acquisition_role.sql"));
 protected override void Down(MigrationBuilder builder)=>throw new NotSupportedException("Use a reviewed forward migration for role permissions.");
}
