using Microsoft.EntityFrameworkCore.Migrations;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;
public partial class ExistingIdentityTenantBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        
        
        var files = new[] { "001_schema.sql", "002_seed.sql", "003_company_email_domain.sql",
            "004_tenant_invitations.sql", "005_login_sessions.sql", "006_password_login.sql",
            "007_timesheet_permissions.sql", "008_developer_issue_assignment.sql",
            "009_employees.sql", "010_employee_profile_details.sql" };
        var commands = string.Join("\n", files.Select(name =>
            "EXECUTE $baseline_sql$" + CareerMigrationSql.Read(name) + "$baseline_sql$;"));
        migrationBuilder.Sql("DO $baseline$ BEGIN IF to_regclass('public.tenants') IS NULL THEN "
            + commands + " END IF; END $baseline$;");
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("The adopted IdentityTenant baseline cannot be rolled back destructively.");
}
