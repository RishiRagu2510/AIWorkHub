using System.Net;
using System.Threading.RateLimiting;
using Authentication;
using Authorization;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Api.Configuration;
using IdentityTenant.Api.Controllers;
using IdentityTenant.Api.Middleware;
using IdentityTenant.Api.Services.Invitations;
using IdentityTenant.Api.Services.Timesheets;
using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Application.Abstractions.Services;
using IdentityTenant.Application.Plans;
using IdentityTenant.Application.Services;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Infrastructure.Persistence;
using IdentityTenant.Infrastructure.Persistence.Repositories;
using Kafka;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using TenantContext;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddOnboardingApproval(builder.Configuration);
builder.Services.AddScoped<PasswordLoginService>();
builder.Services.AddScoped<IVerificationEmailSender, VerificationEmailSender>();
builder.Services.AddSingleton<OperatorActionTokens>();
builder.Services.AddSingleton<IAccessRequestNotifier, AccessRequestEmailNotifier>();
builder.Services.Configure<PasswordHasherOptions>(o => o.IterationCount = 210000);
builder.Services.AddScoped<IPasswordHasher<LocalCredential>,
    PasswordHasher<LocalCredential>>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("password-auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
            { PermitLimit = builder.Environment.IsDevelopment() ? 200 : 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(IPAddress.Parse(address));
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<LoginSessionOptions>()
    .Bind(builder.Configuration.GetSection("Authentication:Session"))
    .Validate(settings => settings.IdleHours > 0 && settings.AbsoluteDays > 0 &&
                          settings.AbsoluteDays <= 90 && settings.IdleHours <= settings.AbsoluteDays * 24,
        "Session idle hours must be positive and no longer than the absolute lifetime (1–90 days).")
    .ValidateOnStart();
builder.Services.AddScoped<LoginSessionService>();
var protection = builder.Services.AddDataProtection().SetApplicationName("AIWorkHub.IdentityTenant");
var keyPath = builder.Configuration["Authentication:Session:KeyRingPath"];
if (!string.IsNullOrWhiteSpace(keyPath))
    protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
else if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException(
        "Authentication:Session:KeyRingPath must identify durable protected key storage.");
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-Google-CSRF";
    options.Cookie.Name = "AIWorkHub.GoogleCsrf";
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;

    var domain = builder.Configuration["Authentication:CookieDomain"]
        ?? Environment.GetEnvironmentVariable("COOKIE_DOMAIN");
    if (!string.IsNullOrWhiteSpace(domain))
    {
        options.Cookie.Domain = domain;
        options.Cookie.SameSite = SameSiteMode.Lax;
    }
    options.Cookie.Extensions.Add("Partitioned");
});
builder.Services.AddSingleton<IGoogleCredentialValidator,
    GoogleCredentialValidator>();
builder.Services.AddScoped<CompanyPrincipalService>();
builder.Services.AddScoped<IUserDirectory, UserDirectory>();

builder.Services.AddDbContext<IdentityTenantDbContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("IdentityTenant");

    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException(
            "IdentityTenant database connection string is not configured.");

    options.UseNpgsql(connectionString);
});


builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<IUserProvisioningService, UserProvisioningService>();
builder.Services.AddScoped<IMembershipRepository, MembershipRepository>();


builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<ITenantPlanStore, TenantPlanStore>();
builder.Services.AddScoped<TenantPlanService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<
    IUserExternalIdentityRepository,
    UserExternalIdentityRepository>();
builder.Services.AddScoped<
    IIdentityProviderRepository,
    IdentityProviderRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IUserRoleRepository, UserRoleRepository>();
builder.Services.AddScoped<
    IUserPermissionRepository,
    UserPermissionRepository>();

builder.Services.AddScoped<IUnitOfWork>(sp =>
    sp.GetRequiredService<IdentityTenantDbContext>());


builder.Services.AddScoped<TenantContext.TenantContext>();

builder.Services.AddScoped<ITenantContext>(serviceProvider =>
    serviceProvider.GetRequiredService<TenantContext.TenantContext>());


builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(
        JwtSettings.SectionName));

builder.Services.AddSingleton<
    IAccessTokenService,
    JwtAccessTokenService>();

builder.Services.AddAiWorkHubJwtAuthentication(builder.Configuration);

builder.Services
    .AddAuthentication(IdentityCookieConfiguration.ConfigureAuthentication)
    .AddCookie(options =>
    {
        IdentityCookieConfiguration.ConfigureCookie(options, builder.Configuration["Authentication:CookieDomain"]);
        if (builder.Environment.IsDevelopment())
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    })
    .AddGoogleOpenIdConnect(options =>
    {
        var clientId =
            builder.Configuration["Authentication:Google:ClientId"];

        var clientSecret =
            builder.Configuration["Authentication:Google:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException(
                "Google OAuth client ID is not configured.");

        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException(
                "Google OAuth client secret is not configured.");

        options.ClientId = clientId;
        options.ClientSecret = clientSecret;
        options.CallbackPath = "/signin-oidc";
        options.ResponseType = "code";
        options.ResponseMode = "query";
        options.UsePkce = true;

        options.SaveTokens = false;

        options.TokenValidationParameters.NameClaimType = "name";

        options.TokenValidationParameters.RoleClaimType =
            TenantClaimTypes.Role;

        if (builder.Environment.IsDevelopment())
        {
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.NonceCookie.SameSite = SameSiteMode.Lax;
            options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        }

        options.Events.OnRemoteFailure = context =>
        {
            context.HandleResponse();
            var frontendBaseUrl = builder.Configuration["Frontend:BaseUrl"];
            if (string.IsNullOrWhiteSpace(frontendBaseUrl) && builder.Environment.IsDevelopment())
                frontendBaseUrl = "http://localhost:5173";

            if (Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out var frontendUri) &&
                (frontendUri.Scheme == Uri.UriSchemeHttps ||
                 (builder.Environment.IsDevelopment() && frontendUri.Scheme == Uri.UriSchemeHttp)))
            {
                var callback = new Uri(frontendUri, "/auth/callback").AbsoluteUri;
                context.Response.Redirect(QueryHelpers.AddQueryString(
                    callback, "error", "GOOGLE_SIGN_IN_FAILED"));
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }

            return Task.CompletedTask;
        };

        options.Events.OnTicketReceived = CompanyGoogleTicket.HandleAsync;
    });

builder.Services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme,
    options => options.TicketDataFormat = new CompactSessionTicketFormat(options.TicketDataFormat));
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestHeadersTotalSize = 64 * 1024);

builder.Services.AddAuthorization();

builder.Services.AddPermissionAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<InvitationService>();
builder.Services.AddScoped<TimesheetApproverService>();
builder.Services.AddAiWorkHubKafka(builder.Configuration);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();

app.UseMiddleware<TenantContextMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();