using System.ComponentModel.DataAnnotations;

namespace Jira.Contracts.Planning;

public sealed record NamedRequest([Required, StringLength(150, MinimumLength = 1)] string Name, [StringLength(4000)] string? Description = null);

public sealed record VersionRequest([Required, StringLength(100, MinimumLength = 1)] string Name, [StringLength(4000)] string? Description = null, string Status = "UNRELEASED", DateOnly? ReleaseDate = null);

public sealed record SprintRequest([Required, StringLength(200, MinimumLength = 1)] string Name, [StringLength(4000)] string? Goal = null, DateOnly? StartDate = null, DateOnly? EndDate = null);

public sealed record Placement(long? SprintId, long Position = 0);

public sealed record ComponentDto(long Id, long TenantId, long ProjectId, string Name, string? Description, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, uint Version);

public sealed record ReleaseDto(long Id, long TenantId, long ProjectId, string Name, string ReleaseVersion, string? Description, string Status, DateOnly? ReleaseDate, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, uint Version);

public sealed record SprintDto(long Id, long TenantId, long ProjectId, long? BoardId, string Name, string? Goal, string Status, DateOnly? StartDate, DateOnly? EndDate, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, uint Version);

public sealed record BacklogItemDto(long Id, string IssueKey, string Summary, string StatusCode, uint Version, long BacklogOrder);

public sealed record BacklogPageDto(IReadOnlyList<BacklogItemDto> Items, int Page, int PageSize, int TotalCount);

public sealed record BoardDto(long Id, long TenantId, long ProjectId, string Name, string BoardType, string FilterDefinition, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
