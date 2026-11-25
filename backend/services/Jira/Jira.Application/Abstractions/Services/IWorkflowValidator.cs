namespace Jira.Application.Abstractions.Services;
public interface IWorkflowValidator
{
    Task ValidateAsync(long tenantId,long projectId,string? from,string to,CancellationToken ct,bool confirmSkip=false);
}
