using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kafka;

public static class KafkaServiceCollectionExtensions
{
    public static IServiceCollection AddAiWorkHubKafka(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(
            configuration.GetSection("Kafka"));

        var bootstrap = configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrap) ||
            bootstrap.Equals("disabled", StringComparison.OrdinalIgnoreCase) ||
            bootstrap.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IKafkaProducer, NoOpKafkaProducer>();
            return services;
        }

        services.AddSingleton<IKafkaProducer, KafkaProducer>();

        return services;
    }
}

public sealed class NoOpKafkaProducer : IKafkaProducer
{
    public Task PublishAsync<T>(string topic, T message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
