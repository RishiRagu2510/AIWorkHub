using System.ComponentModel.DataAnnotations;

namespace Jira.Contracts.Projects;

public sealed record AllocationRequest([Range(1, long.MaxValue)] long ProjectId, [Range(1, long.MaxValue)] long UserId, [Range(1, 100)] int Percentage, DateOnly StartDate, DateOnly? EndDate);

public sealed record AllocationDto(long Id, long ProjectId, string ProjectName, long UserId, int Percentage, DateOnly StartDate, DateOnly? EndDate);
