namespace IdentityTenant.Domain.Employees;

public sealed class EmployeeEducation
{
    public string? FieldOfStudy { get; set; }
    public long Id { get; set; }
    public long TenantId { get; set; }
    public uint Version { get; set; }
    public long EmployeeId { get; set; }
    public string Institution { get; set; } = "";
    public string Qualification { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}