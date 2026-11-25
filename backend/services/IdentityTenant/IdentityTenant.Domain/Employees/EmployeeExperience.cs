namespace IdentityTenant.Domain.Employees;

public sealed class EmployeeExperience
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public uint Version { get; set; }
    public long EmployeeId { get; set; }
    public string Company { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}