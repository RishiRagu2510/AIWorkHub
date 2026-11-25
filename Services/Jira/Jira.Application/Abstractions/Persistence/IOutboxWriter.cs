using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jira.Application.Abstractions.Persistence;

public interface IOutboxWriter
{
    Task AddAsync(
        Guid eventId,
        string eventType,
        string topic,
        string payload,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);
}