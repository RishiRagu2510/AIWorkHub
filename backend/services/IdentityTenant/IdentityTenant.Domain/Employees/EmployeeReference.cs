namespace IdentityTenant.Domain.Employees;

public sealed class EmployeeReference
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public uint Version { get; set; }
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
}