namespace Jira.Contracts.Collaboration;

public sealed record AttachmentDto(Guid Id, long TenantId, long IssueId, long AuthorUserId, string Name, long Size, DateTimeOffset CreatedAt);

public sealed record ActivityEventDto(Guid EventId, long TenantId, string EntityType, long EntityId, long? ProjectId, long ActorUserId, string Action, string? OldValues, string? NewValues, DateTimeOffset OccurredAt);

public sealed record ActivityPageDto(IReadOnlyList<ActivityEventDto> Items, int Page, int PageSize, int TotalCount);

public sealed record EditCommentRequest(string Body);


public sealed record EditedCommentDto(long Id, string Body, long AuthorUserId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, uint Version);
