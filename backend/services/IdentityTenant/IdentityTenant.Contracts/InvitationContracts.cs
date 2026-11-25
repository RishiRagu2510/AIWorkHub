namespace IdentityTenant.Contracts;

public sealed record InviteRequest(string Email, int ExpiresInDays = 7);

public sealed record InvitationDto(Guid Id, long TenantId, string Email, string RoleCode, long InvitedByUserId, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? AcceptedAt, DateTimeOffset? RevokedAt);

public sealed record InvitationPageDto(IReadOnlyList<InvitationDto> Items, int Page, int PageSize, int TotalCount);
