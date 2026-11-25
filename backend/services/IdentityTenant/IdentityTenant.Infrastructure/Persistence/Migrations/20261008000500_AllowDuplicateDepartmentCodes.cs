using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IdentityTenant.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityTenantDbContext))]
[Migration("20261008000500_AllowDuplicateDepartmentCodes")]
public sealed class AllowDuplicateDepartmentCodes : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.Sql(CareerMigrationSql.Read("023_allow_duplicate_department_codes.sql"));
    protected override void Down(MigrationBuilder builder) => throw new NotSupportedException("Use a reviewed forward migration to alter department index constraints.");
}
