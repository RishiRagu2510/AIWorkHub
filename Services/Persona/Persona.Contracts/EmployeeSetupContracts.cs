using System.ComponentModel.DataAnnotations;
namespace Persona.Contracts;





public sealed record SetupInput(
    [StringLength(200)] string? Name,
    [RegularExpression("^[A-Z0-9_]+$"), StringLength(100)] string? Code,
    [StringLength(1000)] string? Description,
    long? DepartmentId, long? GradeId, int DisplayOrder = 0, [StringLength(100)] string? Family = null, [StringLength(100)] string? Category = null, bool IsActive = true,
    long[]? PermissionIds = null,
    long[]? RoleIds = null);

public sealed record AssignRolesInput([Required] long[] RoleIds, long[]? ExpectedRoleIds = null);

public sealed record BulkAssignRolesInput([Required] long[] UserIds, [Required] long[] RoleIds, string Mode = "ADD");

public sealed record SkillDto(long Id, string Code, string Name, string? Category, string? Description, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);


public sealed record DepartmentDto(long Id, string Code, string Name, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record GradeDto(long Id, string Code, string? Family, string Name, int DisplayOrder, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record TitleDto(long Id, string Name, long DepartmentId, long GradeId, int DisplayOrder, bool IsActive);


public sealed record SavedTitleDto(long Id, string Name);

public sealed record SetupRoleDto(long Id, string Code, string Name, string? Description, IReadOnlyList<long> PermissionIds);

public sealed record SetupPermissionDto(long Id, string Code, string Name, string? Description);

public sealed record SetupCatalogDto(
    IReadOnlyList<SkillDto> Skills,
    IReadOnlyList<DepartmentDto> Departments,
    IReadOnlyList<GradeDto> Grades,
    IReadOnlyList<TitleDto> Titles,
    IReadOnlyList<SetupRoleDto> Roles,
    IReadOnlyList<SetupPermissionDto> Permissions);

public sealed record SetupUserDto(long Id, string DisplayName, string Email, long[] RoleIds);

public sealed record SetupUserPageDto(int TotalRoleAssignments, IReadOnlyList<SetupUserDto> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record UserRolesDto(long UserId, long[] RoleIds);

public sealed record BulkRolesResultDto(int UpdatedUsers, string Mode);

public sealed record DeleteAllResultDto(int Deleted, int Skipped);
