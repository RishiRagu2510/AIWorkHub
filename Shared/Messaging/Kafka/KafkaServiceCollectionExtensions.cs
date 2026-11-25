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

        services.AddSingleton<IKafkaProducer, KafkaProducer>();

        return services;
    }
}