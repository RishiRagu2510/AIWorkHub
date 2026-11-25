using Authentication;
using Jira.Api.Collaboration;
using Jira.Api.Notifications;
using Authorization;
using Jira.Api.Middleware;
using Jira.Application.Abstractions.Persistence;
using Jira.Application.Abstractions.Services;
using Jira.Application.Services;
using Jira.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence.Outbox;
using Jira.Infrastructure.Persistence.Repositories;
using Kafka;
using Microsoft.EntityFrameworkCore;
using TenantContext;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

builder.Services.AddControllers(options => { options.Filters.Add<IdempotencyFilter>(); options.Filters.Add<Jira.Api.Middleware.WritePreconditionFilter>(); });
builder.Services.AddJiraNotifications();
        builder.Services.AddCollaboration(builder.Configuration);
builder.Services.AddSingleton<Jira.Api.Collaboration.IAttachmentStorage, Jira.Api.Collaboration.AttachmentStorage>();
builder.Services.AddHostedService<AttachmentCleanup>();
builder.Services.AddScoped<Jira.Infrastructure.Persistence.Timesheets.TimesheetService>();
builder.Services.AddHttpClient<ITimesheetApproverDirectory,Jira.Api.Integrations.IdentityTenant.TimesheetApproverDirectory>(client => {
    client.BaseAddress = new Uri(builder.Configuration["Services:IdentityTenant:BaseUrl"]!.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddOnboardingApproval(builder.Configuration);
builder.Services.AddHttpClient<Jira.Api.Onboarding.EmployeeOnboardingClient>(client => {
    var baseUrl = builder.Configuration["Services:Persona:BaseUrl"] ?? builder.Configuration["Services:IdentityTenant:BaseUrl"] ?? "http://localhost:5234";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<JiraResourceScope>();
builder.Services.AddScoped<WritePrecondition>();
builder.Services.AddScoped<ProjectAuditWriter>();
builder.Services.AddScoped<ProjectMembershipService>();
builder.Services.AddScoped<Jira.Infrastructure.Persistence.Accounts.AccountService>();
builder.Services.AddScoped<Jira.Infrastructure.Persistence.Allocations.AllocationService>();
builder.Services.AddScoped<Jira.Infrastructure.Persistence.Preferences.PreferenceService>();
builder.Services.AddScoped<Jira.Api.Services.Onboarding.EmployeeRequestService>();
builder.Services.AddScoped<IAssigneeValidator>(sp => sp.GetRequiredService<ProjectMembershipService>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IDeveloperDirectory, Jira.Api.Integrations.IdentityTenant.DeveloperDirectoryClient>(client =>
{
    var baseUrl = builder.Configuration["Services:IdentityTenant:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl)) throw new InvalidOperationException("IdentityTenant service URL is not configured.");
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });




builder.Services.AddDbContext<JiraDbContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("Jira");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Jira database connection string is not configured.");
    }

    options.UseNpgsql(connectionString);
});




builder.Services.AddScoped<
    IProjectRepository,
    ProjectRepository>();

builder.Services.AddScoped<
    IIssueRepository,
    IssueRepository>();

builder.Services.AddScoped<
    IIssueCommentRepository,
    IssueCommentRepository>();




builder.Services.AddScoped<
    IOutboxWriter,
    OutboxWriter>();

builder.Services.AddScoped<
    IJiraUnitOfWork,
    JiraUnitOfWork>();

builder.Services.AddScoped<
    OutboxLeaseService>();

builder.Services.AddScoped<
    OutboxCleanupService>();

builder.Services.AddHostedService<
    OutboxPublisherBackgroundService>();

builder.Services
    .Configure<OutboxCleanupOptions>(
        builder.Configuration.GetSection("OutboxCleanup"));

builder.Services.AddHostedService<
    OutboxCleanupBackgroundService>();




builder.Services.AddScoped<ProjectService>();

builder.Services.AddScoped<
    IIssueService,
    IssueService>();

builder.Services.AddScoped<
    IIssueCommentService,
    IssueCommentService>();




builder.Services.AddAiWorkHubKafka(
    builder.Configuration);




builder.Services.AddScoped<
    TenantContext.TenantContext>();

builder.Services.AddScoped<ITenantContext>(
    serviceProvider =>
        serviceProvider.GetRequiredService<
            TenantContext.TenantContext>());




builder.Services.AddAiWorkHubJwtAuthentication(
    builder.Configuration);




builder.Services.AddAuthorization();

builder.Services.AddPermissionAuthorization();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();




app.UseMiddleware<ExceptionHandlingMiddleware>();




app.UseHttpsRedirection();




app.UseAuthentication();




app.UseMiddleware<TenantContextMiddleware>();




app.UseAuthorization();




app.MapControllers();
        app.MapHub<CollaborationHub>("/hubs/issues", options => options.CloseOnAuthenticationExpiration = true);
        app.MapHub<CollaborationHub>("/hubs/boards/{boardId:long}", options => options.CloseOnAuthenticationExpiration = true);
app.MapHub<NotificationsHub>("/hubs/notifications", options => options.CloseOnAuthenticationExpiration = true);

app.Run();
