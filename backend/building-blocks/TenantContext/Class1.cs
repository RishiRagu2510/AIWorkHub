namespace TenantContext;

public interface ITenantContext
{
    long UserId { get; }

    long TenantId { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsAuthenticated { get; }
}