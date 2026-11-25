using System.ComponentModel.DataAnnotations;

namespace IdentityTenant.Contracts;

public sealed record AbsenceInput([Required] string Kind, DateOnly StartDate, DateOnly EndDate, [Required, MaxLength(1000)] string Reason);

public sealed record AbsenceDecision([Required] string Action, int Version, [MaxLength(1000)] string? Note);

public sealed record AbsencePolicyInput([Range(0, 366)] int CasualDays, [Range(0, 366)] int SickDays, [Range(0, 366)] int EarnedDays, DateOnly[] Holidays, int Version);


public sealed record AbsencePolicyDto(int Year, int CasualDays, int SickDays, int EarnedDays, DateOnly[] Holidays, int Version, bool Configured);


public sealed record AbsenceRequestDto(
    long Id, long EmployeeId, string EmployeeCode, string EmployeeName, string Kind, DateOnly StartDate, DateOnly EndDate, int Days,
    string Reason, string Status, string? DecisionNote, long? DecidedBy, DateTimeOffset CreatedAt, int Version);

public sealed record AbsenceBalanceDto(string Kind, int Allowance, int Used, int Pending, int Available);

public sealed record AbsenceManagerDto(long Id, string Name);

public sealed record AbsenceListDto(
    IReadOnlyList<AbsenceRequestDto> Items, int TotalCount, int Page, int PageSize, int TotalPages,
    IReadOnlyList<AbsenceBalanceDto> Balances, AbsenceManagerDto? Manager, bool HasReports, bool PolicyConfigured);
