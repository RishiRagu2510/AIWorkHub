using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Kafka;

public sealed class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaProducer(
        IOptions<KafkaOptions> options)
    {
        var config = new ProducerConfig
        {
            BootstrapServers =
                options.Value.BootstrapServers,

            MessageTimeoutMs = 5000,
            RequestTimeoutMs = 2000,
            SocketTimeoutMs = 2000
        };

        _producer =
            new ProducerBuilder<string, string>(
                    config)
                .Build();
    }

    public async Task PublishAsync<T>(
        string topic,
        T message,
        CancellationToken cancellationToken = default)
    {
        var payload =
            JsonSerializer.Serialize(message);

        await _producer.ProduceAsync(
            topic,
            new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = payload
            },
            cancellationToken);
    }

    public void Dispose()
    {
        _producer.Flush(
            TimeSpan.FromSeconds(5));

        _producer.Dispose();
    }
}