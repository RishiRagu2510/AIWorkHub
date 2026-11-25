using Jira.Contracts.Timesheets;
namespace Jira.Application.Abstractions.Services;
public interface ITimesheetApproverDirectory {
    Task<IReadOnlyList<TimesheetApprover>> ListAsync(CancellationToken ct);
}
