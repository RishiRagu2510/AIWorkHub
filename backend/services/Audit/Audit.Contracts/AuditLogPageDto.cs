namespace Audit.Contracts;

public sealed class AuditLogPageDto
{
    public IReadOnlyList<AuditLogDto> Items { get; init; } =
        Array.Empty<AuditLogDto>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public long TotalCount { get; init; }

    public int TotalPages { get; init; }
}