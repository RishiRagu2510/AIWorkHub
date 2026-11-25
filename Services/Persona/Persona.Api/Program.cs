using System.Net;
using System.Threading.RateLimiting;
using Authentication;
using Authorization;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Infrastructure.Persistence;
using Kafka;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Persona.Api.Controllers;
using Persona.Api.Services.Employees;
using Persona.Infrastructure.Persistence;
using TenantContext;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<EmployeeOnboardingValidation>();
builder.Services.AddOnboardingApproval(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(IPAddress.Parse(address));
});

builder.Services.AddSingleton(TimeProvider.System);

// Register both PersonaDbContext and IdentityTenantDbContext (for User/Role queries)
builder.Services.AddDbContext<PersonaDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Persona") ??
                           builder.Configuration.GetConnectionString("IdentityTenant");

    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Persona database connection string is not configured.");

    options.UseNpgsql(connectionString);
});

builder.Services.AddDbContext<IdentityTenantDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("IdentityTenant") ??
                           builder.Configuration.GetConnectionString("Persona");

    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("IdentityTenant database connection string is not configured.");

    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<TenantContext.TenantContext>();
builder.Services.AddScoped<ITenantContext>(serviceProvider =>
    serviceProvider.GetRequiredService<TenantContext.TenantContext>());

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));

builder.Services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
builder.Services.AddAiWorkHubJwtAuthentication(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddPermissionAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<EmployeeAccess>();
builder.Services.AddScoped<EmployeeEvents>();
builder.Services.AddScoped<EmployeeSetupService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<EmployeeCareerService>();
builder.Services.AddScoped<AbsenceService>();
builder.Services.AddScoped<EmployeePhotoService>();
builder.Services.AddScoped<EmployeeOnboardingService>();
builder.Services.AddScoped<IEmployeePhotoStorage, EmployeePhotoStorage>();
builder.Services.AddScoped<EmployeeCareerHistory>();
builder.Services.AddAiWorkHubKafka(builder.Configuration);
builder.Services.AddHostedService<EmployeeOutboxPublisher>();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
