using Jira.Application.Abstractions.Services;
using Jira.Infrastructure.Persistence.Collaboration;
using StackExchange.Redis;
namespace Jira.Api.Collaboration;
public static class CollaborationRegistration
{
    public static IServiceCollection AddCollaboration(this IServiceCollection services,IConfiguration configuration,bool background=true)
    {
        services.AddScoped<IMentionService,MentionService>();
        services.AddScoped<WorkflowService>();services.AddScoped<IWorkflowValidator>(sp=>sp.GetRequiredService<WorkflowService>());
        services.AddScoped<CollaborationService>();services.AddScoped<ActivityProjection>();
        services.AddScoped<Jira.Api.Services.Planning.PlanningService>();services.AddScoped<AttachmentService>();services.AddScoped<CommentEditService>();
        services.AddSingleton<IConnectionMultiplexer>(_=>ConnectionMultiplexer.Connect(configuration["Redis:ConnectionString"]??"localhost:6379,abortConnect=false"));
        services.AddSingleton<IPresenceStore,RedisPresenceStore>();
        if(background){services.AddHostedService<ActivityConsumer>();services.AddHostedService<CollaborationJobs>();}
        return services;
    }
}
