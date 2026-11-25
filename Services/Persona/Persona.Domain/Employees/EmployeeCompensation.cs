namespace Persona.Domain.Employees;

public sealed class EmployeeCompensation
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public uint Version { get; set; }
    public long EmployeeId { get; set; }
    public decimal Ctc { get; set; }
    public decimal Bonus { get; set; }
    public string Currency { get; set; } = "";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}