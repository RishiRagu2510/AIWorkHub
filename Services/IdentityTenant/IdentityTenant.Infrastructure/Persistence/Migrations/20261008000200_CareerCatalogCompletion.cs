using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;
[DbContext(typeof(IdentityTenantDbContext))]
[Migration("20261008000200_CareerCatalogCompletion")]
public sealed class CareerCatalogCompletion : Migration
{
 protected override void Up(MigrationBuilder builder) {
  builder.Sql(CareerMigrationSql.Read("020_career_catalog_completion.sql"));
  builder.Sql(CareerMigrationSql.Read("018_setup_sequences.sql"));
 }
 protected override void Down(MigrationBuilder builder) => throw new NotSupportedException("Preserve career references. Use a reviewed forward migration.");
}
