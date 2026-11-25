using System.Security.Claims;
using Authentication;
using Jira.Api.Collaboration;
using Jira.Api.Notifications;
using Authorization;
using Audit.Application.Abstractions;
using Audit.Application.Services;
using Audit.Infrastructure.Persistence;
using Audit.Infrastructure.Persistence.Repositories;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Application.Abstractions.Services;
using IdentityTenant.Application.Plans;
using IdentityTenant.Application.Services;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;
using IdentityTenant.Infrastructure.Persistence;
using IdentityTenant.Infrastructure.Persistence.Repositories;
using Jira.Application.Abstractions.Persistence;
using Jira.Application.Abstractions.Services;
using Jira.Application.Services;
using Jira.Domain.Entities;
using Jira.Domain.Enums;
using Jira.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence.Outbox;
using Jira.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenantContext;
using Testcontainers.PostgreSql;

namespace Backend.Authorization.Tests;

public sealed class SecurityFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    public WebApplication App { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public static readonly string[] Permissions = ["ISSUE_VIEW", "ISSUE_CREATE", "ISSUE_UPDATE", "ISSUE_ASSIGN", "ISSUE_TRANSITION", "ISSUE_DELETE", "ISSUE_COMMENT_CREATE", "PROJECT_VIEW", "TENANT_VIEW", "AUDIT_VIEW", "USER_VIEW", "PROJECT_MANAGE", "PROJECT_DELETE", "WORKLOG_VIEW", "WORKLOG_CREATE", "WORKLOG_UPDATE", "WORKLOG_DELETE", "TIMESHEET_VIEW", "TIMESHEET_TEAM_VIEW", "TIMESHEET_APPROVE", "EMPLOYEE_VIEW", "EMPLOYEE_CREATE", "EMPLOYEE_UPDATE", "EMPLOYEE_DELETE", "EMPLOYEE_VIEW_SENSITIVE", "EMPLOYEE_MANAGE_ORG", "EMPLOYEE_VIEW_TEAM", "EMPLOYEE_UPDATE_SELF"];

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Google:ClientId"] = "client",
            ["Platform:OperatorKey"] = "operator-test-key",
            ["Services:OnboardingApprovalKey"] = "test-onboarding-approval-key-0123456789abcdef",
            ["Jwt:Issuer"] = "authorization-tests", ["Jwt:Audience"] = "authorization-tests",
            ["Jwt:SigningKey"] = "test-only-signing-key-at-least-thirty-two-bytes-long"
        });
        var services = builder.Services;
        services.AddControllers(options => { options.Filters.Add<IdempotencyFilter>(); options.Filters.Add<Jira.Api.Middleware.WritePreconditionFilter>(); }).AddApplicationPart(typeof(Jira.Api.Controllers.IssuesController).Assembly)
            .AddApplicationPart(typeof(Audit.Api.Controllers.AuditLogsController).Assembly)
            .AddApplicationPart(typeof(IdentityTenant.Api.Controllers.TenantsController).Assembly)
            .AddApplicationPart(typeof(Persona.Api.Controllers.EmployeesController).Assembly);
        services.AddDbContext<Persona.Infrastructure.Persistence.PersonaDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddAiWorkHubJwtAuthentication(builder.Configuration);
        services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
        services.AddAuthorization(); services.AddPermissionAuthorization();
        services.AddScoped<TenantContext.TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext.TenantContext>());
        services.AddDbContext<JiraDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString()));
        services.AddDbContext<IdentityTenantDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString()));
        services.AddDbContext<AuditDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString()));
        services.AddJiraNotifications();
        services.AddCollaboration(builder.Configuration, background:false);
        services.AddSingleton<Jira.Api.Collaboration.IAttachmentStorage, FakeAttachmentStorage>();
        services.AddSingleton<IPresenceStore,TestPresenceStore>();
        services.AddScoped<Jira.Infrastructure.Persistence.Timesheets.TimesheetService>();
        services.AddHttpClient<ITimesheetApproverDirectory,Jira.Api.Integrations.IdentityTenant.TimesheetApproverDirectory>(client=>client.BaseAddress=new Uri("http://localhost/"))
            .ConfigurePrimaryHttpMessageHandler(()=>App.GetTestServer().CreateHandler());
        services.AddScoped<JiraResourceScope>();
        services.AddScoped<WritePrecondition>();
        services.AddScoped<ProjectAuditWriter>();
        services.AddScoped<ProjectMembershipService>();
        services.AddScoped<Jira.Infrastructure.Persistence.Accounts.AccountService>();
        services.AddScoped<Jira.Infrastructure.Persistence.Allocations.AllocationService>();
        services.AddScoped<Jira.Infrastructure.Persistence.Preferences.PreferenceService>();
        services.AddScoped<Jira.Api.Services.Onboarding.EmployeeRequestService>();
        services.AddScoped<Persona.Api.Services.Employees.AbsenceService>();
        services.AddScoped<Persona.Api.Services.Employees.EmployeePhotoService>();
        services.AddScoped<Persona.Api.Services.Employees.EmployeeOnboardingService>();
        services.AddScoped<IdentityTenant.Api.Services.Invitations.InvitationService>();
        services.AddScoped<IdentityTenant.Api.Services.Timesheets.TimesheetApproverService>();
        services.AddScoped<IAssigneeValidator>(sp => sp.GetRequiredService<ProjectMembershipService>());
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddHttpContextAccessor();
        services.AddScoped<Persona.Api.Services.Employees.EmployeeAccess>();
        services.AddScoped<Persona.Api.Services.Employees.EmployeeOnboardingValidation>();
        services.AddOnboardingApproval(builder.Configuration);
        services.AddSingleton<IdentityTenant.Api.Authentication.OperatorActionTokens>();
        services.AddSingleton<RecordingAccessRequestNotifier>();
        services.AddScoped<ITenantPlanStore, TenantPlanStore>();
        services.AddScoped<TenantPlanService>();
        services.AddSingleton<IAccessRequestNotifier>(sp => sp.GetRequiredService<RecordingAccessRequestNotifier>());
        services.AddHttpClient<Jira.Api.Onboarding.EmployeeOnboardingClient>(c=>c.BaseAddress=new Uri("http://localhost/"))
            .ConfigurePrimaryHttpMessageHandler(()=>App.GetTestServer().CreateHandler());
        services.AddScoped<Persona.Api.Services.Employees.EmployeeEvents>();
        services.AddScoped<Persona.Api.Services.Employees.EmployeeSetupService>();
        services.AddScoped<Persona.Api.Services.Employees.EmployeeService>();
        services.AddScoped<Persona.Api.Services.Employees.EmployeeCareerService>();
        services.AddSingleton<Persona.Api.Services.Employees.IEmployeePhotoStorage,TestPhotoStorage>();
        services.AddScoped<Persona.Api.Services.Employees.EmployeeCareerHistory>();
        services.AddHttpClient<IDeveloperDirectory, Jira.Api.Integrations.IdentityTenant.DeveloperDirectoryClient>(client =>
            client.BaseAddress = new Uri("http://localhost/"))
            .ConfigurePrimaryHttpMessageHandler(() => App.GetTestServer().CreateHandler());
        services.AddScoped<IIssueRepository, IssueRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IIssueCommentRepository, IssueCommentRepository>();
        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<IIssueCommentService, IssueCommentService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<IJiraUnitOfWork, JiraUnitOfWork>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserPermissionRepository, UserPermissionRepository>();
        services.AddScoped<IIdentityProviderRepository, IdentityProviderRepository>();
        services.AddScoped<IUserExternalIdentityRepository, UserExternalIdentityRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityTenantDbContext>());
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<CompanyPrincipalService>();
        services.AddScoped<TenantService>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<AuditLogService>();
        App = builder.Build();
        App.UseMiddleware<Jira.Api.Middleware.ExceptionHandlingMiddleware>();
        App.UseAuthentication();
        App.UseMiddleware<Jira.Api.Middleware.TenantContextMiddleware>();
        App.UseAuthorization(); App.MapControllers();
        App.MapHub<CollaborationHub>("/hubs/issues", options => options.CloseOnAuthenticationExpiration = true);
        App.MapHub<CollaborationHub>("/hubs/boards/{boardId:long}", options => options.CloseOnAuthenticationExpiration = true);
        App.MapHub<NotificationsHub>("/hubs/notifications", options => options.CloseOnAuthenticationExpiration = true);
        await SeedAsync();
        await App.StartAsync();
        Client = App.GetTestClient();
    }

    private async Task SeedAsync()
    {
        using var scope = App.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
        var jira = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        foreach (var db in new DbContext[] { identity, jira, audit })
            await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        var employeeDdl=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"009_employees.sql")).Split("INSERT INTO permissions")[0]+"COMMIT;";
        await identity.Database.ExecuteSqlRawAsync(employeeDdl);
        await identity.Database.ExecuteSqlRawAsync(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"013_employee_career_history.sql")));
        await identity.Database.ExecuteSqlRawAsync(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"016_leave_wfh.sql")));
        using(var migration=typeof(IdentityTenantDbContext).Assembly.GetManifestResourceStream("IdentityTenant.Sql.025_employee_onboarding_links.sql")!)
        using(var reader=new StreamReader(migration))await identity.Database.ExecuteSqlRawAsync(await reader.ReadToEndAsync());
        
        await identity.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX ux_tenants_email_domain ON tenants (LOWER(email_domain))");
        identity.Tenants.AddRange(Id(new Tenant("Company", "company", "company.com"), 1), Id(new Tenant("Other", "other", "other.com"), 2));
        identity.Skills.Add(new IdentityTenant.Domain.Careers.Skill{Code="CSHARP",Name="CSharp",Category="Backend"});
        identity.IdentityProviders.AddRange(Id(new IdentityProvider(1, "Google", IdentityProviderType.Google, "client"), 1), Id(new IdentityProvider(2, "Google", IdentityProviderType.Google, "other-client"), 2));
        identity.Roles.AddRange(Id(new Role(1, "ADMIN", "Admin"), 1), Id(new Role(1, "DEVELOPER", "Developer"), 2), Id(new Role(2, "ADMIN", "Admin"), 3));
        foreach (var id in new long[] {100,101,102,103,104,200})
            identity.Users.Add(Id(new User(id == 200 ? 2 : 1, $"user{id}@{(id == 200 ? "other" : "company")}.com", $"User {id}"), id));
        for (int i = 0; i < Permissions.Length; i++) identity.Permissions.Add(Id(new Permission(Permissions[i], Permissions[i]), i + 1));
        await identity.SaveChangesAsync();
        identity.UserRoles.AddRange(new UserRole(1,100,1), new UserRole(1,101,2), new UserRole(1,102,2), new UserRole(2,200,3));
        for (int i = 0; i < Permissions.Length; i++)
        {
            identity.RolePermissions.Add(new RolePermission(1, i + 1));
            identity.RolePermissions.Add(new RolePermission(3, i + 1));
            if (Permissions[i] != "AUDIT_VIEW") identity.RolePermissions.Add(new RolePermission(2, i + 1));
        }
        await identity.SaveChangesAsync();
        
        await identity.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('users', 'id'), (SELECT MAX(id) FROM users))");
        await identity.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('roles', 'id'), (SELECT MAX(id) FROM roles))");
        await identity.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('permissions', 'id'), (SELECT MAX(id) FROM permissions))");
        jira.Projects.AddRange(Id(new Project(1,"A","Personal work"), 1), Id(new Project(1,"B","Shared work",projectLeadUserId:105), 2), Id(new Project(2,"C","Other tenant"), 3));
        await jira.SaveChangesAsync();
        await jira.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('projects', 'id'), (SELECT MAX(id) FROM projects))");
        await jira.Database.ExecuteSqlRawAsync("INSERT INTO project_members (tenant_id, project_id, user_id, role, created_at) VALUES (1,2,104,'DEVELOPER',CURRENT_TIMESTAMP)");
        foreach (var entry in new[] {(10L,1L,1L,101L), (11L,1L,1L,102L), (12L,1L,2L,102L), (20L,2L,3L,201L)})
        {
            var (id,tenant,project,user) = entry;
            var issue = Id(new Issue(tenant,project,IssueType.Task,$"{(project == 3 ? "C" : project == 2 ? "B" : "A")}-{id}",$"Issue {id}",null,"TODO","MEDIUM",user),id);
            issue.AssignTo(user); jira.Issues.Add(issue);
            jira.IssueComments.Add(new IssueComment(tenant,id,user,$"Comment {id}"));
        }
        await jira.SaveChangesAsync();
        await jira.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('issues', 'id'), (SELECT MAX(id) FROM issues))");
        await jira.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('issues', 'issue_number'), (SELECT GREATEST(MAX(id), MAX(issue_number)) FROM issues))");
        foreach (var tenant in new long[] {1,2}) audit.AuditLogs.Add(new Audit.Domain.Entities.AuditLog(Guid.NewGuid(),tenant,100,"ISSUE_UPDATED","ISSUE",10,"JIRA",null,null,"{}",DateTimeOffset.UtcNow));
        await audit.SaveChangesAsync();
    }

    public static T Id<T>(T entity, long id) where T : class
    { entity.GetType().GetProperty("Id")!.SetValue(entity,id); return entity; }

    public string Token(long user, long tenant = 1, string role = "DEVELOPER", string[]? permissions = null, string[]? portals = null)
    {
        var identity = new ClaimsIdentity("tests", "name", TenantClaimTypes.Role);
        identity.AddClaim(new Claim(TenantClaimTypes.UserId,user.ToString()));
        identity.AddClaim(new Claim(TenantClaimTypes.TenantId,tenant.ToString()));
        identity.AddClaim(new Claim(TenantClaimTypes.Role,role));
        foreach (var permission in permissions ?? Permissions) identity.AddClaim(new Claim(TenantClaimTypes.Permission,permission));
        foreach (var portal in portals ?? []) identity.AddClaim(new Claim(TenantClaimTypes.Portal,portal));
        return App.Services.GetRequiredService<IAccessTokenService>().CreateAccessToken(new ClaimsPrincipal(identity));
    }

    public static ClaimsPrincipal Google(string email, bool verified = true)
        => new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier,email), new Claim(ClaimTypes.Email,email),
            new Claim("email_verified", verified ? "true" : "false"), new Claim("name",email),
            
            new Claim(TenantClaimTypes.Role,"ADMIN"), new Claim(TenantClaimTypes.TenantId,"999") },"validated-google"));

    public async Task DisposeAsync() { Client.Dispose(); await App.DisposeAsync(); await _postgres.DisposeAsync(); }
}
