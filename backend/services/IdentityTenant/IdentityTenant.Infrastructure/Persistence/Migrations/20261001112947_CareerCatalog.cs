using Microsoft.EntityFrameworkCore.Migrations;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;
public partial class CareerCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(CareerMigrationSql.Read("011_career_catalog.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Career catalogue rollback would delete master data and employee links. Use a reviewed forward migration.");
}
