using Jira.Domain.Entities;
namespace Jira.Application.Abstractions.Services;
public interface IMentionService
{
    Task NotifyAsync(Issue issue,long actor,string body,IReadOnlyList<long> users,Guid eventId,CancellationToken ct);
}
