namespace TenantContext;

public sealed class TenantContext : ITenantContext
{
    private readonly List<string> _roles = [];

    public long UserId { get; private set; }

    public long TenantId { get; private set; }

    public IReadOnlyCollection<string> Roles => _roles.AsReadOnly();

    public bool IsAuthenticated { get; private set; }

    public void Set(
        long userId,
        long tenantId,
        IEnumerable<string> roles)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tenantId));
        }

        ArgumentNullException.ThrowIfNull(roles);

        UserId = userId;
        TenantId = tenantId;

        _roles.Clear();

        _roles.AddRange(
            roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim().ToUpperInvariant())
                .Distinct());

        IsAuthenticated = true;
    }

    public void Clear()
    {
        UserId = 0;
        TenantId = 0;

        _roles.Clear();

        IsAuthenticated = false;
    }
}