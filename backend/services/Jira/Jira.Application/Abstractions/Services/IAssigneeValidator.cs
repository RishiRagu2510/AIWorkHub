namespace Jira.Application.Abstractions.Services;
public interface IAssigneeValidator
{
    Task ValidateAsync(long tenantId, long projectId, long userId, CancellationToken ct);
}
