using System.Text.Json;
namespace IdentityTenant.Contracts;







public sealed record EmployeeProfileDto(
    string[]? Roles, long Id, long UserId, string EmployeeCode, string FirstName, string LastName, string? PreferredName, string WorkEmail, string? WorkAddress,
    string? Phone, string? AadharNumber, string? PermanentAddress, string? EmergencyContactName, string? EmergencyContactPhone, string? EmergencyContactRelation,
    string? ProfilePhotoUrl, long? DepartmentId, long? DisciplineId, long? JobTitleId, long? DesignationId, long? GradeId, long? CareerProfileId, long? CareerLevelId,
    long? LocationId, long? ManagerEmployeeId, string EmploymentType, string EmploymentStatus, DateOnly JoiningDate, DateOnly? EndDate,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, uint Version);

public sealed record EmployeeListDto(IReadOnlyList<EmployeeProfileDto> Items, int Page, int PageSize, int TotalCount, int TotalPages, int MaxEmployeeNumber);

public sealed record ManagerOptionDto(long Id, long UserId, string FirstName, string LastName, string EmployeeCode, string WorkEmail);

public sealed record LoginUserDto(long Id, string DisplayName, string Email);

public sealed record LoginUserPageDto(IReadOnlyList<LoginUserDto> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record OrgChartNodeDto(
    long Id, string FirstName, string LastName, string EmployeeCode, long? ManagerEmployeeId, long? JobTitleId, long? DepartmentId, long? LocationId,
    string? ProfilePhotoUrl, string? CareerTitle, int DirectReportCount, bool HasReports);

public sealed record OrgChartPageDto(IReadOnlyList<OrgChartNodeDto> Items, int Page, int PageSize, int TotalCount);

public sealed record CareerHistoryEntryDto(long Id, DateOnly? EffectiveDate, DateTimeOffset RecordedAt, long RecordedBy, string ChangeType, JsonElement Details, string? Note);

public sealed record CatalogSkillDto(long Id, string Name, string? Category, string? Description);

public sealed record CatalogGradeDto(long Id, string Code, string Name, string? Family);

public sealed record CatalogProfileDto(long Id, string Code, string Name);

public sealed record CatalogLevelDto(long Id, long CareerProfileId, long GradeId, string Grade, string CareerTitle);

public sealed record CareerCatalogDto(
    IReadOnlyList<CatalogSkillDto> Skills, IReadOnlyList<CatalogGradeDto> Grades, IReadOnlyList<CatalogProfileDto> Profiles, IReadOnlyList<CatalogLevelDto> Levels);


public sealed record EmployeeReferenceDto(long Id, long TenantId, uint Version, string Kind, string Name);
