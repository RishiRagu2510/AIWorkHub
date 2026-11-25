using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;
[DbContext(typeof(IdentityTenantDbContext))]
[Migration("20261008000700_EmployeeOnboardingLinks")]
public sealed class EmployeeOnboardingLinks:Migration {
 protected override void Up(MigrationBuilder builder)=>builder.Sql(CareerMigrationSql.Read("025_employee_onboarding_links.sql"));
 protected override void Down(MigrationBuilder builder)=>throw new NotSupportedException("Preserve onboarding completion receipts.");
}
