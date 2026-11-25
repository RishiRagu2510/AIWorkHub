using Authentication;
using Audit.Application.Abstractions;
using Audit.Application.Services;
using Audit.Infrastructure.Messaging;
using Audit.Infrastructure.Persistence;
using Audit.Infrastructure.Persistence.Repositories;
using Authorization;
using Kafka;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHostedService<AuditRetentionService>();




builder.Services.AddDbContext<AuditDbContext>(
    options =>
    {
        var connectionString =
            builder.Configuration
                .GetConnectionString("Audit");

        if (string.IsNullOrWhiteSpace(
                connectionString))
        {
            throw new InvalidOperationException(
                "Audit database connection string is not configured.");
        }

        options.UseNpgsql(connectionString);
    });




builder.Services.AddScoped<
    IAuditLogRepository,
    AuditLogRepository>();




builder.Services.AddScoped<
    AuditLogService>();




builder.Services.AddAiWorkHubKafka(
    builder.Configuration);




builder.Services.AddHostedService<
    IssueAuditConsumer>();




builder.Services.AddAiWorkHubJwtAuthentication(
    builder.Configuration);




builder.Services.AddAuthorization();

builder.Services.AddPermissionAuthorization();

var app = builder.Build();

app.UseHttpsRedirection();




app.UseAuthentication();




app.UseAuthorization();




app.MapControllers();

app.Run();