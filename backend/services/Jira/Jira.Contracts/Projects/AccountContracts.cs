using System.ComponentModel.DataAnnotations;

namespace Jira.Contracts.Projects;

public sealed record AccountRequest([Required, StringLength(200)] string Name, string Status = "ACTIVE");


public sealed record AccountDto(long Id, long TenantId, string Name, string Status, uint Version);


public sealed record AccountSummaryDto(long Id, string Name, string Status, uint Version, int ProjectCount);
