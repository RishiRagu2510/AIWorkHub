using Jira.Domain.Entities;
namespace Jira.Application.Abstractions.Services;
public interface INotificationWriter
{
    Task OnboardingAsync(Issue issue, long actorUserId, Guid eventId, string kind, long[] recipients, CancellationToken ct);
    
    Task AssignmentAsync(Issue issue, long actorUserId, Guid eventId, CancellationToken ct, IReadOnlyCollection<long>? alreadyAssigned = null);
    Task CommentAsync(Issue issue, long actorUserId, Guid eventId, CancellationToken ct, long[]? mentions = null);
}
