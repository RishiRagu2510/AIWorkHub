using Jira.Domain.Common;
namespace Jira.Domain.Entities;
public sealed class Account : Entity
{
    private Account() { }
    public Account(long tenantId, string name)
    {
        if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) throw new ArgumentException("Account name must contain 1–200 characters.");
        TenantId = tenantId; Name = name.Trim();
    }
    public string Status { get; private set; } = "ACTIVE";
    public uint Version { get; private set; }
    public void Update(string name, string status)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) throw new ArgumentException("Account name must contain 1–200 characters.");
        if (status is not ("ACTIVE" or "ARCHIVED")) throw new ArgumentException("Invalid account status.");
        Name = name.Trim(); Status = status; Touch();
    }
    public long TenantId { get; private set; }
    public string Name { get; private set; } = null!;
}
