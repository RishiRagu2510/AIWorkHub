using Jira.Application.Abstractions.Services;
using Jira.Infrastructure.Persistence.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
namespace Jira.Api.Notifications;
public static class NotificationRegistration
{
    public static IServiceCollection AddJiraNotifications(this IServiceCollection services)
    {
        services.AddScoped<INotificationWriter, NotificationWriter>();
        services.AddScoped<NotificationInbox>();
        services.AddSignalR(options => options.MaximumParallelInvocationsPerClient = 1);
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var previous = options.Events.OnMessageReceived;
            options.Events.OnMessageReceived = async context =>
            {
                if (previous is not null) await previous(context);
                if (string.IsNullOrEmpty(context.Token) && (context.Request.Path.StartsWithSegments("/hubs/notifications") || context.Request.Path.StartsWithSegments("/hubs/issues") || context.Request.Path.StartsWithSegments("/hubs/boards")) &&
                    context.Request.Query.TryGetValue("access_token", out var token) && token.Count == 1)
                    context.Token = token[0];
            };
        });
        return services;
    }
}
